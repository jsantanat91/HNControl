using HNControl.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HNControl.Web.Services;

public class EventEmailTemplateService : IEventEmailTemplateService
{
    private readonly ApplicationDbContext _db;

    public EventEmailTemplateService(ApplicationDbContext db) => _db = db;

    public async Task<(string Subject, string Body)> RenderAsync(
        string eventKey,
        string defaultSubject,
        string defaultBody,
        IDictionary<string, string>? vars = null)
    {
        var tpl = await _db.EventEmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventKey == eventKey && x.IsActive);

        var subject = tpl?.SubjectTemplate ?? defaultSubject;
        var body = tpl?.BodyTemplate ?? defaultBody;

        if (vars == null || vars.Count == 0)
            return (subject, body);

        foreach (var kv in vars)
        {
            var token = "{{" + kv.Key + "}}";
            subject = subject.Replace(token, kv.Value ?? "", StringComparison.OrdinalIgnoreCase);
            body = body.Replace(token, kv.Value ?? "", StringComparison.OrdinalIgnoreCase);
        }

        return (subject, body);
    }
}

