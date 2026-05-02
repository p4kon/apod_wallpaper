using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace apod_wallpaper.WinUI;

public sealed partial class SettingsPage : Page
{
    private static readonly apod_wallpaper.WallpaperStyle[] WallpaperStyleDisplayOrder =
    {
        apod_wallpaper.WallpaperStyle.Smart,
        apod_wallpaper.WallpaperStyle.Fill,
        apod_wallpaper.WallpaperStyle.Fit,
        apod_wallpaper.WallpaperStyle.Stretch,
        apod_wallpaper.WallpaperStyle.Tile,
        apod_wallpaper.WallpaperStyle.Center,
        apod_wallpaper.WallpaperStyle.Span,
    };

    private static readonly Brush ApiKeyValidBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0x3D, 0x8C, 0x63));
    private static readonly Brush ApiKeyInvalidBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0xC4, 0x5A, 0x5A));
    private static readonly Brush ApiKeyDemoBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0x2F, 0x79, 0xD9));

    private const string NasaApiKeyUrl = "https://api.nasa.gov/";

    private SettingsPageArguments? _arguments;
    private BackendHost? _backendHost;
    private apod_wallpaper.ApplicationSettingsSnapshot? _settingsSnapshot;
    private apod_wallpaper.ApiKeyValidationState _apiKeyValidationState;
    private bool _isHydratingControls;

    public SettingsPage()
    {
        InitializeComponent();
        WallpaperStyleComboBox.ItemsSource = WallpaperStyleDisplayOrder;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _arguments = e.Parameter as SettingsPageArguments;
        if (_arguments == null)
        {
            SetErrorState("Settings page did not receive backend composition arguments.");
            return;
        }

        _backendHost = _arguments.BackendHost;
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        if (_backendHost == null)
        {
            SetErrorState("Backend host is unavailable.");
            return;
        }

        SettingsStatusBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusBar.Title = "Loading settings";
        SettingsStatusBar.Message = "Reading persisted settings and current API key state through the backend facade.";

        var settingsResult = await _backendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
        {
            SetErrorState(settingsResult.Error?.Message ?? "Unable to load settings.");
            return;
        }

        _settingsSnapshot = settingsResult.Value;
        var apiKeyStateResult = await _backendHost.Backend.GetApiKeyValidationStateAsync();
        _apiKeyValidationState = apiKeyStateResult.Succeeded ? apiKeyStateResult.Value : apod_wallpaper.ApiKeyValidationState.Unknown;

        PopulateSettings(_settingsSnapshot, _apiKeyValidationState);

        SettingsStatusBar.Severity = InfoBarSeverity.Success;
        SettingsStatusBar.Title = "Settings ready";
        SettingsStatusBar.Message = "Changes save through the backend as soon as each control is committed.";
    }

    private void PopulateSettings(apod_wallpaper.ApplicationSettingsSnapshot settings, apod_wallpaper.ApiKeyValidationState apiKeyValidationState)
    {
        _isHydratingControls = true;
        try
        {
            ApiKeyTextBox.Text = settings.NasaApiKey ?? string.Empty;
            AutoCheckToggle.IsOn = settings.AutoRefreshEnabled;
            StartWithWindowsToggle.IsOn = settings.StartWithWindows;
            CloseToTrayToggle.IsOn = settings.MinimizeToTrayOnClose;
            ImagesDirectoryTextBox.Text = settings.ImagesDirectoryPath ?? string.Empty;
            WallpaperStyleComboBox.SelectedItem = ResolveWallpaperStyleFromSettings(settings);
        }
        finally
        {
            _isHydratingControls = false;
        }

        UpdateApiKeyStateBadge(settings, apiKeyValidationState);
        ImagesDirectoryHintText.Text = string.IsNullOrWhiteSpace(settings.ImagesDirectoryPath)
            ? "The backend will fall back to its default images directory until you choose a custom folder."
            : "Current folder: " + settings.ImagesDirectoryPath;
    }

    private static apod_wallpaper.WallpaperStyle ResolveWallpaperStyleFromSettings(apod_wallpaper.ApplicationSettingsSnapshot settings)
    {
        if (settings != null && Enum.IsDefined(typeof(apod_wallpaper.WallpaperStyle), settings.WallpaperStyleIndex))
            return (apod_wallpaper.WallpaperStyle)settings.WallpaperStyleIndex;

        return apod_wallpaper.WallpaperStyle.Smart;
    }

    private async void AutoCheckToggle_Toggled(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync(snapshot => snapshot.AutoRefreshEnabled = AutoCheckToggle.IsOn, "Auto-check preference saved.");
    }

    private async void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync(snapshot => snapshot.StartWithWindows = StartWithWindowsToggle.IsOn, "Start-with-Windows preference saved.");
    }

    private async void CloseToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAsync(
            snapshot => snapshot.MinimizeToTrayOnClose = CloseToTrayToggle.IsOn,
            CloseToTrayToggle.IsOn ? "Clicking X now hides the app to tray." : "Clicking X now exits the app.",
            afterSave: saved =>
            {
                _arguments?.UpdateCloseBehavior(saved.MinimizeToTrayOnClose);
                return Task.CompletedTask;
            });
    }

    private async void WallpaperStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingControls || WallpaperStyleComboBox.SelectedItem is not apod_wallpaper.WallpaperStyle selectedStyle)
            return;

        await SaveSettingsAsync(snapshot => snapshot.WallpaperStyleIndex = (int)selectedStyle, "Wallpaper style preference saved.");
    }

    private async void ApiKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await CommitApiKeyAsync();
    }

    private async void ApiKeyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        e.Handled = true;
        await CommitApiKeyAsync();
    }

    private async Task CommitApiKeyAsync()
    {
        await SaveSettingsAsync(
            snapshot => snapshot.NasaApiKey = ApiKeyTextBox.Text.Trim(),
            "API key saved through protected backend storage.",
            afterSave: async saved =>
            {
                var validationResult = await _backendHost!.Backend.GetApiKeyValidationStateAsync();
                _apiKeyValidationState = validationResult.Succeeded ? validationResult.Value : apod_wallpaper.ApiKeyValidationState.Unknown;
                UpdateApiKeyStateBadge(saved, _apiKeyValidationState);
            });
    }

    private async void ImagesDirectoryTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await CommitImagesDirectoryAsync(ImagesDirectoryTextBox.Text.Trim());
    }

    private async void ImagesDirectoryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        e.Handled = true;
        await CommitImagesDirectoryAsync(ImagesDirectoryTextBox.Text.Trim());
    }

    private async Task CommitImagesDirectoryAsync(string path)
    {
        await SaveSettingsAsync(
            snapshot => snapshot.ImagesDirectoryPath = path,
            "Images directory saved.",
            afterSave: saved =>
            {
                ImagesDirectoryHintText.Text = string.IsNullOrWhiteSpace(saved.ImagesDirectoryPath)
                    ? "The backend will fall back to its default images directory until you choose a custom folder."
                    : "Current folder: " + saved.ImagesDirectoryPath;
                return Task.CompletedTask;
            });
    }

    private async void BrowseImagesFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        if (app?.Host == null)
            return;

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        if (app.MainWindow == null)
            return;

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(app.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null)
            return;

        ImagesDirectoryTextBox.Text = folder.Path;
        await CommitImagesDirectoryAsync(folder.Path);
    }

    private async void OpenImagesFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var path = ImagesDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Warning;
            SettingsStatusBar.Title = "No images folder configured";
            SettingsStatusBar.Message = "Choose or enter an images folder first.";
            return;
        }

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            await Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception ex)
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Error;
            SettingsStatusBar.Title = "Unable to open folder";
            SettingsStatusBar.Message = ex.Message;
        }
    }

    private async void GetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(NasaApiKeyUrl));
    }

    private async Task SaveSettingsAsync(
        Action<apod_wallpaper.ApplicationSettingsSnapshot> update,
        string successMessage,
        Func<apod_wallpaper.ApplicationSettingsSnapshot, Task>? afterSave = null)
    {
        if (_isHydratingControls || _settingsSnapshot == null || _backendHost == null)
            return;

        var updatedSnapshot = _settingsSnapshot.Clone();
        update(updatedSnapshot);

        SettingsStatusBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusBar.Title = "Saving settings";
        SettingsStatusBar.Message = "Persisting changes through the backend facade.";

        var stopwatch = Stopwatch.StartNew();
        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        stopwatch.Stop();
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Error;
            SettingsStatusBar.Title = "Settings were not saved";
            SettingsStatusBar.Message = saveResult.Error?.Message ?? "Unknown backend error while saving settings.";
            if (_settingsSnapshot != null)
                PopulateSettings(_settingsSnapshot, _apiKeyValidationState);
            return;
        }

        _settingsSnapshot = saveResult.Value;
        PopulateSettings(_settingsSnapshot, _apiKeyValidationState);

        if (afterSave != null)
            await afterSave(_settingsSnapshot);

        SettingsStatusBar.Severity = InfoBarSeverity.Success;
        SettingsStatusBar.Title = "Settings saved";
        SettingsStatusBar.Message = string.Format(CultureInfo.InvariantCulture, "{0} ({1} ms)", successMessage, stopwatch.ElapsedMilliseconds);
    }

    private void UpdateApiKeyStateBadge(apod_wallpaper.ApplicationSettingsSnapshot settings, apod_wallpaper.ApiKeyValidationState validationState)
    {
        var rawKey = settings.NasaApiKey?.Trim();
        var usesDemoKey = string.IsNullOrWhiteSpace(rawKey) || string.Equals(rawKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase);

        if (!usesDemoKey && validationState == apod_wallpaper.ApiKeyValidationState.Valid)
        {
            ApiKeyStateBadge.Background = ApiKeyValidBrush;
            ApiKeyStateText.Text = "Valid personal key";
            return;
        }

        if (!usesDemoKey && validationState == apod_wallpaper.ApiKeyValidationState.Invalid)
        {
            ApiKeyStateBadge.Background = ApiKeyInvalidBrush;
            ApiKeyStateText.Text = "Invalid key, DEMO_KEY fallback active";
            return;
        }

        ApiKeyStateBadge.Background = ApiKeyDemoBrush;
        ApiKeyStateText.Text = "DEMO_KEY / no personal key";
    }

    private void SetErrorState(string message)
    {
        SettingsStatusBar.Severity = InfoBarSeverity.Error;
        SettingsStatusBar.Title = "Settings unavailable";
        SettingsStatusBar.Message = message;
        ApiKeyTextBox.IsEnabled = false;
        AutoCheckToggle.IsEnabled = false;
        StartWithWindowsToggle.IsEnabled = false;
        CloseToTrayToggle.IsEnabled = false;
        ImagesDirectoryTextBox.IsEnabled = false;
        BrowseImagesFolderButton.IsEnabled = false;
        OpenImagesFolderButton.IsEnabled = false;
        WallpaperStyleComboBox.IsEnabled = false;
        GetApiKeyButton.IsEnabled = false;
    }
}
