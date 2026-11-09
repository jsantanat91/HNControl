using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
namespace HNControl.Mobile.Pages;
public class RootTabsPage : Microsoft.Maui.Controls.TabbedPage
{
    public RootTabsPage(OrdersPage orders, EmployeeDashboardPage employee, ViaticosModulePage viaticos, ServiceInventoryPage inventory)
    {
        Title = "HN Control";
        BarBackgroundColor = Colors.White;
        BarTextColor = Color.FromArgb("#10284A");
        SelectedTabColor = Color.FromArgb("#087EE8");
        UnselectedTabColor = Color.FromArgb("#69809E");
        this.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().SetIsSwipePagingEnabled(false);
        this.On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().SetToolbarPlacement(ToolbarPlacement.Bottom);
        AddTab(orders, "Órdenes", "nav_orders.png");
        AddTab(employee, "Mi dashboard", "nav_employee.png");
        AddTab(viaticos, "Viáticos", "nav_wallet.png");
        AddTab(inventory, "Servicios", "nav_services.png");
    }
    private void AddTab(Page page, string title, string icon) =>
        Children.Add(new NavigationPage(page) { Title = title, IconImageSource = icon,
            BarBackgroundColor = Colors.White, BarTextColor = Color.FromArgb("#10284A") });
}