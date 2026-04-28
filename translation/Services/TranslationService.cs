using System.Net.Http;
using System.Text.Json;

namespace App1.Services;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _cache = new();

    public TranslationService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<string> TranslateEnglishToChineseAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (_cache.TryGetValue(text, out var cached))
            return cached;

        try
        {
            var encodedText = Uri.EscapeDataString(text);
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=zh-CN&dt=t&q={encodedText}";

            var response = await _httpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            var translations = root[0];

            var result = new System.Text.StringBuilder();
            foreach (var item in translations.EnumerateArray())
            {
                var translated = item[0].GetString();
                if (!string.IsNullOrEmpty(translated))
                {
                    result.Append(translated);
                }
            }

            var finalResult = result.ToString();
            if (!string.IsNullOrEmpty(finalResult))
            {
                _cache[text] = finalResult;
                return finalResult;
            }
        }
        catch
        {
        }

        return text;
    }
}
