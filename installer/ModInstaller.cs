using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Installer;

internal static class ModInstaller
{
    private const string ModFolderName = "atproto-tracker";

    private static readonly string[] EmbeddedFiles =
        ["atproto-tracker.dll", "manifest.json", "config.example.json"];

    public static string GetModFolder(string modsPath) =>
        Path.Combine(modsPath, ModFolderName);

    /// <summary>Read the version from the embedded manifest.json resource.</summary>
    public static string? GetBundledVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
        if (resName is null) return null;
        using var stream = asm.GetManifestResourceStream(resName)!;
        try
        {
            var doc = JsonNode.Parse(stream);
            return doc?["version"]?.GetValue<string>();
        }
        catch { return null; }
    }

    /// <summary>Read the version from the installed manifest.json on disk.</summary>
    public static string? GetInstalledVersion(string modsPath)
    {
        var path = Path.Combine(GetModFolder(modsPath), "manifest.json");
        if (!File.Exists(path)) return null;
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(path));
            return doc?["version"]?.GetValue<string>();
        }
        catch { return null; }
    }

    /// <summary>Check if the mod is already installed and return existing config if so.</summary>
    public static JsonNode? ReadExistingConfig(string modsPath)
    {
        var configPath = Path.Combine(GetModFolder(modsPath), "config.json");
        if (!File.Exists(configPath)) return null;
        try { return JsonNode.Parse(File.ReadAllText(configPath)); }
        catch { return null; }
    }

    public static bool IsUpgrade(string modsPath) =>
        File.Exists(Path.Combine(GetModFolder(modsPath), "config.json"));

    /// <summary>Install or update the mod files + write config.</summary>
    public static void Install(string modsPath, string handle, string appPassword,
                               Action<string> onStatus)
    {
        var modDir = GetModFolder(modsPath);
        Directory.CreateDirectory(modDir);
        onStatus("Copying mod files…");

        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in EmbeddedFiles)
        {
            var resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            if (resName is null) throw new FileNotFoundException($"embedded resource {name} not found");
            using var src = asm.GetManifestResourceStream(resName)!;
            var dest = Path.Combine(modDir, name);
            using var fs = File.Create(dest);
            src.CopyTo(fs);
        }

        onStatus("Writing config…");
        WriteConfig(modDir, handle, appPassword);

        onStatus("Done!");
    }

    private static void WriteConfig(string modDir, string handle, string appPassword)
    {
        var configPath = Path.Combine(modDir, "config.json");
        JsonNode? existing = null;
        if (File.Exists(configPath))
        {
            try { existing = JsonNode.Parse(File.ReadAllText(configPath)); }
            catch { /* overwrite broken config */ }
        }

        // Preserve statsRkey from existing config on upgrade.
        var statsRkey = existing?["statsRkey"]?.GetValue<string>() ?? "";

        var config = new JsonObject
        {
            ["handle"]      = handle,
            ["appPassword"] = appPassword,
        };
        if (!string.IsNullOrEmpty(statsRkey))
            config["statsRkey"] = statsRkey;

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, config.ToJsonString(opts));
    }

    /// <summary>
    /// Copy progress.save from unmodded to modded profile if the modded dir
    /// doesn't exist yet. Returns true if a copy was made.
    /// </summary>
    public static bool MigrateSaveIfNeeded(Action<string> onStatus)
    {
        var root = SteamPathFinder.GetSaveDataRoot();
        if (root is null) return false;

        var steamDir = Path.Combine(root, "steam");
        if (!Directory.Exists(steamDir)) return false;

        bool copied = false;
        foreach (var idDir in Directory.GetDirectories(steamDir))
        {
            var unmodded = Path.Combine(idDir, "1", "profile1", "saves", "progress.save");
            var moddedDir = Path.Combine(idDir, "modded", "profile1", "saves");
            if (File.Exists(unmodded) && !Directory.Exists(moddedDir))
            {
                onStatus($"Copying save for {Path.GetFileName(idDir)}…");
                Directory.CreateDirectory(moddedDir);
                File.Copy(unmodded, Path.Combine(moddedDir, "progress.save"));
                copied = true;
            }
        }
        return copied;
    }

    /// <summary>Check if save migration is applicable (for UI toggle visibility).</summary>
    public static bool CanMigrateSave()
    {
        var root = SteamPathFinder.GetSaveDataRoot();
        if (root is null) return false;
        var steamDir = Path.Combine(root, "steam");
        if (!Directory.Exists(steamDir)) return false;
        foreach (var idDir in Directory.GetDirectories(steamDir))
        {
            var unmodded = Path.Combine(idDir, "1", "profile1", "saves", "progress.save");
            var moddedDir = Path.Combine(idDir, "modded", "profile1", "saves");
            if (File.Exists(unmodded) && !Directory.Exists(moddedDir))
                return true;
        }
        return false;
    }
}
