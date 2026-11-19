using System.Text.Json;
using HNControl.Web.Models;

namespace HNControl.Web.Services;

public sealed record ContractProviderIdentity(string? LegalName, string? Rfc, string? FiscalAddress, string? PostalCode)
{
    public bool IsComplete => new[] { LegalName, Rfc, FiscalAddress, PostalCode }.All(x => !string.IsNullOrWhiteSpace(x));
}

// Freeze the template inputs when the document is prepared, before either party signs.
public sealed record ClientSigningSnapshot(Client Client, ClientServiceContract? Contract)
{
    public const int CurrentServiceTemplateRevision = 5;
    public const int CurrentEventTemplateRevision = 2;
    public bool UsesCorporateServiceTemplate { get; init; }
    public int? ServiceTemplateRevision { get; init; }
    public int? EventTemplateRevision { get; init; }
    public ContractProviderIdentity? ProviderIdentity { get; init; }

    public static string Capture(Client client, ClientServiceContract? contract, bool corporateServiceTemplate = true,
        ContractProviderIdentity? provider = null) => JsonSerializer.Serialize(
        new ClientSigningSnapshot(new Client
        {
            Name = client.Name, Rfc = client.Rfc, LegalRepresentative = client.LegalRepresentative,
            ContactName = client.ContactName, FiscalAddress = client.FiscalAddress, Address = client.Address,
            State = client.State, FiscalZipCode = client.FiscalZipCode, BillingEmail = client.BillingEmail,
            Email = client.Email, ClientCode = client.ClientCode
        }, contract == null ? null : new ClientServiceContract
        {
            Label = contract.Label, ServiceType = contract.ServiceType, Notes = contract.Notes, Provider = contract.Provider,
            Branch = contract.Branch, BranchAddress = contract.BranchAddress, ContractNumber = contract.ContractNumber, MonthlyAmount = contract.MonthlyAmount,
            ContractStartDate = contract.ContractStartDate, ContractEndDate = contract.ContractEndDate
        })
        {
            ProviderIdentity = provider,
            UsesCorporateServiceTemplate = corporateServiceTemplate,
            ServiceTemplateRevision = corporateServiceTemplate ? CurrentServiceTemplateRevision : 0,
            EventTemplateRevision = corporateServiceTemplate ? CurrentEventTemplateRevision : 1
        });
}
