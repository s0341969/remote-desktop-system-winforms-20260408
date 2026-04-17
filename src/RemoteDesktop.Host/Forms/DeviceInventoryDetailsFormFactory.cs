using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public sealed class DeviceInventoryDetailsFormFactory
{
    private readonly MainDashboardDataSourceFactory _mainDashboardDataSourceFactory;
    private readonly InventoryExportService _inventoryExportService;

    public DeviceInventoryDetailsFormFactory(MainDashboardDataSourceFactory mainDashboardDataSourceFactory, InventoryExportService inventoryExportService)
    {
        _mainDashboardDataSourceFactory = mainDashboardDataSourceFactory;
        _inventoryExportService = inventoryExportService;
    }

    public DeviceInventoryDetailsForm Create(string deviceId)
    {
        return new DeviceInventoryDetailsForm(_mainDashboardDataSourceFactory.Create(), _inventoryExportService, deviceId);
    }
}
