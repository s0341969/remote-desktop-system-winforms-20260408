using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class RemoteViewerFormFactory
{
    private readonly DeviceBroker _deviceBroker;

    public RemoteViewerFormFactory(DeviceBroker deviceBroker)
    {
        _deviceBroker = deviceBroker;
    }

    public RemoteViewerForm Create(DeviceRecord device, string viewerName)
    {
        var form = new RemoteViewerForm();
        form.Bind(device, viewerName, _deviceBroker);
        return form;
    }
}
