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
    private readonly MainDashboardDataSourceFactory _mainDashboardDataSourceFactory;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly RemoteViewerFormFactory _remoteViewerFormFactory;
    private readonly DeviceInventoryDetailsFormFactory _deviceInventoryDetailsFormFactory;
    private readonly HostSettingsFormFactory _hostSettingsFormFactory;
    private readonly UserManagementFormFactory _userManagementFormFactory;
    private readonly AuditLogFormFactory _auditLogFormFactory;

    public MainFormFactory(
        MainDashboardDataSourceFactory mainDashboardDataSourceFactory,
        IOptions<ControlServerOptions> options,
        RemoteViewerFormFactory remoteViewerFormFactory,
        DeviceInventoryDetailsFormFactory deviceInventoryDetailsFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory,
        UserManagementFormFactory userManagementFormFactory,
        AuditLogFormFactory auditLogFormFactory)
    {
        _mainDashboardDataSourceFactory = mainDashboardDataSourceFactory;
        _options = options;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _deviceInventoryDetailsFormFactory = deviceInventoryDetailsFormFactory;
        _hostSettingsFormFactory = hostSettingsFormFactory;
        _userManagementFormFactory = userManagementFormFactory;
        _auditLogFormFactory = auditLogFormFactory;
    }

    public MainForm Create(AuthenticatedUserSession signedInUser)
    {
        var form = new MainForm();
        form.Bind(_mainDashboardDataSourceFactory.Create(), _options.Value, _remoteViewerFormFactory, _deviceInventoryDetailsFormFactory, _hostSettingsFormFactory, _userManagementFormFactory, _auditLogFormFactory, signedInUser);
        return form;
    }
}
