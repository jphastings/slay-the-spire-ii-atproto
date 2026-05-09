using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Installer;

internal static class ModInstaller
{
    private const string ModFolderName = "atproto-tracker";
    private const string Sts2SteamAppId = "2868840";
    private static readonly string[] ProfileDirs = ["profile1", "profile2", "profile3"];

    private static readonly string[] EmbeddedFiles =
        ["atproto-tracker.dll", "manifest.json", "config.json.example"];

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
    /// Copy unmodded profile dirs into modded/ for each Steam user, but only
    /// when modded/ contains none of profile1/profile2/profile3. Returns true
    /// if anything was copied.
    /// </summary>
    public static bool MigrateSaveIfNeeded(Action<string> onStatus)
    {
        var userdataRoot = SteamPathFinder.GetSaveDataRoot();
        if (userdataRoot is null) return false;

        bool copied = false;
        foreach (var (idDir, remote, moddedDir) in EnumerateCandidates(userdataRoot))
        {
            foreach (var profile in ProfileDirs)
            {
                var src = Path.Combine(remote, profile);
                if (!Directory.Exists(src)) continue;
                onStatus($"Copying {profile} for {Path.GetFileName(idDir)}…");
                CopyDirectory(src, Path.Combine(moddedDir, profile));
                copied = true;
            }
        }
        return copied;
    }

    /// <summary>Check if save migration is applicable (for UI toggle visibility).</summary>
    public static bool CanMigrateSave()
    {
        var userdataRoot = SteamPathFinder.GetSaveDataRoot();
        if (userdataRoot is null) return false;
        foreach (var (_, remote, _) in EnumerateCandidates(userdataRoot))
        {
            if (ProfileDirs.Any(p => Directory.Exists(Path.Combine(remote, p))))
                return true;
        }
        return false;
    }

    // Yields (idDir, remote, moddedDir) for each Steam ID whose modded/
    // contains none of profile1/profile2/profile3 yet.
    private static IEnumerable<(string idDir, string remote, string moddedDir)>
        EnumerateCandidates(string userdataRoot)
    {
        foreach (var idDir in Directory.GetDirectories(userdataRoot))
        {
            var remote = Path.Combine(idDir, Sts2SteamAppId, "remote");
            if (!Directory.Exists(remote)) continue;
            var moddedDir = Path.Combine(remote, "modded");
            if (ProfileDirs.Any(p => Directory.Exists(Path.Combine(moddedDir, p))))
                continue;
            yield return (idDir, remote, moddedDir);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)));
        foreach (var sub in Directory.GetDirectories(src))
            CopyDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
    }
}
