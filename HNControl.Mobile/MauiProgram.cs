using HNControl.Mobile.Pages;
using HNControl.Mobile.Services;

namespace HNControl.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<MobileApiSettings>();
        builder.Services.AddSingleton<AuthSession>();

#if DEBUG
        builder.Services.AddSingleton(_ =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            return new HttpClient(handler);
        });
#else
        builder.Services.AddSingleton(_ => new HttpClient());
#endif

        builder.Services.AddSingleton<MobileApiClient>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<OrdersService>();
        builder.Services.AddSingleton<EmployeeService>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<OrdersPage>();
        builder.Services.AddTransient<OrderDetailPage>();
        builder.Services.AddTransient<EmployeeDashboardPage>();
        builder.Services.AddTransient<RootTabsPage>();

        return builder.Build();
    }
}
