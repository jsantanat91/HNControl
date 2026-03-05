using HNControl.Mobile.Pages;
using HNControl.Mobile.Services;

namespace HNControl.Mobile;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        Services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var session = Services.GetRequiredService<AuthSession>();
        var rootPage = session.IsLoggedIn
            ? CreateMainPage()
            : CreateLoginPage();

        return new Window(rootPage);
    }

    public static Page CreateLoginPage() => new NavigationPage(Services.GetRequiredService<LoginPage>());

    public static Page CreateMainPage() => Services.GetRequiredService<RootTabsPage>();

    public static void SwitchToMain()
    {
        if (Current?.Windows?.Count > 0)
        {
            Current.Windows[0].Page = CreateMainPage();
        }
    }

    public static void SwitchToLogin()
    {
        if (Current?.Windows?.Count > 0)
        {
            Current.Windows[0].Page = CreateLoginPage();
        }
    }
}
