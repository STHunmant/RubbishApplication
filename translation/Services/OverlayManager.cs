using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace App1.Services;

public class OverlayManager
{
    private readonly ScreenCaptureService _captureService;
    private readonly OcrService _ocrService;
    private readonly TranslationService _translationService;
    private readonly OverlayWindow _overlayWindow;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private readonly SemaphoreSlim _translationSemaphore = new(6, 6);
    private readonly string _jsonFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public OverlayManager()
    {
        _captureService = new ScreenCaptureService();
        _ocrService = new OcrService();
        _translationService = new TranslationService();
        _overlayWindow = new OverlayWindow();
        _jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_capture.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task StartAsync()
    {
        _overlayWindow.ShowOverlay();
        _cts = new CancellationTokenSource();
        _processingTask = ProcessingLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_processingTask != null)
        {
            try { await _processingTask; }
            catch (OperationCanceledException) { }
        }
        _overlayWindow.HideOverlay();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ProcessingLoopAsync(CancellationToken ct)
    {
        var sw = new System.Diagnostics.Stopwatch();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                sw.Restart();

                var captureInfo = _captureService.CaptureScreen(0.5);

                var ocrResults = await _ocrService.RecognizeEnglishTextAsync(captureInfo);

                if (ocrResults.Count == 0)
                {
                    captureInfo.FullBitmap.Dispose();
                    captureInfo.OcrBitmap.Dispose();
                    _overlayWindow.UpdateOverlays(new List<OverlayItem>());
                    await CooldownAsync(sw, ct);
                    continue;
                }

                SaveToJsonFile(ocrResults);

                var entries = LoadFromJsonFile();

                await TranslateJsonEntriesAsync(entries, ct);

                var overlayItems = BuildOverlayItems(entries, captureInfo);

                SaveTranslatedJsonFile(entries);

                captureInfo.FullBitmap.Dispose();
                captureInfo.OcrBitmap.Dispose();

                _overlayWindow.UpdateOverlays(overlayItems);

                await CooldownAsync(sw, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(300, ct); }
        }
    }

    private void SaveToJsonFile(List<OcrResult> ocrResults)
    {
        var entries = new List<JsonCaptureEntry>();
        foreach (var ocr in ocrResults)
        {
            var hash = FnvHash(ocr.Text);
            var existing = LoadExistingTranslation(hash);

            entries.Add(new JsonCaptureEntry
            {
                Text = ocr.Text,
                X = ocr.BoundingBox.X,
                Y = ocr.BoundingBox.Y,
                Width = ocr.BoundingBox.Width,
                Height = ocr.BoundingBox.Height,
                Translated = existing,
                TextHash = hash
            });
        }

        var json = JsonSerializer.Serialize(entries, _jsonOptions);
        File.WriteAllText(_jsonFilePath, json);
    }

    private static string? LoadExistingTranslation(string hash)
    {
        string translatedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_translated.json");
        if (!File.Exists(translatedPath)) return null;

        try
        {
            var json = File.ReadAllText(translatedPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null && dict.TryGetValue(hash, out var translated))
                return translated;
        }
        catch { }

        return null;
    }

    private static List<JsonCaptureEntry> LoadFromJsonFile()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_capture.json");
        if (!File.Exists(path))
            return new List<JsonCaptureEntry>();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<JsonCaptureEntry>>(json) ?? new List<JsonCaptureEntry>();
        }
        catch
        {
            return new List<JsonCaptureEntry>();
        }
    }

    private async Task TranslateJsonEntriesAsync(List<JsonCaptureEntry> entries, CancellationToken ct)
    {
        var untranslated = entries.Where(e => string.IsNullOrEmpty(e.Translated)).ToList();
        if (untranslated.Count == 0) return;

        var tasks = untranslated.Select(e => TranslateEntryAsync(e, ct)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task TranslateEntryAsync(JsonCaptureEntry entry, CancellationToken ct)
    {
        await _translationSemaphore.WaitAsync(ct);
        try
        {
            var translated = await _translationService.TranslateEnglishToChineseAsync(entry.Text);
            entry.Translated = string.IsNullOrEmpty(translated) ? entry.Text : translated;
        }
        finally { _translationSemaphore.Release(); }
    }

    private void SaveTranslatedJsonFile(List<JsonCaptureEntry> entries)
    {
        string translatedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_translated.json");
        Dictionary<string, string> dict;

        if (File.Exists(translatedPath))
        {
            try
            {
                var existing = File.ReadAllText(translatedPath);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(existing) ?? new Dictionary<string, string>();
            }
            catch
            {
                dict = new Dictionary<string, string>();
            }
        }
        else
        {
            dict = new Dictionary<string, string>();
        }

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Translated) && !string.IsNullOrEmpty(entry.TextHash))
            {
                dict[entry.TextHash] = entry.Translated;
            }
        }

        var json = JsonSerializer.Serialize(dict, _jsonOptions);
        File.WriteAllText(translatedPath, json);
    }

    private List<OverlayItem> BuildOverlayItems(List<JsonCaptureEntry> entries, ScreenCaptureInfo captureInfo)
    {
        var items = new List<OverlayItem>();

        foreach (var entry in entries)
        {
            var translated = entry.Translated ?? entry.Text;

            var rect = new System.Drawing.Rectangle(entry.X, entry.Y, entry.Width, entry.Height);
            var (maskColor, textColor) = AnalyzeAreaColor(captureInfo.FullBitmap, rect);

            int paddingX = 6;
            int paddingY = 3;

            double wpfX = (entry.X - paddingX) / captureInfo.DpiScaleX;
            double wpfY = (entry.Y - paddingY) / captureInfo.DpiScaleY;
            double wpfW = (entry.Width + paddingX * 2) / captureInfo.DpiScaleX;
            double wpfH = (entry.Height + paddingY * 2) / captureInfo.DpiScaleY;

            double fontSize = Math.Max(10,
                Math.Min(entry.Height / captureInfo.DpiScaleY * 0.65, 32));

            items.Add(new OverlayItem
            {
                X = wpfX,
                Y = wpfY,
                Width = Math.Max(wpfW, 40),
                Height = wpfH,
                ChineseText = translated,
                MaskColor = maskColor,
                TextColor = textColor,
                FontSize = fontSize
            });
        }

        return items;
    }

    private static string FnvHash(string text)
    {
        const uint fnvPrime = 0x01000193;
        uint hash = 0x811C9DC5;
        foreach (char c in text)
        {
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
        }
        return hash.ToString("X8");
    }

    private static async Task CooldownAsync(System.Diagnostics.Stopwatch sw, CancellationToken ct)
    {
        var elapsed = sw.ElapsedMilliseconds;
        if (elapsed < 900)
            await Task.Delay((int)(900 - elapsed), ct);
    }

    private static (System.Windows.Media.Color maskColor, System.Windows.Media.Color textColor)
        AnalyzeAreaColor(System.Drawing.Bitmap fullBitmap, System.Drawing.Rectangle rect)
    {
        try
        {
            int sampleX = Math.Max(0, rect.X - 3);
            int sampleY = Math.Max(0, rect.Y - 3);
            int sampleW = Math.Min(8, rect.Width + 6);
            int sampleH = Math.Min(8, rect.Height + 6);
            sampleW = Math.Min(sampleW, fullBitmap.Width - sampleX);
            sampleH = Math.Min(sampleH, fullBitmap.Height - sampleY);
            if (sampleW < 1) sampleW = 1;
            if (sampleH < 1) sampleH = 1;

            long totalR = 0, totalG = 0, totalB = 0;
            long count = 0;

            for (int y = sampleY; y < sampleY + sampleH; y++)
            {
                for (int x = sampleX; x < sampleX + sampleW; x++)
                {
                    var pixel = fullBitmap.GetPixel(x, y);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    count++;
                }
            }

            if (count == 0)
                return (System.Windows.Media.Color.FromArgb(220, 40, 40, 40),
                        System.Windows.Media.Colors.White);

            byte avgR = (byte)(totalR / count);
            byte avgG = (byte)(totalG / count);
            byte avgB = (byte)(totalB / count);

            double brightness = (avgR * 0.299 + avgG * 0.587 + avgB * 0.114) / 255.0;

            System.Windows.Media.Color textColor = brightness > 0.5
                ? System.Windows.Media.Color.FromRgb(20, 20, 20)
                : System.Windows.Media.Color.FromRgb(240, 240, 240);

            return (System.Windows.Media.Color.FromArgb(225, avgR, avgG, avgB), textColor);
        }
        catch
        {
            return (System.Windows.Media.Color.FromArgb(220, 40, 40, 40),
                    System.Windows.Media.Colors.White);
        }
    }
}
