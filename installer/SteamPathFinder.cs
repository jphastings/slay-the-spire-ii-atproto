using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AtprotoTracker.Installer;

internal static class SteamPathFinder
{
    private const string GameFolder = "Slay the Spire 2";

    /// <summary>Try to find the StS2 install directory. Returns null if not found.</summary>
    public static string? FindGamePath()
    {
        foreach (var lib in GetSteamLibraryFolders())
        {
            var candidate = Path.Combine(lib, "steamapps", "common", GameFolder);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Get the mods folder for the detected game path.</summary>
    public static string GetModsPath(string gamePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var app = Directory.GetDirectories(gamePath, "*.app").FirstOrDefault()
                      ?? Path.Combine(gamePath, "SlayTheSpire2.app");
            return Path.Combine(app, "Contents", "MacOS", "mods");
        }
        return Path.Combine(gamePath, "mods");
    }

    /// <summary>Get the user save data root (contains steam/&lt;id&gt;/ dirs).</summary>
    public static string? GetSaveDataRoot()
    {
        string dir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "SlayTheSpire2");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlayTheSpire2");
        else
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "SlayTheSpire2");
        return Directory.Exists(dir) ? dir : null;
    }

    private static List<string> GetSteamLibraryFolders()
    {
        var folders = new List<string>();
        var steamRoot = GetSteamRoot();
        if (steamRoot is not null) folders.Add(steamRoot);

        // Parse libraryfolders.vdf for additional library paths.
        var vdf = steamRoot is not null
            ? Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf")
            : null;
        if (vdf is not null && File.Exists(vdf))
        {
            try
            {
                var text = File.ReadAllText(vdf);
                foreach (Match m in Regex.Matches(text, @"""path""\s+""([^""]+)"""))
                {
                    var p = m.Groups[1].Value;
                    if (Directory.Exists(p) && !folders.Contains(p))
                        folders.Add(p);
                }
            }
            catch { /* best-effort */ }
        }
        return folders;
    }

    private static string? GetSteamRoot()
    {
        string candidate;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            candidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Steam");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            candidate = @"C:\Program Files (x86)\Steam";
        else
            candidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".steam", "steam");
        return Directory.Exists(candidate) ? candidate : null;
    }
}
