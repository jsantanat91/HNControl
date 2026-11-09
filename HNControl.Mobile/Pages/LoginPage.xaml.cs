using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;
    private bool _busy;

    public LoginPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_busy) return;
        ErrorLabel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) || string.IsNullOrEmpty(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Escribe tu usuario y contraseña para continuar.";
            ErrorLabel.IsVisible = true;
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
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnTogglePassword(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        if (sender is Button button) button.Text = PasswordEntry.IsPassword ? "Ver" : "Ocultar";
    }

    private void SetBusy(bool isBusy)
    {
        _busy = isBusy;
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoginButton.IsEnabled = !isBusy;
        EmailEntry.IsEnabled = !isBusy;
        PasswordEntry.IsEnabled = !isBusy;
    }
}
