using HNControl.Web.Data;
using HNControl.Web.Models;
using HNControl.Web.Pages.Admin.SystemPages;
using HNControl.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class EngineeringNotificationSettingsTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var page = new ConfigurationModel(db, null!, null!, null!, null!);
        var context = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = context };
        page.TempData = new TempDataDictionary(context, new ClientCommunicationTests.MemoryTempData());
        await page.OnGetAsync();
        check(DocumentNotificationRecipients.Parse(page.Input.EngineeringNotificationEmails).SequenceEqual(new[]
            { "jsantana@hubnet-solutions.net", "dtaboada@hubnet-solutions.net" }),
            "Ingeniería propone los dos correos solicitados sin configuración previa");

        var config = new SystemConfiguration
        {
            CompanyName = "Empresa conservada", SmtpHost = "smtp.example.test",
            DocumentNotificationEmails = "documentos@example.test"
        };
        db.SystemConfigurations.Add(config);
        await db.SaveChangesAsync();
        page.Input.EngineeringNotificationEmails = "operaciones@example.test;ingenieria@example.test\nOPERACIONES@example.test";
        page.Input.DocumentNotificationEmails = "no-guardar@example.test";
        page.Input.CompanyName = "No guardar";
        var saved = await page.OnPostSaveEngineeringNotificationsAsync();
        check(saved is RedirectToPageResult && page.FlashType == "success"
            && config.EngineeringNotificationEmails == "operaciones@example.test\ningenieria@example.test",
            "Ingeniería permite agregar destinatarios y elimina duplicados sin distinguir mayúsculas");
        check(config.DocumentNotificationEmails == "documentos@example.test"
            && config.CompanyName == "Empresa conservada" && config.SmtpHost == "smtp.example.test",
            "Guardar Ingeniería conserva destinatarios de documentos, empresa y SMTP");

        var valid = config.EngineeringNotificationEmails;
        page.Input.EngineeringNotificationEmails = "correo inválido";
        await page.OnPostSaveEngineeringNotificationsAsync();
        check(page.FlashType == "danger" && config.EngineeringNotificationEmails == valid,
            "Correo de Ingeniería inválido no reemplaza la lista guardada");
        page.Input.EngineeringNotificationEmails = new string('x', 4001);
        await page.OnPostSaveEngineeringNotificationsAsync();
        check(page.FlashType == "danger" && config.EngineeringNotificationEmails == valid,
            "Lista de Ingeniería mayor a 4000 caracteres se rechaza sin cambios");

        page.Input.EngineeringNotificationEmails = "ingenieria@example.test";
        await page.OnPostSaveEngineeringNotificationsAsync();
        check(config.EngineeringNotificationEmails == "ingenieria@example.test",
            "Ingeniería permite quitar destinatarios anteriores");
        page.Input.EngineeringNotificationEmails = "";
        await page.OnPostSaveEngineeringNotificationsAsync();
        await page.OnGetAsync();
        check(page.Input.EngineeringNotificationEmails == "" && config.DocumentNotificationEmails == "documentos@example.test",
            "Vaciar Ingeniería desactiva solo esos avisos y la recarga no repone los correos iniciales");
        page.Input.EngineeringNotificationEmails = SystemConfiguration.DefaultEngineeringNotificationEmails;
        page.Input.CompanyName = "Empresa actualizada";
        await page.OnPostSaveGeneralAsync();
        check(config.EngineeringNotificationEmails == "" && config.CompanyName == "Empresa actualizada",
            "Guardar datos generales no reactiva destinatarios de Ingeniería eliminados");

        await using var postgres = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=script_only;Username=test;Password=test").Options);
        var sql = postgres.GetService<IMigrator>().GenerateScript(
            "20261022000200_AddDeliverySignaturesAndNotificationRecipients", "20261023000100_AddEngineeringNotificationEmails");
        check(sql.Contains("ALTER TABLE \"SystemConfigurations\" ADD \"EngineeringNotificationEmails\"")
            && sql.Contains("jsantana@hubnet-solutions.net") && sql.Contains("dtaboada@hubnet-solutions.net")
            && !sql.Contains("DROP TABLE") && !sql.Contains("DROP COLUMN"),
            "Migración de Ingeniería agrega una columna con los destinatarios iniciales y conserva datos");
    }
}
