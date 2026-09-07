namespace NativeTavern.Security;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}
