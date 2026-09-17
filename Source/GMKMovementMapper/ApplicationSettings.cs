using System.Text.Json;

namespace GMKMovementMapper;

public sealed class ApplicationSettings
{
    public string LastProfileName { get; set; } = "Default";
    public bool AutoStartMovement { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GMKRebuild", "app-settings.json");

    public static ApplicationSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(FilePath)) ?? new ApplicationSettings();
        }
        catch { }
        return new ApplicationSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
