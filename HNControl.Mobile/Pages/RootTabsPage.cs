namespace HNControl.Mobile.Pages;

public class RootTabsPage : TabbedPage
{
    public RootTabsPage(OrdersPage ordersPage, EmployeeDashboardPage employeeDashboardPage, ModulesHubPage modulesHubPage)
    {
        Title = "HN Control";
        BarBackgroundColor = Color.FromArgb("#FFFFFF");
        BarTextColor = Color.FromArgb("#0E2242");
        SelectedTabColor = Color.FromArgb("#2252D5");
        UnselectedTabColor = Color.FromArgb("#7A869A");
        Children.Add(new NavigationPage(ordersPage)
        {
            Title = "Ordenes",
            BarBackgroundColor = Color.FromArgb("#0E2242"),
            BarTextColor = Colors.White
        });

        Children.Add(new NavigationPage(employeeDashboardPage)
        {
            Title = "Mi ficha",
            BarBackgroundColor = Color.FromArgb("#0E2242"),
            BarTextColor = Colors.White
        });

        Children.Add(new NavigationPage(modulesHubPage)
        {
            Title = "Modulos",
            BarBackgroundColor = Color.FromArgb("#0E2242"),
            BarTextColor = Colors.White
        });
    }
}
