using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Forms;

public sealed class LoginFormFactory
{
    private readonly AuthenticationService _authenticationService;
    private readonly AuditService _auditService;
    private readonly IOptions<ControlServerOptions> _options;

    public LoginFormFactory(AuthenticationService authenticationService, AuditService auditService, IOptions<ControlServerOptions> options)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
        _options = options;
    }

    public LoginForm Create()
    {
        var form = new LoginForm();
        form.Bind(_authenticationService, _auditService, _options.Value);
        return form;
    }
}
