using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Forms.Users;

public sealed class UserManagementFormFactory
{
    private readonly AuthenticationService _authenticationService;
    private readonly AuditService _auditService;

    public UserManagementFormFactory(AuthenticationService authenticationService, AuditService auditService)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
    }

    public UserManagementForm Create(AuthenticatedUserSession currentUser)
    {
        var form = new UserManagementForm();
        form.Bind(_authenticationService, _auditService, currentUser);
        return form;
    }
}
