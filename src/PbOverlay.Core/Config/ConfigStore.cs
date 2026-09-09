using System.Text.Json;

namespace PbOverlay.Core.Config;

public static class ConfigStore
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PbOverlay",
        "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load()
    {
        if (!File.Exists(Path)) return new AppConfig();

        var bytes = File.ReadAllBytes(Path);
        var cfg = JsonSerializer.Deserialize<AppConfig>(bytes, JsonOptions)
                  ?? throw new InvalidOperationException("Config file exists but deserialized to null.");

        if (cfg.SchemaVersion > AppConfig.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Config schema version {cfg.SchemaVersion} is newer than this build supports ({AppConfig.CurrentSchemaVersion}). Update the app.");
        }

        Migrate(cfg);
        return cfg;
    }

    public static void Save(AppConfig cfg)
    {
        var dir = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(dir);
        cfg.SchemaVersion = AppConfig.CurrentSchemaVersion;

        var tmp = Path + ".tmp";
        File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(cfg, JsonOptions));
        File.Move(tmp, Path, overwrite: true);
    }

    private static void Migrate(AppConfig cfg)
    {
        switch (cfg.SchemaVersion)
        {
            case 1:
                // Current version — no-op. Future migrations chain here without a break.
                goto default;
            default:
                cfg.SchemaVersion = AppConfig.CurrentSchemaVersion;
                break;
        }
    }
}
