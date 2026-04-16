using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AtprotoTracker.Installer;

public partial class MainWindow : Window
{
    private const string SteamUri = "steam://rungameid/2868840";

    private bool _isUpgrade;
    private bool _isPlayMode;
    private bool _passwordVisible;
    private MiniDoc? _resolvedIdentity;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyLocale();

        // Auto-detect game path.
        var gamePath = SteamPathFinder.FindGamePath();
        if (gamePath is not null)
            GamePathBox.Text = gamePath;

        // Check for existing install (upgrade or up-to-date).
        TryLoadExistingConfig();

        // Show/hide save migration.
        MigrateSaveCheck.IsVisible = !_isUpgrade && !_isPlayMode && ModInstaller.CanMigrateSave();
    }

    private void ApplyLocale()
    {
        Title = Strings.Get("installer_title");
        SubtitleText.Text = Strings.Get("installer_subtitle");
        SectionGameInstall.Text = Strings.Get("section_game_install");
        SectionAtmosphere.Text = Strings.Get("section_atmosphere");
        SectionOptions.Text = Strings.Get("section_options");
        GamePathBox.Watermark = Strings.Get("watermark_game_path");
        HandleBox.Watermark = Strings.Get("watermark_handle");
        PasswordBox.Watermark = Strings.Get("watermark_password");
        BrowseBtn.Content = Strings.Get("btn_browse");
        VerifyBtn.Content = Strings.Get("btn_verify");
        InstallBtn.Content = Strings.Get("btn_install");
        MigrateSaveCheck.Content = Strings.Get("checkbox_migrate_save");
        ToolTip.SetTip(TogglePasswordBtn, Strings.Get("tooltip_toggle_password"));
        StatusText.Text = Strings.Get("status_ready");
    }

    private void TryLoadExistingConfig()
    {
        var gamePath = GamePathBox.Text;
        if (string.IsNullOrWhiteSpace(gamePath)) return;

        var modsPath = SteamPathFinder.GetModsPath(gamePath);
        var existing = ModInstaller.ReadExistingConfig(modsPath);
        if (existing is null) return;

        _isUpgrade = true;
        HandleBox.Text = existing["handle"]?.GetValue<string>() ?? "";
        PasswordBox.Text = existing["appPassword"]?.GetValue<string>() ?? "";

        var installed = ModInstaller.GetInstalledVersion(modsPath);
        var bundled = ModInstaller.GetBundledVersion();
        if (installed is not null && installed == bundled)
        {
            ShowPlayMode(installed);
        }
        else
        {
            InstallBtn.Content = Strings.Get("btn_update");
            StatusText.Text = Strings.Get("status_existing_install");
        }
    }

    private void ShowPlayMode(string version)
    {
        _isPlayMode = true;
        InstallBtn.Content = Strings.Get("btn_play");
        MigrateSaveCheck.IsVisible = false;
        SetStatus(Strings.Get("status_up_to_date", version));
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.Get("dialog_select_folder"),
            AllowMultiple = false,
        });
        if (folders.Count > 0)
        {
            GamePathBox.Text = folders[0].Path.LocalPath;
            _isUpgrade = false;
            _isPlayMode = false;
            TryLoadExistingConfig();
            MigrateSaveCheck.IsVisible = !_isUpgrade && !_isPlayMode && ModInstaller.CanMigrateSave();
        }
    }

    private void TogglePasswordClicked(object? sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordBox.PasswordChar = _passwordVisible ? '\0' : '●';
        TogglePasswordBtn.Content = _passwordVisible ? "🔒" : "👁";
    }

    private async void VerifyClicked(object? sender, RoutedEventArgs e)
    {
        var handle = HandleBox.Text?.Trim();
        var password = PasswordBox.Text?.Trim();
        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            VerifyStatus.Text = Strings.Get("error_enter_handle_password");
            VerifyStatus.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        VerifyStatus.Text = Strings.Get("status_verifying");
        VerifyStatus.Foreground = Avalonia.Media.Brushes.Gray;
        try
        {
            var mini = await IdentityResolver.ResolveAsync(handle);
            await IdentityResolver.ValidateCredentialsAsync(mini, password);
            _resolvedIdentity = mini;
            VerifyStatus.Text = Strings.Get("status_resolved", mini.Handle);
            VerifyStatus.Foreground = Avalonia.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            _resolvedIdentity = null;
            VerifyStatus.Text = $"✗ {ex.Message}";
            VerifyStatus.Foreground = Avalonia.Media.Brushes.OrangeRed;
        }
    }

    private async void InstallClicked(object? sender, RoutedEventArgs e)
    {
        if (_isPlayMode)
        {
            LaunchGameAndQuit();
            return;
        }

        var gamePath = GamePathBox.Text?.Trim();
        var handle = HandleBox.Text?.Trim();
        var password = PasswordBox.Text?.Trim();

        if (string.IsNullOrEmpty(gamePath) || !System.IO.Directory.Exists(gamePath))
        {
            SetStatus(Strings.Get("error_game_path_invalid"), error: true);
            return;
        }
        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            SetStatus(Strings.Get("error_enter_credentials"), error: true);
            return;
        }

        InstallBtn.IsEnabled = false;
        try
        {
            // Verify credentials if not already done.
            if (_resolvedIdentity is null)
            {
                SetStatus(Strings.Get("status_verifying_credentials"));
                var mini = await IdentityResolver.ResolveAsync(handle);
                await IdentityResolver.ValidateCredentialsAsync(mini, password);
                _resolvedIdentity = mini;
                VerifyStatus.Text = Strings.Get("status_resolved", mini.Handle);
                VerifyStatus.Foreground = Avalonia.Media.Brushes.Green;
            }

            var modsPath = SteamPathFinder.GetModsPath(gamePath);
            ModInstaller.Install(modsPath, handle, password, s => SetStatus(s));

            if (MigrateSaveCheck.IsVisible && MigrateSaveCheck.IsChecked == true)
                ModInstaller.MigrateSaveIfNeeded(s => SetStatus(s));

            var version = ModInstaller.GetBundledVersion() ?? "?";
            ShowPlayMode(version);
        }
        catch (Exception ex)
        {
            SetStatus(Strings.Get("error_prefix", ex.Message), error: true);
        }
        finally
        {
            InstallBtn.IsEnabled = true;
        }
    }

    private void LaunchGameAndQuit()
    {
        Process.Start(new ProcessStartInfo(SteamUri) { UseShellExecute = true });
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void SetStatus(string text, bool error = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = text;
            StatusText.Foreground = error
                ? Avalonia.Media.Brushes.OrangeRed
                : Avalonia.Media.Brushes.Gray;
        });
    }
}
