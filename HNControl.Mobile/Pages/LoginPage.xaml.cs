using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;
    private bool _busy;
#if ANDROID
    private bool? _previousLightStatusBar;
    private Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust? _previousSoftInputMode;
#endif

    public LoginPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
        VersionLabel.Text = "Versión " + AppInfo.Current.VersionString;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (Application.Current is { } app)
        {
            _previousSoftInputMode ??= Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.GetWindowSoftInputModeAdjust(app);
            Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(app,
                Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
        }
        if (Platform.CurrentActivity?.Window is { } window)
        {
            var insets = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
            if (insets != null)
            {
                _previousLightStatusBar ??= insets.AppearanceLightStatusBars;
                insets.AppearanceLightStatusBars = false;
            }
        }
#endif
        var saved = await _auth.Session.LoadSavedCredentialsAsync();
        RememberCheck.IsChecked = saved.Remember;
        if (saved.Remember)
        {
            EmailEntry.Text = saved.User;
            PasswordEntry.Text = saved.Password;
        }

        HeroLayout.Opacity = 0;
        HeroLayout.TranslationY = 18;
        LoginCard.Opacity = 0;
        LoginCard.TranslationY = 22;
        await Task.WhenAll(
            HeroLayout.FadeToAsync(1, 320, Easing.CubicOut),
            HeroLayout.TranslateToAsync(0, 0, 320, Easing.CubicOut));
        await Task.WhenAll(
            LoginCard.FadeToAsync(1, 260, Easing.CubicOut),
            LoginCard.TranslateToAsync(0, 0, 260, Easing.CubicOut));
#if ANDROID
        // NavigationPage applies its system bar appearance during its first layout.
        if (Platform.CurrentActivity?.Window is { } readyWindow
            && AndroidX.Core.View.WindowCompat.GetInsetsController(readyWindow, readyWindow.DecorView) is { } readyInsets)
            readyInsets.AppearanceLightStatusBars = false;
#endif
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_busy) return;
        ErrorPanel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrEmpty(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Escribe tu usuario y contraseña para continuar.";
            ErrorPanel.IsVisible = true;
            return;
        }
        SetBusy(true);
        try
        {
            await _auth.LoginAsync(
                EmailEntry.Text ?? "",
                PasswordEntry.Text ?? "");
            await _auth.Session.SaveCredentialsAsync(
                EmailEntry.Text ?? "",
                PasswordEntry.Text ?? "",
                RememberCheck.IsChecked);

            App.SwitchToMain();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorPanel.IsVisible = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (_previousLightStatusBar.HasValue && Platform.CurrentActivity?.Window is { } window
            && AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView) is { } insets)
            insets.AppearanceLightStatusBars = _previousLightStatusBar.Value;
        _previousLightStatusBar = null;
        if (_previousSoftInputMode.HasValue && Application.Current is { } app)
            Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(app, _previousSoftInputMode.Value);
        _previousSoftInputMode = null;
#endif
        base.OnDisappearing();
    }

    private void OnTogglePassword(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        if (sender is Button button) button.Text = PasswordEntry.IsPassword ? "Ver" : "Ocultar";
        SemanticProperties.SetDescription(PasswordToggle, PasswordEntry.IsPassword ? "Mostrar contraseña" : "Ocultar contraseña");
    }

    private void OnEmailCompleted(object sender, EventArgs e) => PasswordEntry.Focus();
    private void OnRememberTapped(object sender, TappedEventArgs e)
    {
        if (!_busy) RememberCheck.IsChecked = !RememberCheck.IsChecked;
    }
    private void OnFieldFocused(object sender, FocusEventArgs e)
    {
        var border = sender == EmailEntry ? EmailBorder : PasswordBorder;
        border.Stroke = Color.FromArgb("#81E5F4");
        border.StrokeThickness = 2;
    }
    private void OnFieldUnfocused(object sender, FocusEventArgs e)
    {
        var border = sender == EmailEntry ? EmailBorder : PasswordBorder;
        border.Stroke = Color.FromArgb("#BFD2F2");
        border.StrokeThickness = 1;
    }
    private void OnEntryHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        // The pill supplies the border; remove Android's second, underlined border only here.
        if (sender is Entry entry && entry.Handler?.PlatformView is Android.Widget.EditText native)
            native.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
    }

    private void SetBusy(bool isBusy)
    {
        _busy = isBusy;
        BusyPanel.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoginButton.IsEnabled = !isBusy;
        LoginButton.Text = isBusy ? "Conectando…" : "Iniciar sesión   →";
        EmailEntry.IsEnabled = !isBusy;
        PasswordEntry.IsEnabled = !isBusy;
        PasswordToggle.IsEnabled = !isBusy;
        RememberCheck.IsEnabled = !isBusy;
    }
}
