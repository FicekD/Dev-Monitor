using System.Text.Json;
using DevMonitor.Core;

namespace DevMonitor.App;

public sealed class UiState
{
    static readonly string FilePath = Path.Combine(AppPaths.DataDirectory, "ui-state.json");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public double? Width { get; set; }
    public double? Height { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Maximized { get; set; }
    public string Page { get; set; } = "overview";
    public Dictionary<string, string> Ranges { get; set; } = [];
    public HashSet<string> HiddenSeries { get; set; } = [];

    public static UiState Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<UiState>(File.ReadAllText(FilePath), JsonOptions) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save() => JsonFile.Write(FilePath, JsonSerializer.Serialize(this, JsonOptions));
}
