using Microsoft.AspNetCore.DataProtection;

namespace HNControl.Web.Services;

public class SecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HNControl.ProjectSecrets.v1");
    }

    public string Protect(string plain)
        => string.IsNullOrEmpty(plain) ? "" : _protector.Protect(plain);

    public string Unprotect(string protectedValue)
    {
        try
        {
            return string.IsNullOrEmpty(protectedValue) ? "" : _protector.Unprotect(protectedValue);
        }
        catch
        {
            return "";
        }
    }
}
