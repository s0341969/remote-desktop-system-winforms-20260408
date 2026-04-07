using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services;

public sealed class CredentialValidator
{
    private readonly ControlServerOptions _options;

    public CredentialValidator(IOptions<ControlServerOptions> options)
    {
        _options = options.Value;
    }

    public bool Validate(string userName, string password)
    {
        return FixedTimeEquals(userName, _options.AdminUserName)
            && FixedTimeEquals(password, _options.AdminPassword);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
