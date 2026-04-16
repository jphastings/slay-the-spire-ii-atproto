using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace AtprotoTracker;

[HarmonyPatch(typeof(NMainMenu), "_Ready")]
internal static class MainMenuBadge
{
    private static readonly Color OkColor      = new(0.45f, 0.90f, 0.55f, 0.95f); // green
    private static readonly Color CheckColor   = new(0.80f, 0.80f, 0.30f, 0.75f); // yellow
    private static readonly Color FailedColor  = new(0.85f, 0.30f, 0.30f, 0.90f); // red
    private static readonly Color StrikeColor  = new(0.85f, 0.30f, 0.30f, 1.00f); // red
    private static readonly Color UnconfigColor = new(0.70f, 0.70f, 0.70f, 0.55f); // grey

    [HarmonyPostfix]
    public static void Postfix(NMainMenu __instance)
    {
        try { Inject(__instance); }
        catch (Exception ex) { Log.Error("failed to inject main-menu badge", ex); }
    }

    private static void Inject(NMainMenu menu)
    {
        var btn = new Button
        {
            Name        = "AtprotoTrackerBadge",
            Text        = "@",
            Flat        = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode   = Control.FocusModeEnum.None,
        };
        btn.AddThemeFontSizeOverride("font_size", 28);
        btn.AnchorLeft = 0; btn.AnchorTop = 1; btn.AnchorRight = 0; btn.AnchorBottom = 1;
        btn.OffsetLeft = 12; btn.OffsetTop = -50;
        btn.OffsetRight = 60; btn.OffsetBottom = -8;

        // Red strike-through line overlaid on the @; hidden when status is Ok.
        var strike = new ColorRect
        {
            Name        = "Strike",
            Color       = StrikeColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible     = false,
        };
        strike.AnchorLeft = 0.15f; strike.AnchorRight = 0.85f;
        strike.AnchorTop  = 0.50f; strike.AnchorBottom = 0.50f;
        strike.OffsetTop  = -1.5f; strike.OffsetBottom = 1.5f;
        btn.AddChild(strike);

        btn.Pressed += () => ShowDialog(menu);
        menu.AddChild(btn);

        Apply(btn, strike);

        // Subscribe to auth state changes so the badge updates when auth finishes.
        Action handler = () => btn.CallDeferred(Control.MethodName.SetMeta, "refresh", 1);
        AuthState.Changed += handler;
        btn.TreeExiting += () => AuthState.Changed -= handler;

        // Poll via a timer — CallDeferred doesn't let us run arbitrary code, so drive
        // it from a SceneTreeTimer chain that re-applies state each tick.
        WireRefreshLoop(menu, btn, strike);
    }

    private static void WireRefreshLoop(NMainMenu menu, Button btn, ColorRect strike)
    {
        var tree = menu.GetTree();
        if (tree is null) return;
        void Tick()
        {
            if (!GodotObject.IsInstanceValid(btn)) return;
            Apply(btn, strike);
            if (AuthState.Status == AuthStatus.Checking)
            {
                var t = tree.CreateTimer(0.3);
                t.Timeout += Tick;
            }
        }
        Tick();
    }

    private static void Apply(Button btn, ColorRect strike)
    {
        var status = AuthState.Status;
        btn.Modulate = status switch
        {
            AuthStatus.Ok          => OkColor,
            AuthStatus.Checking    => CheckColor,
            AuthStatus.Failed      => FailedColor,
            _                      => UnconfigColor,
        };
        strike.Visible = status != AuthStatus.Ok && status != AuthStatus.Checking;
        btn.TooltipText = status switch
        {
            AuthStatus.Ok          => $"atproto-tracker: publishing as @{AuthState.Handle}",
            AuthStatus.Checking    => "atproto-tracker: checking credentials…",
            AuthStatus.Failed      => $"atproto-tracker: {AuthState.Error}",
            _                      => "atproto-tracker: not configured — check config.json",
        };
    }

    private static void ShowDialog(NMainMenu parent)
    {
        var status = AuthState.Status;
        string body = status switch
        {
            AuthStatus.Ok          => $"Publishing to your Atmosphere account:\n\n@{AuthState.Handle}",
            AuthStatus.Checking    => "Checking credentials with Slingshot…",
            AuthStatus.Failed      => $"Authentication failed:\n\n{AuthState.Error}\n\nFix config.json and restart the game.",
            _                      => $"Not yet configured.\n\n{AuthState.Error ?? "Edit config.json next to the mod DLL and restart the game."}",
        };
        var popup = NErrorPopup.Create("atproto-tracker", body, false);
        NModalContainer.Instance?.Add(popup!);
        NModalContainer.Instance?.ShowBackstop();
    }
}
