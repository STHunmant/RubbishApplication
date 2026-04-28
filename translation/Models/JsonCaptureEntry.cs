using System.Text.Json.Serialization;

namespace App1.Services;

public class JsonCaptureEntry
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("translated")]
    public string? Translated { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonIgnore]
    public string? TextHash { get; set; }
}
