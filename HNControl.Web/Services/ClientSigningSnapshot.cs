using System.Text.Json;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

// Freeze the template inputs when the document is prepared, before either party signs.
public sealed record ClientSigningSnapshot(Client Client, ClientServiceContract? Contract)
{
    public const int CurrentServiceTemplateRevision = 2;
    public const int CurrentEventTemplateRevision = 2;
    public bool UsesCorporateServiceTemplate { get; init; }
    public int? ServiceTemplateRevision { get; init; }
    public int? EventTemplateRevision { get; init; }

    public static string Capture(Client client, ClientServiceContract? contract, bool corporateServiceTemplate = true) => JsonSerializer.Serialize(
        new ClientSigningSnapshot(new Client
        {
            Name = client.Name, Rfc = client.Rfc, LegalRepresentative = client.LegalRepresentative,
            ContactName = client.ContactName, FiscalAddress = client.FiscalAddress, Address = client.Address,
            State = client.State, FiscalZipCode = client.FiscalZipCode, BillingEmail = client.BillingEmail,
            Email = client.Email, ClientCode = client.ClientCode
        }, contract == null ? null : new ClientServiceContract
        {
            Label = contract.Label, ServiceType = contract.ServiceType, Notes = contract.Notes,
            Branch = contract.Branch, BranchAddress = contract.BranchAddress, ContractNumber = contract.ContractNumber, MonthlyAmount = contract.MonthlyAmount,
            ContractStartDate = contract.ContractStartDate, ContractEndDate = contract.ContractEndDate
        })
        {
            UsesCorporateServiceTemplate = corporateServiceTemplate,
            ServiceTemplateRevision = corporateServiceTemplate ? CurrentServiceTemplateRevision : 0,
            EventTemplateRevision = corporateServiceTemplate ? CurrentEventTemplateRevision : 1
        });
}
