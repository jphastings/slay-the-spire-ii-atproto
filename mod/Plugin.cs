using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AtprotoTracker;

[ModInitializer("ModLoaded")]
public static class Plugin
{
    public const string ModId = "atproto-tracker";
    public const string ModVersion = "0.1.0";

    internal static Config Config = null!;
    internal static AtProtoClient AtProto = null!;

    public static void ModLoaded()
    {
        Config = Config.LoadOrCreate();
        AtProto = new AtProtoClient(Config);
        new Harmony(ModId).PatchAll();
        Log.Info($"{ModId} {ModVersion} loaded (handle={Config.Handle})");
    }
}
