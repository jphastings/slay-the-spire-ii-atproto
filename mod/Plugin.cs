using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AtprotoTracker;

[ModInitializer("ModLoaded")]
public static class Plugin
{
    public const string ModId = "atproto-tracker";
    public const string ModVersion = "0.2.0";

    internal static Config Config = null!;
    internal static AtProtoClient AtProto = null!;

    public static void ModLoaded()
    {
        Config = Config.LoadOrCreate();
        AtProto = new AtProtoClient();
        new Harmony(ModId).PatchAll();
        Log.Info($"{ModId} {ModVersion} loaded");

        _ = Task.Run(AuthenticateAsync);
    }

    private static async Task AuthenticateAsync()
    {
        var cfg = Config;
        if (string.IsNullOrWhiteSpace(cfg.Handle) || string.IsNullOrWhiteSpace(cfg.AppPassword))
        {
            AuthState.Set(AuthStatus.Unconfigured, error: "handle or appPassword is empty — edit config.json and restart");
            return;
        }

        AuthState.Set(AuthStatus.Checking);
        try
        {
            var mini = await IdentityResolver.ResolveAsync(cfg.Handle);
            await AtProto.LoginAsync(mini.Pds, mini.Did, cfg.AppPassword);
            AuthState.Set(AuthStatus.Ok, handle: mini.Handle, did: mini.Did);
            Log.Info($"authenticated as @{mini.Handle} ({mini.Did}) on {mini.Pds}");
        }
        catch (Exception ex)
        {
            AuthState.Set(AuthStatus.Failed, error: ex.Message);
            Log.Error("authentication failed", ex);
        }
    }
}
