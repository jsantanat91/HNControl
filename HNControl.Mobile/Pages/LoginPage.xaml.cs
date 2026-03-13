using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;

    public LoginPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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
        ErrorLabel.IsVisible = false;
        SetBusy(true);
        try
        {
            await _auth.LoginAsync(
                EmailEntry.Text ?? "",
                PasswordEntry.Text ?? "");

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

    private void SetBusy(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoginButton.IsEnabled = !isBusy;
        EmailEntry.IsEnabled = !isBusy;
        PasswordEntry.IsEnabled = !isBusy;
    }
}
