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
    private static readonly Brush ApiKeyValidBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0x3D, 0x8C, 0x63));
    private static readonly Brush ApiKeyInvalidBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0xC4, 0x5A, 0x5A));
    private static readonly Brush ApiKeyDemoBrush = new SolidColorBrush(ColorHelper.FromArgb(0x33, 0x2F, 0x79, 0xD9));
    private static readonly Brush SelectedWallpaperStyleBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x78, 0xD4));
    private static readonly Brush SelectedWallpaperStyleForegroundBrush = new SolidColorBrush(Colors.White);

    private const string NasaApiKeyUrl = "https://api.nasa.gov/";

    private SettingsPageArguments? _arguments;
    private BackendHost? _backendHost;
    private apod_wallpaper.ApplicationSettingsSnapshot? _settingsSnapshot;
    private apod_wallpaper.ApiKeyValidationState _apiKeyValidationState;
    private string _effectiveImagesDirectory = string.Empty;
    private bool _isHydratingControls;

    public SettingsPage()
    {
        InitializeComponent();
        LocalizationHelper.ApplyTo(this);
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _arguments = e.Parameter as SettingsPageArguments;
        if (_arguments == null)
        {
            SetErrorState(AppStrings.Get("Settings page did not receive backend composition arguments."));
            return;
        }

        _backendHost = _arguments.BackendHost;
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        if (_backendHost == null)
        {
            SetErrorState(AppStrings.Get("Backend host is unavailable."));
            return;
        }

        SettingsStatusBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusBar.Title = AppStrings.Get("Loading settings");
        SettingsStatusBar.Message = AppStrings.Get("Reading persisted settings and current API key state through the backend facade.");

        var settingsResult = await _backendHost.Backend.GetSettingsAsync();
        if (!settingsResult.Succeeded || settingsResult.Value == null)
        {
            SetErrorState(AppStrings.GetBackendMessageOrDefault(settingsResult.Error?.Message, "Unable to load settings."));
            return;
        }

        _settingsSnapshot = settingsResult.Value;
        var apiKeyStateResult = await _backendHost.Backend.GetApiKeyValidationStateAsync();
        _apiKeyValidationState = apiKeyStateResult.Succeeded ? apiKeyStateResult.Value : apod_wallpaper.ApiKeyValidationState.Unknown;
        _effectiveImagesDirectory = await ResolveEffectiveImagesDirectoryAsync();

        PopulateSettings(_settingsSnapshot, _apiKeyValidationState);

        SettingsStatusBar.Severity = InfoBarSeverity.Success;
        SettingsStatusBar.Title = AppStrings.Get("Settings ready");
        SettingsStatusBar.Message = AppStrings.Get("Changes save through the backend as soon as each control is committed.");
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
            ImagesDirectoryTextBox.Text = ResolveDisplayedImagesDirectory(settings);
            SetWallpaperStyleButtons(ResolveWallpaperStyleFromSettings(settings));
            CloseBehaviorComboBox.SelectedIndex = settings.MinimizeToTrayOnClose ? 0 : 1;
        }
        finally
        {
            _isHydratingControls = false;
        }

        UpdateApiKeyStateBadge(settings, apiKeyValidationState);
        UpdateImagesDirectoryHint(settings);
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

    private async void CloseBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isHydratingControls || CloseBehaviorComboBox.SelectedIndex < 0)
            return;

        var minimizeToTrayOnClose = CloseBehaviorComboBox.SelectedIndex == 0;
        CloseToTrayToggle.IsOn = minimizeToTrayOnClose;
        await SaveSettingsAsync(
            snapshot => snapshot.MinimizeToTrayOnClose = minimizeToTrayOnClose,
            minimizeToTrayOnClose ? "Clicking X now hides the app to tray." : "Clicking X now exits the app.",
            afterSave: saved =>
            {
                _arguments?.UpdateCloseBehavior(saved.MinimizeToTrayOnClose);
                return Task.CompletedTask;
            });
    }

    private async void WallpaperStyleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isHydratingControls || sender is not Button button)
            return;

        if (!Enum.TryParse<apod_wallpaper.WallpaperStyle>(button.Tag?.ToString(), out var selectedStyle))
            return;

        if (_settingsSnapshot != null && ResolveWallpaperStyleFromSettings(_settingsSnapshot) == selectedStyle)
        {
            SetWallpaperStyleButtons(selectedStyle);
            return;
        }

        await SaveWallpaperStyleAsync(selectedStyle);
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
        var normalizedPath = path.Trim();
        await SaveSettingsAsync(
            snapshot => snapshot.ImagesDirectoryPath = ShouldUseDefaultImagesDirectory(normalizedPath)
                ? string.Empty
                : normalizedPath,
            "Images directory saved.",
            afterSave: async saved =>
            {
                _effectiveImagesDirectory = await ResolveEffectiveImagesDirectoryAsync();
                ImagesDirectoryTextBox.Text = ResolveDisplayedImagesDirectory(saved);
                UpdateImagesDirectoryHint(saved);
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
        var path = ResolveImagesDirectoryToOpen();
        if (string.IsNullOrWhiteSpace(path))
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Warning;
            SettingsStatusBar.Title = AppStrings.Get("No images folder configured");
            SettingsStatusBar.Message = AppStrings.Get("Choose or enter an images folder first.");
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            var opened = await Launcher.LaunchFolderAsync(folder);
            if (!opened)
                throw new InvalidOperationException(AppStrings.Get("Windows did not open the images folder."));
        }
        catch (Exception ex)
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Error;
            SettingsStatusBar.Title = AppStrings.Get("Unable to open folder");
            SettingsStatusBar.Message = ex.Message;
        }
    }

    private async Task<string> ResolveEffectiveImagesDirectoryAsync()
    {
        if (_backendHost == null)
            return string.Empty;

        var ensureResult = await _backendHost.Backend.EnsureEffectiveImagesDirectoryAsync();
        if (ensureResult.Succeeded && !string.IsNullOrWhiteSpace(ensureResult.Value))
            return ensureResult.Value;

        var getResult = await _backendHost.Backend.GetEffectiveImagesDirectoryAsync();
        return getResult.Succeeded && !string.IsNullOrWhiteSpace(getResult.Value)
            ? getResult.Value
            : string.Empty;
    }

    private string ResolveDisplayedImagesDirectory(apod_wallpaper.ApplicationSettingsSnapshot settings)
    {
        return string.IsNullOrWhiteSpace(settings.ImagesDirectoryPath)
            ? _effectiveImagesDirectory
            : settings.ImagesDirectoryPath;
    }

    private string ResolveImagesDirectoryToOpen()
    {
        var textBoxPath = ImagesDirectoryTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(textBoxPath))
            return textBoxPath;

        return _effectiveImagesDirectory;
    }

    private bool ShouldUseDefaultImagesDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        if (string.IsNullOrWhiteSpace(_effectiveImagesDirectory))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(_effectiveImagesDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateImagesDirectoryHint(apod_wallpaper.ApplicationSettingsSnapshot settings)
    {
        ImagesDirectoryHintText.Text = string.IsNullOrWhiteSpace(settings.ImagesDirectoryPath)
            ? AppStrings.Format("Default portable folder: {0}", _effectiveImagesDirectory)
            : AppStrings.Format("Custom folder: {0}", settings.ImagesDirectoryPath);
    }

    private async void GetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(NasaApiKeyUrl));
    }

    private async void ConfigureApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowApiKeyDialogAsync();
    }

    private async Task ShowApiKeyDialogAsync()
    {
        if (_backendHost == null)
            return;

        var textBox = new TextBox
        {
            PlaceholderText = AppStrings.Get("Paste NASA API key"),
            Text = ApiKeyTextBox.Text,
        };

        var linkButton = new Button
        {
            Content = AppStrings.Get("Get NASA API key"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        linkButton.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri(NasaApiKeyUrl));

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("A personal NASA API key unlocks richer calendar warmup and avoids DEMO_KEY rate limits."),
            TextWrapping = TextWrapping.Wrap,
        });
        stack.Children.Add(linkButton);
        stack.Children.Add(textBox);
        stack.Children.Add(new TextBlock
        {
            Text = AppStrings.Get("If the NASA page does not open in your region, VPN may be required."),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = AppStrings.Get("NASA API Key"),
            PrimaryButtonText = AppStrings.Get("Save"),
            CloseButtonText = AppStrings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        ApiKeyTextBox.Text = textBox.Text.Trim();
        await CommitApiKeyAsync();
    }

    private async Task SaveSettingsAsync(
        Action<apod_wallpaper.ApplicationSettingsSnapshot> update,
        string successMessage,
        Func<apod_wallpaper.ApplicationSettingsSnapshot, Task>? afterSave = null)
    {
        if (_isHydratingControls || _settingsSnapshot == null || _backendHost == null)
            return;

        var updatedSnapshot = await GetFreshSettingsSnapshotAsync();
        update(updatedSnapshot);

        SettingsStatusBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusBar.Title = AppStrings.Get("Saving settings");
        SettingsStatusBar.Message = AppStrings.Get("Persisting changes through the backend facade.");

        var stopwatch = Stopwatch.StartNew();
        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        stopwatch.Stop();
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Error;
            SettingsStatusBar.Title = AppStrings.Get("Settings were not saved");
            SettingsStatusBar.Message = saveResult.Error?.Message ?? AppStrings.Get("Unknown backend error while saving settings.");
            if (_settingsSnapshot != null)
                PopulateSettings(_settingsSnapshot, _apiKeyValidationState);
            return;
        }

        _settingsSnapshot = saveResult.Value;
        PopulateSettings(_settingsSnapshot, _apiKeyValidationState);

        if (afterSave != null)
            await afterSave(_settingsSnapshot);

        SettingsStatusBar.Severity = InfoBarSeverity.Success;
        SettingsStatusBar.Title = AppStrings.Get("Settings saved");
        SettingsStatusBar.Message = string.Format(CultureInfo.CurrentCulture, "{0} ({1} ms)", AppStrings.Get(successMessage), stopwatch.ElapsedMilliseconds);
    }

    private void SetWallpaperStyleButtons(apod_wallpaper.WallpaperStyle style)
    {
        _isHydratingControls = true;
        try
        {
            ApplyWallpaperStyleButtonState(SmartStyleButton, style == apod_wallpaper.WallpaperStyle.Smart);
            ApplyWallpaperStyleButtonState(FillStyleButton, style == apod_wallpaper.WallpaperStyle.Fill);
            ApplyWallpaperStyleButtonState(FitStyleButton, style == apod_wallpaper.WallpaperStyle.Fit);
            ApplyWallpaperStyleButtonState(StretchStyleButton, style == apod_wallpaper.WallpaperStyle.Stretch);
            ApplyWallpaperStyleButtonState(TileStyleButton, style == apod_wallpaper.WallpaperStyle.Tile);
            ApplyWallpaperStyleButtonState(CenterStyleButton, style == apod_wallpaper.WallpaperStyle.Center);
            ApplyWallpaperStyleButtonState(SpanStyleButton, style == apod_wallpaper.WallpaperStyle.Span);
        }
        finally
        {
            _isHydratingControls = false;
        }
    }

    private static void ApplyWallpaperStyleButtonState(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.Background = SelectedWallpaperStyleBrush;
            button.BorderBrush = SelectedWallpaperStyleBrush;
            button.Foreground = SelectedWallpaperStyleForegroundBrush;
            return;
        }

        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.BorderBrushProperty);
        button.ClearValue(Control.ForegroundProperty);
    }

    private async Task SaveWallpaperStyleAsync(apod_wallpaper.WallpaperStyle selectedStyle)
    {
        if (_settingsSnapshot == null || _backendHost == null)
            return;

        var updatedSnapshot = await GetFreshSettingsSnapshotAsync();
        updatedSnapshot.WallpaperStyleIndex = (int)selectedStyle;

        SettingsStatusBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusBar.Title = AppStrings.Get("Saving wallpaper style");
        SettingsStatusBar.Message = AppStrings.Get("Persisting wallpaper style and reapplying the current wallpaper.");

        var stopwatch = Stopwatch.StartNew();
        var saveResult = await _backendHost.Backend.SaveSettingsAsync(updatedSnapshot);
        if (!saveResult.Succeeded || saveResult.Value == null)
        {
            stopwatch.Stop();
            SettingsStatusBar.Severity = InfoBarSeverity.Error;
            SettingsStatusBar.Title = AppStrings.Get("Wallpaper style was not saved");
            SettingsStatusBar.Message = saveResult.Error?.Message ?? AppStrings.Get("Unable to save the wallpaper style.");
            if (_settingsSnapshot != null)
                PopulateSettings(_settingsSnapshot, _apiKeyValidationState);
            return;
        }

        _settingsSnapshot = saveResult.Value;
        PopulateSettings(_settingsSnapshot, _apiKeyValidationState);

        var reapplyResult = await _backendHost.Backend.ReapplyCurrentWallpaperStyleAsync(selectedStyle);
        stopwatch.Stop();
        if (!reapplyResult.Succeeded)
        {
            SettingsStatusBar.Severity = InfoBarSeverity.Warning;
            SettingsStatusBar.Title = AppStrings.Get("Style saved, but wallpaper was not reapplied");
            SettingsStatusBar.Message = AppStrings.GetBackendMessageOrDefault(reapplyResult.Error?.Message, "Unable to reapply the current wallpaper with the selected style.");
            return;
        }

        SettingsStatusBar.Severity = InfoBarSeverity.Success;
        SettingsStatusBar.Title = AppStrings.Get("Wallpaper style applied");
        SettingsStatusBar.Message = AppStrings.Format(
            "The current wallpaper was reapplied using {0}. ({1} ms)",
            AppStrings.WallpaperStyleName(selectedStyle),
            stopwatch.ElapsedMilliseconds);
    }

    private async Task<apod_wallpaper.ApplicationSettingsSnapshot> GetFreshSettingsSnapshotAsync()
    {
        if (_backendHost == null)
            return _settingsSnapshot?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();

        var latestSettingsResult = await _backendHost.Backend.GetSettingsAsync();
        if (latestSettingsResult.Succeeded && latestSettingsResult.Value != null)
            return latestSettingsResult.Value.Clone();

        return _settingsSnapshot?.Clone() ?? new apod_wallpaper.ApplicationSettingsSnapshot();
    }

    private void UpdateApiKeyStateBadge(apod_wallpaper.ApplicationSettingsSnapshot settings, apod_wallpaper.ApiKeyValidationState validationState)
    {
        var rawKey = settings.NasaApiKey?.Trim();
        var usesDemoKey = string.IsNullOrWhiteSpace(rawKey) || string.Equals(rawKey, "DEMO_KEY", StringComparison.OrdinalIgnoreCase);

        if (!usesDemoKey && validationState == apod_wallpaper.ApiKeyValidationState.Valid)
        {
            ApiKeyStateBadge.Background = ApiKeyValidBrush;
            ApiKeyStateText.Text = AppStrings.Get("Valid personal key");
            ApiKeySummaryText.Text = AppStrings.Get("Personal NASA API key is active.");
            return;
        }

        if (!usesDemoKey && validationState == apod_wallpaper.ApiKeyValidationState.Invalid)
        {
            ApiKeyStateBadge.Background = ApiKeyInvalidBrush;
            ApiKeyStateText.Text = AppStrings.Get("Invalid key, DEMO_KEY fallback active");
            ApiKeySummaryText.Text = AppStrings.Get("The saved key looks invalid. The app will continue through DEMO_KEY and HTML fallback.");
            return;
        }

        ApiKeyStateBadge.Background = ApiKeyDemoBrush;
        ApiKeyStateText.Text = AppStrings.Get("DEMO_KEY / no personal key");
        ApiKeySummaryText.Text = AppStrings.Get("Configure a personal key to avoid rate limiting issues.");
    }

    private void SetErrorState(string message)
    {
        SettingsStatusBar.Severity = InfoBarSeverity.Error;
        SettingsStatusBar.Title = AppStrings.Get("Settings unavailable");
        SettingsStatusBar.Message = message;
        ApiKeyTextBox.IsEnabled = false;
        AutoCheckToggle.IsEnabled = false;
        StartWithWindowsToggle.IsEnabled = false;
        CloseToTrayToggle.IsEnabled = false;
        ImagesDirectoryTextBox.IsEnabled = false;
        BrowseImagesFolderButton.IsEnabled = false;
        OpenImagesFolderButton.IsEnabled = false;
        SmartStyleButton.IsEnabled = false;
        FillStyleButton.IsEnabled = false;
        FitStyleButton.IsEnabled = false;
        StretchStyleButton.IsEnabled = false;
        TileStyleButton.IsEnabled = false;
        CenterStyleButton.IsEnabled = false;
        SpanStyleButton.IsEnabled = false;
        GetApiKeyButton.IsEnabled = false;
    }
}
