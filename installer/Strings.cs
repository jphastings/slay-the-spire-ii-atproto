using System.Globalization;
using System.Resources;

namespace AtprotoTracker.Installer;

internal static class Strings
{
    private static readonly ResourceManager Rm =
        new("AtprotoTracker.Installer.Strings", typeof(Strings).Assembly);

    public static string Get(string key, params object?[] args)
    {
        var value = Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, value, args) : value;
    }
}
