using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCreate = HNControl.Web.Pages.Admin.ServiceOrders.CreateModel;
using OrderEdit = HNControl.Web.Pages.Admin.ServiceOrders.EditModel;
using ProjectCreate = HNControl.Web.Pages.Projects.CreateModel;
using ProjectEdit = HNControl.Web.Pages.Projects.EditModel;

internal static class RelatedFormDataTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMvcCore();
        using var provider = services.BuildServiceProvider();
        T Page<T>(T page) where T : PageModel
        {
            var http = new DefaultHttpContext { RequestServices = provider };
            page.PageContext = new PageContext { HttpContext = http };
            page.TempData = new TempDataDictionary(http, new ClientCommunicationTests.MemoryTempData());
            return page;
        }
        var client = new Client { Name = "Cliente A", ClientCode = "HN-A", IsActive = true };
        var other = new Client { Name = "Cliente B", ClientCode = "HN-B", IsActive = true };
        var employee = new EmployeeProfile { UserId = "forms-user", FullName = "Responsable" };
        db.Users.Add(new ApplicationUser { Id = employee.UserId, UserName = "forms@example.test" });
        db.EmployeeProfiles.Add(employee);
        var project = new Project { Client = client, AssignedUserId = employee.UserId, Title = "Proyecto A" };
        var foreignProject = new Project { Client = other, AssignedUserId = employee.UserId, Title = "Proyecto B" };
        var contract = new ClientServiceContract { Client = client, Project = project, Label = "Internet", ContractNumber = "HN-A-01", BranchAddress = "Sucursal A" };
        var unlinked = new ClientServiceContract { Client = client, Label = "WiFi" };
        var foreignContract = new ClientServiceContract { Client = other, Project = foreignProject, Label = "Otro cliente" };
        var order = new ServiceOrder { Client = client, Project = project, ClientServiceContract = contract, Title = "Visita", StartedAt = DateTime.UtcNow.Date, EstimatedEndDate = DateTime.UtcNow.Date.AddDays(1) };
        db.AddRange(client, other, project, foreignProject, contract, unlinked, foreignContract, order);
        await db.SaveChangesAsync();

        var fresh = Page(new OrderCreate(db, null!));
        await fresh.OnGetAsync();
        check(fresh.Input.ClientId == Guid.Empty && !fresh.ContractItems.Any(), "Nueva orden exige elegir cliente sin seleccionar el primero automáticamente");
        var deepLink = Page(new OrderCreate(db, null!));
        await deepLink.OnGetAsync(client.Id, contractId: contract.Id);
        check(deepLink.Input.ClientId == client.Id && deepLink.Input.ProjectId == project.Id && deepLink.Input.ClientServiceContractId == contract.Id,
            "Crear orden desde contrato hereda cliente, contrato y proyecto");
        check(deepLink.ProjectItems.Any(x => x.Selected && x.Value == project.Id.ToString()) && deepLink.ContractItems.Any(x => x.Selected && x.Value == contract.Id.ToString()),
            "La primera respuesta HTML ya incluye las opciones seleccionadas");
        deepLink.ModelState.AddModelError("Input.Title", "Falta título");
        check(await deepLink.OnPostAsync() is PageResult && deepLink.ContractItems.Any(x => x.Selected && x.Value == contract.Id.ToString()),
            "Un error de validación al crear conserva proyecto y contrato");
        var mismatched = Page(new OrderCreate(db, null!));
        await mismatched.OnGetAsync(client.Id, foreignProject.Id, foreignContract.Id);
        check(mismatched.Input.ProjectId == null && mismatched.Input.ClientServiceContractId == null,
            "Los enlaces no precargan proyectos ni contratos de otro cliente");
        var choices = await ServiceOrderFormData.ContractsAsync(db, client.Id, project.Id);
        check(choices.Count == 2 && choices.All(x => x.Id != foreignContract.Id) && choices.Single(x => x.Id == contract.Id).Address == "Sucursal A",
            "Los datos del contrato se consultan por cliente y proyecto sin mezclar otras cuentas");
        var start = new DateTime(2026, 11, 5);
        check(await ServiceOrderFormData.ValidateAsync(db, client.Id, foreignProject.Id, null, start, start) != null,
            "Backend rechaza un proyecto de otro cliente aunque no se ligue contrato");
        check(await ServiceOrderFormData.ValidateAsync(db, client.Id, null, foreignContract.Id, start, start) != null,
            "Backend rechaza un contrato de otro cliente");
        check(await ServiceOrderFormData.ValidateAsync(db, client.Id, project.Id, contract.Id, start, start.AddDays(-1)) != null
            && await ServiceOrderFormData.ValidateAsync(db, client.Id, project.Id, contract.Id, start, start) == null,
            "La orden admite atención en un día y rechaza fechas invertidas");

        var edit = Page(new OrderEdit(db));
        await edit.OnGetAsync(order.Id);
        check(edit.Input!.StartDate == order.StartedAt!.Value.Date && edit.Input.ExpectedEndDate == order.EstimatedEndDate!.Value.Date,
            "Editar una orden conserva los días programados sin desplazarlos por zona horaria");
        check(edit.ProjectItems.Any(x => x.Selected && x.Value == project.Id.ToString()) && edit.ContractItems.Any(x => x.Selected && x.Value == contract.Id.ToString()),
            "Editar orden renderiza y conserva sus relaciones existentes");
        edit.ModelState.AddModelError("Input.StartDate", "Fecha inválida capturada");
        check(await edit.OnPostAsync() is PageResult && edit.ModelState.ContainsKey("Input.StartDate") && edit.ContractItems.Any(x => x.Selected),
            "Editar orden conserva errores de captura y selección, sin guardar valores de fecha inválidos");

        var createProject = Page(new ProjectCreate(db));
        createProject.Input.ClientId = client.Id;
        createProject.Input.Title = "Validación de contratos";
        createProject.Input.ResponsibleUserId = employee.UserId;
        createProject.Input.ContractIds = [foreignContract.Id];
        check(await createProject.OnPostAsync() is PageResult && createProject.ModelState.ContainsKey("Input.ContractIds"),
            "Crear proyecto rechaza contratos que pertenecen a otro cliente");
        var editProject = Page(new ProjectEdit(db));
        await editProject.OnGetAsync(project.Id);
        check(editProject.ContractItems.Any(x => x.Selected && x.Value == contract.Id.ToString()),
            "Editar proyecto conserva sus contratos seleccionados en la lista múltiple");
        editProject.Input.ClientId = other.Id;
        editProject.Input.ContractIds = [foreignContract.Id];
        var result = await editProject.OnPostAsync();
        await db.Entry(contract).ReloadAsync(); await db.Entry(foreignContract).ReloadAsync();
        check(result is RedirectToPageResult && contract.ProjectId == null && foreignContract.ProjectId == project.Id,
            "Al cambiar el cliente de un proyecto se actualizan los vínculos sin dejar contratos del cliente anterior");
    }
}
