using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class RemoteViewerFormFactory
{
    private readonly RemoteViewerSessionBrokerFactory _remoteViewerSessionBrokerFactory;
    private readonly FileTransferTraceService _fileTransferTraceService;

    public RemoteViewerFormFactory(RemoteViewerSessionBrokerFactory remoteViewerSessionBrokerFactory, FileTransferTraceService fileTransferTraceService)
    {
        _remoteViewerSessionBrokerFactory = remoteViewerSessionBrokerFactory;
        _fileTransferTraceService = fileTransferTraceService;
    }

    public RemoteViewerForm Create(DeviceRecord device, AuthenticatedUserSession viewer)
    {
        var form = new RemoteViewerForm();
        form.Bind(device, viewer, _remoteViewerSessionBrokerFactory.Create(), _fileTransferTraceService);
        return form;
    }
}
