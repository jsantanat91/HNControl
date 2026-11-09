using System.Text.RegularExpressions;
using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class ServiceOrderReportTests
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var folder = Path.GetFullPath(Path.Combine("artifacts", "service-order-reports"));
        Directory.CreateDirectory(folder);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:BasePath"] = folder, ["Branding:ReportFooter"] = "HN Solutions · Documento de muestra"
        }).Build();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var storage = new LocalFileStorage(config);
        var pdf = new ServiceOrderPdfRenderer(config, db, storage, new TestEnvironment(root));
        var day = new DateTime(2026, 11, 8, 16, 30, 0, DateTimeKind.Utc);
        var customer = new Client { Name = "Centro de Convenciones del Valle", ClientCode = "HN-DEMO", Email = "eventos@example.test" };
        var contract = new ClientServiceContract
        {
            Client = customer, ContractNumber = "HN-DEMO-01", ServiceType = ClientServiceType.Internet,
            Label = "Internet dedicado para evento", Branch = "Salón norte", BranchAddress = "Av. Central 120, Ciudad de México",
            Notes = "[META] Recurrencia=Eventual\n[META] InternetCapacidad=20",
            ContractStartDate = day.Date, ContractEndDate = day.Date.AddDays(1)
        };
        var order = new ServiceOrder
        {
            Client = customer, ClientServiceContract = contract, Title = "Instalación de conectividad para evento",
            Type = ServiceOrderType.Eventos, Status = ServiceOrderStatus.Finalized,
            Description = "Instalación del enlace dedicado y habilitación de la red Wi-Fi para asistentes.",
            MaterialesNotes = "Tres puntos de acceso y cableado de red.", CreatedAt = day.AddDays(-2),
            EstimatedEndDate = day.Date, FinalizedAt = day
        };
        order.Checklist.Add(new() { SortOrder = 1, Title = "Validar enlace principal", IsDone = true, Notes = "Enlace operativo." });
        order.Checklist.Add(new() { SortOrder = 2, Title = "Verificar cobertura Wi-Fi", IsDone = true, Notes = "Cobertura validada en salón." });
        order.Checklist.Add(new() { SortOrder = 3, Title = "Probar acceso de asistentes", IsDone = true, Notes = "Acceso confirmado." });
        db.Add(order);
        await db.SaveChangesAsync();
        var unsigned = await pdf.RenderAsync(order);
        await File.WriteAllBytesAsync(Path.Combine(folder, "pendiente.pdf"), unsigned);

        static byte[] SampleImage(string label) => Document.Create(d => d.Page(p =>
        {
            p.Size(230, 44);
            p.Content().AlignLeft().AlignMiddle().Text(label).Italic().FontSize(18).FontColor("#596F82");
        })).GenerateImages().Single();
        foreach (var role in new[] { SignatureRole.Technician, SignatureRole.Client })
        {
            var name = role == SignatureRole.Technician ? "Técnico de muestra" : "Cliente de muestra";
            var path = await storage.SaveBytesAsync(SampleImage(name), "capturas", role + ".png", "image/png");
            db.ServiceOrderSignatures.Add(new() { Order = order, Role = role, SignedByName = name, SignedAt = day, StoragePath = path.storagePath });
        }
        await db.SaveChangesAsync();
        async Task<byte[]> Save(string name)
        {
            var bytes = await pdf.RenderAsync(order);
            await File.WriteAllBytesAsync(Path.Combine(folder, name + ".pdf"), bytes);
            return bytes;
        }
        static (string Hash, int Pages) Inspect(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            return (Regex.Match(doc.Info.Subject ?? "", "[A-F0-9]{64}$").Value, doc.PageCount);
        }
        var signed = Inspect(await Save("orden-evento-20mb"));
        check(signed.Hash.Length == 64 && signed.Hash != Inspect(unsigned).Hash && signed.Pages == 1,
            "Orden de evento con ambas firmas cabe en una hoja y su huella SHA-256 refleja las capturas");
        check(Inspect(await pdf.RenderAsync(order)).Hash == signed.Hash,
            "Regenerar la misma orden conserva la huella de sus datos");
        var capture = order.Signatures.Single(s => s.Role == SignatureRole.Client);
        await File.WriteAllBytesAsync(Path.Combine(folder, capture.StoragePath), SampleImage("Segunda captura de muestra"));
        var changedSignature = Inspect(await pdf.RenderAsync(order)).Hash;
        check(changedSignature != signed.Hash, "Cambiar la imagen de una firma manteniendo nombre y fecha cambia la huella");
        contract.Notes = contract.Notes.Replace("=20", "=50");
        await db.SaveChangesAsync();
        var changedSpeed = Inspect(await pdf.RenderAsync(order)).Hash;
        check(changedSpeed != changedSignature, "Cambiar únicamente la velocidad contratada cambia la huella de la orden");
        order.Checklist[0].Notes = "Resultado corregido";
        await db.SaveChangesAsync();
        check(Inspect(await pdf.RenderAsync(order)).Hash != changedSpeed,
            "La huella detecta cambios de contenido sin cambiar la cantidad de verificaciones");

        contract.Notes = "[META] Recurrencia=Mensual\n[META] InternetCapacidad=20";
        contract.Label = "Internet Dedicado";
        order.Title = "Entrega de enlace de Internet Dedicado 20 MB";
        order.Description = "Instalación del enlace dedicado y validación de la conexión en la sucursal.";
        order.Type = ServiceOrderType.NuevaInstalacion;
        await db.SaveChangesAsync();
        check(Inspect(await Save("orden-mensual-noc")).Pages == 1,
            "Orden mensual con firmas y NOC conserva una página para una entrega breve");

        var hardware = new HNControl.Shared.OrderDeliveryData { Kind = "both", HasRouter = true,
            RouterBrand = "MikroTik", RouterModel = "hEX", RouterSerial = "HN-RTR-001", RouterAccessories = "Fuente de poder y cable de red" };
        hardware.Items.Add(new() { Name = "Punto de acceso", Brand = "Ubiquiti", Model = "U6+", Serial = "HN-AP-001", Mode = "Venta", Accessories = "Montaje y PoE" });
        order.DeliveryJson = hardware.Serialize();
        await db.SaveChangesAsync();
        var hardwareReport = Inspect(await Save("orden-entrega-unificada"));
        hardware.RouterSerial = "HN-RTR-002"; order.DeliveryJson = hardware.Serialize(); await db.SaveChangesAsync();
        check(Inspect(await pdf.RenderAsync(order)).Hash != hardwareReport.Hash, "Entrega: cambiar la serie del router cambia la huella del documento");
        for (var i = 0; i < 18; i++) hardware.Items.Add(new() { Name = "Equipo adicional de prueba " + i, Serial = "QA-" + i, Accessories = "Cable y fuente de poder. Observaciones de estado físico." });
        order.DeliveryJson = hardware.Serialize(); await db.SaveChangesAsync();
        check(Inspect(await Save("orden-entrega-equipos-multipagina")).Pages > 1, "Entrega: múltiples equipos paginan sin eliminar firmas ni NOC");
        order.DeliveryJson = "";
        order.Type = ServiceOrderType.Global;
        db.ServiceOrderWorkItems.Add(new() { Order = order, SortOrder = 0, Type = ServiceOrderType.Correctivo, Title = "Actividad sin checklist", WorkPerformed = "Cableado revisado." });
        for (var i = 4; i <= 55; i++) db.ServiceOrderChecklistItems.Add(new() { Order = order, SortOrder = i, Title = $"Punto de control {i}", Notes = "Observación de muestra." });
        var evidence = await storage.SaveBytesAsync(SampleImage("Evidencia de muestra"), "capturas", "evidencia.png", "image/png");
        db.Set<ServiceOrderEvidence>().Add(new() { OrderId = order.Id, OriginalFileName = "Evidencia de prueba.png", StoragePath = evidence.storagePath, ContentType = "image/png" });
        db.Set<ServiceOrderEvidence>().Add(new() { OrderId = order.Id, OriginalFileName = "Reporte de visita.pdf", ContentType = "application/pdf" });
        order.ClientServiceContract = null;
        order.ClientServiceContractId = null;
        await db.SaveChangesAsync();
        check(Inspect(await Save("orden-global")).Pages > 1, "Orden global sin contrato admite checklist multipágina, evidencias y firmas");
    }
}
