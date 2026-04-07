using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class MainFormFactory
{
    private readonly IDeviceRepository _repository;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly RemoteViewerFormFactory _remoteViewerFormFactory;

    public MainFormFactory(
        IDeviceRepository repository,
        IOptions<ControlServerOptions> options,
        RemoteViewerFormFactory remoteViewerFormFactory)
    {
        _repository = repository;
        _options = options;
        _remoteViewerFormFactory = remoteViewerFormFactory;
    }

    public MainForm Create(string signedInUserName)
    {
        var form = new MainForm();
        form.Bind(_repository, _options.Value, _remoteViewerFormFactory, signedInUserName);
        return form;
    }
}
