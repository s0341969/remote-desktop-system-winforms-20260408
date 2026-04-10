using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Forms.Users;

public sealed class UserManagementFormFactory
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAuditService _auditService;

    public UserManagementFormFactory(IAuthenticationService authenticationService, IAuditService auditService)
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
