namespace HNControl.Mobile.Pages;

public class RootTabsPage : TabbedPage
{
    public RootTabsPage(OrdersPage ordersPage, EmployeeDashboardPage employeeDashboardPage)
    {
        Title = "HN Control";

        Children.Add(new NavigationPage(ordersPage) { Title = "Ordenes" });
        Children.Add(new NavigationPage(employeeDashboardPage) { Title = "Mi ficha" });
    }
}
