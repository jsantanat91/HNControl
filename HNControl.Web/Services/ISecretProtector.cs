namespace HNControl.Web.Services;

public interface ISecretProtector
{
    string Protect(string plain);
    string Unprotect(string protectedValue);
}
