using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace App1.Services;

public class OcrResult
{
    public string Text { get; set; } = string.Empty;
    public System.Drawing.Rectangle BoundingBox { get; set; }
}

public class OcrService
{
    private readonly OcrEngine _ocrEngine;

    public OcrService()
    {
        _ocrEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"));
        if (_ocrEngine == null)
        {
            throw new InvalidOperationException("无法创建英文OCR引擎，请确认系统已安装英文语言包。");
        }
    }

    public async Task<List<OcrResult>> RecognizeEnglishTextAsync(ScreenCaptureInfo captureInfo)
    {
        var results = new List<OcrResult>();
        using var softwareBitmap = ConvertBitmapFast(captureInfo.OcrBitmap);
        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);

        foreach (var line in ocrResult.Lines)
        {
            var text = line.Text.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (!IsLikelyEnglish(text)) continue;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool hasWords = false;

            foreach (var word in line.Words)
            {
                hasWords = true;
                var wr = word.BoundingRect;
                if (wr.X < minX) minX = wr.X;
                if (wr.Y < minY) minY = wr.Y;
                if (wr.X + wr.Width > maxX) maxX = wr.X + wr.Width;
                if (wr.Y + wr.Height > maxY) maxY = wr.Y + wr.Height;
            }

            if (!hasWords) continue;

            double sf = captureInfo.ScaleFactor;

            results.Add(new OcrResult
            {
                Text = text,
                BoundingBox = new System.Drawing.Rectangle(
                    (int)(minX * sf),
                    (int)(minY * sf),
                    (int)((maxX - minX) * sf),
                    (int)((maxY - minY) * sf))
            });
        }

        return results;
    }

    private static bool IsLikelyEnglish(string text)
    {
        int englishChars = 0, totalChars = 0;
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                totalChars++;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                    englishChars++;
            }
        }
        if (totalChars == 0) return false;
        return (double)englishChars / totalChars >= 0.7;
    }

    private static SoftwareBitmap ConvertBitmapFast(System.Drawing.Bitmap bitmap)
    {
        int w = bitmap.Width, h = bitmap.Height;
        var rect = new System.Drawing.Rectangle(0, 0, w, h);
        var bmpData = bitmap.LockBits(rect,
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int stride = bmpData.Stride;
            int tightRowBytes = w * 4;
            byte[] pixelData = new byte[tightRowBytes * h];

            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(bmpData.Scan0 + y * stride, pixelData, y * tightRowBytes, tightRowBytes);
            }

            IBuffer buffer = pixelData.AsBuffer();
            return SoftwareBitmap.CreateCopyFromBuffer(buffer,
                BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }
}
