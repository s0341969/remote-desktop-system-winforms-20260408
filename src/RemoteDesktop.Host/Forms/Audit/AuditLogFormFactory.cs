using RemoteDesktop.Host.Services.Auditing;

namespace RemoteDesktop.Host.Forms.Audit;

public sealed class AuditLogFormFactory
{
    private readonly IAuditService _auditService;

    public AuditLogFormFactory(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public AuditLogForm Create()
    {
        var form = new AuditLogForm();
        form.Bind(_auditService);
        return form;
    }
}
