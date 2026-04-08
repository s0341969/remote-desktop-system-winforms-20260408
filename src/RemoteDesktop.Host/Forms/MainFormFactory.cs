using RemoteDesktop.Host.Forms.Audit;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Forms.Users;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class MainFormFactory
{
    private readonly IDeviceRepository _repository;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly DeviceBroker _deviceBroker;
    private readonly RemoteViewerFormFactory _remoteViewerFormFactory;
    private readonly HostSettingsFormFactory _hostSettingsFormFactory;
    private readonly UserManagementFormFactory _userManagementFormFactory;
    private readonly AuditLogFormFactory _auditLogFormFactory;

    public MainFormFactory(
        IDeviceRepository repository,
        IOptions<ControlServerOptions> options,
        DeviceBroker deviceBroker,
        RemoteViewerFormFactory remoteViewerFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory,
        UserManagementFormFactory userManagementFormFactory,
        AuditLogFormFactory auditLogFormFactory)
    {
        _repository = repository;
        _options = options;
        _deviceBroker = deviceBroker;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _hostSettingsFormFactory = hostSettingsFormFactory;
        _userManagementFormFactory = userManagementFormFactory;
        _auditLogFormFactory = auditLogFormFactory;
    }

    public MainForm Create(AuthenticatedUserSession signedInUser)
    {
        var form = new MainForm();
        form.Bind(_repository, _options.Value, _deviceBroker, _remoteViewerFormFactory, _hostSettingsFormFactory, _userManagementFormFactory, _auditLogFormFactory, signedInUser);
        return form;
    }
}
