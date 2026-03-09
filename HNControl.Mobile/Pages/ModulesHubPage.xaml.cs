using HNControl.Mobile.Services;

namespace HNControl.Mobile.Pages;

public partial class ModulesHubPage : ContentPage
{
    private readonly ModulesService _modules;
    private readonly IServiceProvider _services;

    public ModulesHubPage(ModulesService modules, IServiceProvider services)
    {
        InitializeComponent();
        _modules = modules;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var list = await _modules.GetAllowedModulesAsync();
            ModulesCollection.ItemsSource = list;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnOpenModuleClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string key)
            return;

        Page? page = key switch
        {
            "ServiceOrders" => _services.GetRequiredService<OrdersPage>(),
            "Monitoring" => _services.GetRequiredService<MonitoringModulePage>(),
            "Inventory" => _services.GetRequiredService<InventoryModulePage>(),
            "Carriers" => _services.GetRequiredService<CarriersModulePage>(),
            "Viaticos" => _services.GetRequiredService<ViaticosModulePage>(),
            "Projects" => _services.GetRequiredService<ProjectsModulePage>(),
            "Knowledge" => _services.GetRequiredService<KnowledgeModulePage>(),
            "Leaves" => _services.GetRequiredService<LeavesModulePage>(),
            "Exams" => _services.GetRequiredService<ExamsModulePage>(),
            "Eval360" => _services.GetRequiredService<Eval360ModulePage>(),
            _ => null
        };

        if (page == null)
        {
            await DisplayAlertAsync("Modulo", "Este modulo quedo habilitado, pero su pantalla movil se agrega en la siguiente iteracion.", "OK");
            return;
        }

        await Navigation.PushAsync(page);
    }
}
