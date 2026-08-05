namespace CampaignManager.Application.Abstractions;

/// <summary>Encrypts/decrypts provider credential dictionaries at rest.</summary>
public interface ICredentialProtector
{
    byte[] Protect(IReadOnlyDictionary<string, string> credentials);
    IReadOnlyDictionary<string, string> Unprotect(byte[] encrypted);
}
