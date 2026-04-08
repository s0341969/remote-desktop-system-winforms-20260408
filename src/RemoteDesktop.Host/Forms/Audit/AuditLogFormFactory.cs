using RemoteDesktop.Host.Services.Auditing;

namespace RemoteDesktop.Host.Forms.Audit;

public sealed class AuditLogFormFactory
{
    private readonly AuditService _auditService;

    public AuditLogFormFactory(AuditService auditService)
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
