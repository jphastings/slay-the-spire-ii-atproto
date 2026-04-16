using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;
using Godot;

namespace AtprotoTracker;

internal static class Strings
{
    private static readonly ResourceManager Rm =
        new("AtprotoTracker.Strings", typeof(Strings).Assembly);

    private static int _initialized;

    // Godot locale codes that don't map 1:1 to .NET culture names.
    private static readonly Dictionary<string, string> LocaleMap = new()
    {
        ["pt_BR"] = "pt-BR",
        ["zh_CN"] = "zh-Hans",
        ["zh_TW"] = "zh-Hans",
        ["zh_HK"] = "zh-Hans",
        ["zh"]    = "zh-Hans",
    };

    /// <summary>
    /// Registers an <see cref="AppDomain.AssemblyResolve"/> handler so the runtime
    /// can find satellite resource assemblies next to the mod DLL, even when Godot's
    /// assembly loader doesn't probe the standard culture subdirectories.
    /// </summary>
    internal static void Init()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var name = new AssemblyName(args.Name);
            if (name.Name?.EndsWith(".resources") != true)
                return null;

            var modDir = Path.GetDirectoryName(typeof(Strings).Assembly.Location)!;
            var culture = name.CultureInfo?.Name ?? "";
            if (string.IsNullOrEmpty(culture))
                return null;

            var path = Path.Combine(modDir, culture, name.Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }

    public static string Get(string key, params object?[] args)
    {
        var culture = GetCurrentCulture();
        var value = Rm.GetString(key, culture) ?? key;
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, value, args) : value;
    }

    private static CultureInfo GetCurrentCulture()
    {
        try
        {
            var locale = TranslationServer.GetLocale();
            if (LocaleMap.TryGetValue(locale, out var mapped))
                locale = mapped;
            else
                locale = locale.Replace('_', '-');
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
