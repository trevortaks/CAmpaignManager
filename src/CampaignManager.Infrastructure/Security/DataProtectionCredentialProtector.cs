using System.Text.Json;
using CampaignManager.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace CampaignManager.Infrastructure.Security;

/// <summary>Encrypts provider credentials with ASP.NET Data Protection.
/// Keys are persisted to a shared directory so Api, Workers and AdminUI can all decrypt.
/// Losing the key ring makes stored credentials unrecoverable — back it up.</summary>
public sealed class DataProtectionCredentialProtector : ICredentialProtector
{
    public const string Purpose = "CampaignManager.ProviderCredentials.v1";

    private readonly IDataProtector _protector;

    public DataProtectionCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public byte[] Protect(IReadOnlyDictionary<string, string> credentials)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(credentials);
        return _protector.Protect(json);
    }

    public IReadOnlyDictionary<string, string> Unprotect(byte[] encrypted)
    {
        var json = _protector.Unprotect(encrypted);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }
}
