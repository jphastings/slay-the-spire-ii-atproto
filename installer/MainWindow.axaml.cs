using System;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AtprotoTracker.Installer;

public partial class MainWindow : Window
{
    private bool _isUpgrade;
    private bool _passwordVisible;
    private MiniDoc? _resolvedIdentity;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Auto-detect game path.
        var gamePath = SteamPathFinder.FindGamePath();
        if (gamePath is not null)
            GamePathBox.Text = gamePath;

        // Check for existing install (upgrade).
        TryLoadExistingConfig();

        // Show/hide save migration.
        MigrateSaveCheck.IsVisible = !_isUpgrade && ModInstaller.CanMigrateSave();
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
        InstallBtn.Content = "Update";
        StatusText.Text = "Existing install detected — update to latest version.";
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Slay the Spire 2 install folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0)
        {
            GamePathBox.Text = folders[0].Path.LocalPath;
            _isUpgrade = false;
            TryLoadExistingConfig();
            MigrateSaveCheck.IsVisible = !_isUpgrade && ModInstaller.CanMigrateSave();
        }
    }

    private void TogglePasswordClicked(object? sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordBox.PasswordChar = _passwordVisible ? '\0' : '●';
        TogglePasswordBtn.Content = _passwordVisible ? "Hide" : "Show";
    }

    private async void VerifyClicked(object? sender, RoutedEventArgs e)
    {
        var handle = HandleBox.Text?.Trim();
        var password = PasswordBox.Text?.Trim();
        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            VerifyStatus.Text = "Enter handle and password first.";
            VerifyStatus.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        VerifyStatus.Text = "Verifying…";
        VerifyStatus.Foreground = Avalonia.Media.Brushes.Gray;
        try
        {
            var mini = await IdentityResolver.ResolveAsync(handle);
            await IdentityResolver.ValidateCredentialsAsync(mini, password);
            _resolvedIdentity = mini;
            VerifyStatus.Text = $"✓ Resolved as @{mini.Handle}";
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
        var gamePath = GamePathBox.Text?.Trim();
        var handle = HandleBox.Text?.Trim();
        var password = PasswordBox.Text?.Trim();

        if (string.IsNullOrEmpty(gamePath) || !System.IO.Directory.Exists(gamePath))
        {
            SetStatus("Game install path is missing or invalid.", error: true);
            return;
        }
        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            SetStatus("Enter handle and app password first.", error: true);
            return;
        }

        InstallBtn.IsEnabled = false;
        try
        {
            // Verify credentials if not already done.
            if (_resolvedIdentity is null)
            {
                SetStatus("Verifying credentials…");
                var mini = await IdentityResolver.ResolveAsync(handle);
                await IdentityResolver.ValidateCredentialsAsync(mini, password);
                _resolvedIdentity = mini;
                VerifyStatus.Text = $"✓ Resolved as @{mini.Handle}";
                VerifyStatus.Foreground = Avalonia.Media.Brushes.Green;
            }

            var modsPath = SteamPathFinder.GetModsPath(gamePath);
            ModInstaller.Install(modsPath, handle, password, s => SetStatus(s));

            if (MigrateSaveCheck.IsVisible && MigrateSaveCheck.IsChecked == true)
                ModInstaller.MigrateSaveIfNeeded(s => SetStatus(s));

            SetStatus("Done! Launch Slay the Spire 2 to start tracking runs.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", error: true);
        }
        finally
        {
            InstallBtn.IsEnabled = true;
        }
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
