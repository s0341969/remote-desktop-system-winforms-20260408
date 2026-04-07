using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class LoginFormFactory
{
    private readonly CredentialValidator _credentialValidator;
    private readonly IOptions<ControlServerOptions> _options;

    public LoginFormFactory(CredentialValidator credentialValidator, IOptions<ControlServerOptions> options)
    {
        _credentialValidator = credentialValidator;
        _options = options;
    }

    public LoginForm Create()
    {
        var form = new LoginForm();
        form.Bind(_credentialValidator, _options.Value);
        return form;
    }
}
