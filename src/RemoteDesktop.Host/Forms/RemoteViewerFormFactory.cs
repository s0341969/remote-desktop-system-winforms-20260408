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

    public RemoteViewerForm Create(DeviceRecord device, AuthenticatedUserSession viewer)
    {
        var form = new RemoteViewerForm();
        form.Bind(device, viewer, _deviceBroker);
        return form;
    }
}
