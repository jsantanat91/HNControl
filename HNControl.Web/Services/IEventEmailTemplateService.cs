namespace HNControl.Web.Services;

public interface IEventEmailTemplateService
{
    Task<(string Subject, string Body)> RenderAsync(
        string eventKey,
        string defaultSubject,
        string defaultBody,
        IDictionary<string, string>? vars = null);
}

