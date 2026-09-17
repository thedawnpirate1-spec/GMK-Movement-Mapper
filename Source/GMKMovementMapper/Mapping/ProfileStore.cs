using System.Text.Json;

namespace GMKMovementMapper.Mapping;

public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GMKRebuild", "Profiles");

    public ProfileStore()
    {
        Directory.CreateDirectory(DirectoryPath);
        if (GetNames().Count == 0)
        {
            var profile = MovementProfile.Load();
            profile.Name = "Default";
            Save(profile);
        }
    }

    public IReadOnlyList<string> GetNames() => Directory.EnumerateFiles(DirectoryPath, "*.json")
        .Select(Path.GetFileNameWithoutExtension).Where(x => !string.IsNullOrWhiteSpace(x))
        .Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public MovementProfile Load(string name)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<MovementProfile>(File.ReadAllText(PathFor(name)));
            if (profile is not null) { profile.Name = name; profile.Normalize(); return profile; }
        }
        catch { }
        return new MovementProfile { Name = name };
    }

    public void Save(MovementProfile profile)
    {
        profile.Normalize();
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(PathFor(profile.Name), JsonSerializer.Serialize(profile, JsonOptions));
        profile.Save();
    }

    public MovementProfile Create(string name, MovementProfile source)
    {
        var clean = CleanName(name);
        var json = JsonSerializer.Serialize(source);
        var copy = JsonSerializer.Deserialize<MovementProfile>(json) ?? new MovementProfile();
        copy.Name = clean;
        Save(copy);
        return copy;
    }

    public void Delete(string name) { var path = PathFor(name); if (File.Exists(path)) File.Delete(path); }
    private string PathFor(string name) => Path.Combine(DirectoryPath, CleanName(name) + ".json");
    private static string CleanName(string name)
    {
        var cleaned = string.Concat(name.Trim().Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        return string.IsNullOrWhiteSpace(cleaned) ? "Profile" : cleaned;
    }
}
