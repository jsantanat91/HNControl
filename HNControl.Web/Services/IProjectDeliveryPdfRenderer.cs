using HNControl.Web.Models;

namespace HNControl.Web.Services;

public interface IProjectDeliveryPdfRenderer
{
    Task<byte[]> RenderAsync(ProjectDeliveryFormat delivery);
    Task<byte[]> AppendEvidencePagesAsync(ProjectDeliveryFormat delivery, byte[] basePdf);
}
