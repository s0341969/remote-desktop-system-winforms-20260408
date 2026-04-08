using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Forms.Settings;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class MainFormFactory
{
    private readonly IDeviceRepository _repository;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly RemoteViewerFormFactory _remoteViewerFormFactory;
    private readonly HostSettingsFormFactory _hostSettingsFormFactory;

    public MainFormFactory(
        IDeviceRepository repository,
        IOptions<ControlServerOptions> options,
        RemoteViewerFormFactory remoteViewerFormFactory,
        HostSettingsFormFactory hostSettingsFormFactory)
    {
        _repository = repository;
        _options = options;
        _remoteViewerFormFactory = remoteViewerFormFactory;
        _hostSettingsFormFactory = hostSettingsFormFactory;
    }

    public MainForm Create(string signedInUserName)
    {
        var form = new MainForm();
        form.Bind(_repository, _options.Value, _remoteViewerFormFactory, _hostSettingsFormFactory, signedInUserName);
        return form;
    }
}
