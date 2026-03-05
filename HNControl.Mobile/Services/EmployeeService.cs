using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class EmployeeService
{
    private readonly MobileApiClient _api;

    public EmployeeService(MobileApiClient api)
    {
        _api = api;
    }

    public Task<EmployeeDashboardDto> DashboardAsync()
    {
        return _api.GetJsonAsync<EmployeeDashboardDto>("api/mobile/employee/dashboard");
    }
}
