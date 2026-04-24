using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AtprotoTracker;

[ModInitializer("ModLoaded")]
public static class Plugin
{
    public const string ModId = "atproto-tracker";
    public static readonly string ModVersion =
        typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    internal static Config Config = null!;
    internal static AtProtoClient AtProto = null!;

    public static void ModLoaded()
    {
        Strings.Init();
        Config = Config.LoadOrCreate();
        AtProto = new AtProtoClient();
        PreloadLinuxUnwinder();
        new Harmony(ModId).PatchAll();
        Log.Info($"{ModId} {ModVersion} loaded");
        if (!Signing.ModSigningKey.Available)
            Log.Warn("no signing key embedded — run records will be published unsigned. CI builds contain the production key.");

        _ = Task.Run(AuthenticateAsync);
    }

    // HarmonyX's native detour helper (mm-exhelper.so) references
    // _Unwind_RaiseException from libgcc_s. Inside sandboxed Linux runtimes
    // like Steam Deck's Steam Runtime "sniper" container, libgcc_s isn't
    // loaded into the process yet, so dlopen fails and PatchAll throws.
    // Must use RTLD_GLOBAL so the symbols are visible to Harmony's later
    // dlopen of mm-exhelper.so — NativeLibrary.TryLoad uses RTLD_LOCAL.
    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 0x100;

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenLibdl(string path, int flags);

    [DllImport("libc.so.6", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenLibc(string path, int flags);

    private static void PreloadLinuxUnwinder()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        foreach (var name in new[] { "libgcc_s.so.1", "libgcc_s.so" })
        {
            var h = TryDlopenGlobal(name);
            if (h != IntPtr.Zero)
            {
                Log.Info($"preloaded {name} (RTLD_GLOBAL) for Harmony unwinder");
                return;
            }
        }
        Log.Warn("couldn't preload libgcc_s — Harmony patching may fail");
    }

    private static IntPtr TryDlopenGlobal(string name)
    {
        const int flags = RTLD_NOW | RTLD_GLOBAL;
        try { return DlopenLibdl(name, flags); }
        catch (DllNotFoundException) { }
        try { return DlopenLibc(name, flags); }
        catch (DllNotFoundException) { }
        return IntPtr.Zero;
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
