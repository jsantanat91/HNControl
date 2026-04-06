using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface ITemplateDocxService
{
    byte[] BuildClientLegalDocx(ClientLegalDocument document, Client client, ClientServiceContract? contract);
    byte[] BuildDeliveryDocx(ProjectDeliveryFormat delivery, Client client, Project? project);
}
