using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class RemoteViewerFormFactory
{
    private readonly DeviceBroker _deviceBroker;
    private readonly FileTransferTraceService _fileTransferTraceService;

    public RemoteViewerFormFactory(DeviceBroker deviceBroker, FileTransferTraceService fileTransferTraceService)
    {
        _deviceBroker = deviceBroker;
        _fileTransferTraceService = fileTransferTraceService;
    }

    public RemoteViewerForm Create(DeviceRecord device, AuthenticatedUserSession viewer)
    {
        var form = new RemoteViewerForm();
        form.Bind(device, viewer, _deviceBroker, _fileTransferTraceService);
        return form;
    }
}
