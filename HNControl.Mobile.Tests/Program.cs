using System.Net;
using System.Text;
using System.Text.Json;
using HNControl.Mobile.Models;
using HNControl.Mobile.Services;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
var session = new AuthSession();
session.Set(new LoginResponseDto { Token = "test-token", ExpiresAtUtc = DateTime.UtcNow.AddHours(1) });
var handler = new FakeHandler();
var api = new MobileApiClient(new HttpClient(handler), new MobileApiSettings(), session);
var orders = new OrdersService(api);
var viaticos = new ViaticosService(api);
var id = Guid.NewGuid();
handler.Body = "[]";
await orders.ListAsync(999);
Check(handler.Path.EndsWith("/orders?take=300"), "Order query respects server limit");
Check(handler.Auth == "Bearer test-token", "Authenticated requests carry bearer token");
await orders.TakeAsync(id);
Check(handler.Method == "POST" && handler.Path.EndsWith($"/orders/{id}/take"), "Take uses existing endpoint");
handler.Body = "{}";
await orders.UpdateNotesAsync(id, new() { LevantamientoNotes = "Revisión", MaterialesNotes = "Cable" });
Check(handler.Method == "PUT" && JsonDocument.Parse(handler.Payload).RootElement.GetProperty("LevantamientoNotes").GetString() == "Revisión", "Technical notes round-trip accents");
await orders.UpdateChecklistAsync(id, new() { Items = [new() { Id = id, IsDone = true, Notes = "OK" }] });
Check(JsonDocument.Parse(handler.Payload).RootElement.GetProperty("Items")[0].GetProperty("IsDone").GetBoolean(), "Checklist completion serializes");
await orders.SubmitAsync(id, "data:image/png;base64,dGVzdA==", "data:image/png;base64,Y2xpZW50");
Check(handler.Payload.Contains("techSigDataUrl") && handler.Payload.Contains("clientSigDataUrl"), "Both signatures sent on submission");
await viaticos.EnsureWeekAsync(new DateTime(2026,9,28));
Check(JsonDocument.Parse(handler.Payload).RootElement.ValueKind == JsonValueKind.String, "Week creation matches server DateTime body");
await viaticos.CreateTravelWeekAsync(new() { TripDestination = "Monterrey", TripPurpose = "Servicio", RequestedAdvanceAmount = 1250.50m });
Check(handler.Path.EndsWith("/weeks/travel") && handler.Payload.Contains("1250.50"), "Travel request uses exact decimal amount");
await using var file = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-test"));
await viaticos.AddEntryWithAttachmentAsync(id, new() { DayDate = DateTime.Today, Amount = 123.45m, IsBillable = true, Description = "Taxi" }, file, "receipt.pdf", "application/pdf");
Check(handler.ContentType.StartsWith("multipart/form-data") && handler.Payload.Contains("AttachmentFile") && handler.Payload.Contains("123.45"), "Invoice upload field and invariant amount match API");
await viaticos.EditEntryAsync(id, new() { Amount = 44.50m, Description = "Transporte" });
Check(handler.Method == "PUT" && handler.Path.EndsWith($"/entries/{id}"), "Expense edit contract");
await viaticos.DeleteEntryAsync(id);
Check(handler.Method == "DELETE", "Expense delete contract");
await viaticos.SubmitWeekAsync(id);
Check(handler.Path.EndsWith($"/week/{id}/submit"), "Expense submission contract");
handler.Body = """{"profile":{"fullName":"Ana"},"payroll":{"netQuincenal":9820.25},"kpiFeedback":null,"eval360Feedback":{"visibleToEmployee":false}}""";
var dashboard = await new EmployeeService(api).DashboardAsync();
Check(dashboard.Payroll.NetQuincenal == 9820.25m && dashboard.Profile.FullName == "Ana", "Dashboard payroll precision");
Check(dashboard.KpiFeedback == null && dashboard.Eval360Feedback?.VisibleToEmployee == false, "Unpublished evaluations remain distinguishable");

var mine = new ServiceOrderListItemDto { Title = "Enlace", Client = "Norte", IsMine = true, Status = 2, CreatedAt = DateTime.Today };
var overdue = new ServiceOrderListItemDto { Title = "Cámara", Client = "Sur", CanTake = true, Status = 1, EstimatedEndDate = DateTime.Today.AddDays(-1) };
var closed = new ServiceOrderListItemDto { Title = "Cierre", IsMine = true, Status = 4, EstimatedEndDate = DateTime.Today.AddDays(-2) };
var data = new[] { closed, mine, overdue };
Check(OrderPresentation.Filter(data, "all", null)[0] == overdue, "Overdue active work is prioritized");
Check(!closed.IsOverdue, "Completed orders never show overdue");
Check(OrderPresentation.Filter(data, "mine", null).SequenceEqual([mine]), "My work excludes completed orders");
Check(OrderPresentation.Filter(data, "available", null).SequenceEqual([overdue]), "Available filter honors server permissions");
Check(OrderPresentation.Filter(data, "closed", null).SequenceEqual([closed]), "Completed filter");
Check(OrderPresentation.Filter(data, "all", " norte ").SequenceEqual([mine]), "Search trims input and ignores case");
Check(OrderPresentation.Filter(data, "all", "missing").Count == 0, "Search empty state");
Check(mine.AssignmentLabel == "Asignada a ti", "Assigned order has explicit ownership");

foreach (var (status, expected) in new[] { (403, "permiso"), (413, "tamaño"), (409, "Conflicto") })
{
    handler.Status = (HttpStatusCode)status;
    handler.Body = "";
    try { await orders.ListAsync(); throw new Exception("Expected HTTP failure"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains(expected), $"Actionable HTTP {status} error"); }
}
handler.Status = HttpStatusCode.Conflict;
handler.Body = """{"message":"Esta orden ya fue tomada."}""";
try { await orders.TakeAsync(id); throw new Exception("Expected conflict"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("ya fue tomada"), "Server concurrency message preserved"); }
handler.Failure = new HttpRequestException("offline");
try { await orders.ListAsync(); throw new Exception("Expected network failure"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("conexión"), "Offline error in Spanish"); }
handler.Failure = new TaskCanceledException();
try { await orders.TakeAsync(id); throw new Exception("Expected timeout"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("comprobar el resultado"), "Timeout warns against duplicate writes"); }
session.Set(new() { Token = "expired", ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) });
Check(!session.IsLoggedIn, "Expired token cannot restore session");
session.Clear();
Check(!session.IsLoggedIn && session.Token == "", "Logout clears session");
Console.WriteLine($"All {count} checks passed.");

sealed class FakeHandler : HttpMessageHandler
{
    public string Body = "{}";
    public HttpStatusCode Status = HttpStatusCode.OK;
    public Exception? Failure;
    public string Path = "", Method = "", Auth = "", Payload = "", ContentType = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Failure != null) throw Failure;
        Path = request.RequestUri!.PathAndQuery;
        Method = request.Method.Method;
        Auth = request.Headers.Authorization?.ToString() ?? "";
        ContentType = request.Content?.Headers.ContentType?.ToString() ?? "";
        Payload = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
    }
}
