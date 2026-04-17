using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services;

namespace RemoteDesktop.Host.Forms;

public partial class DeviceInventoryDetailsForm : Form
{
    private readonly IMainDashboardDataSource _dashboardDataSource;
    private readonly InventoryExportService _inventoryExportService;
    private readonly string _deviceId;
    private DeviceRecord? _device;
    private IReadOnlyList<InventoryHistoryRecord> _history = [];

    public DeviceInventoryDetailsForm(IMainDashboardDataSource dashboardDataSource, InventoryExportService inventoryExportService, string deviceId)
    {
        _dashboardDataSource = dashboardDataSource;
        _inventoryExportService = inventoryExportService;
        _deviceId = deviceId;
        InitializeComponent();
        InitializeUiText();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await RefreshAsync(showErrorDialog: true);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        (_dashboardDataSource as IDisposable)?.Dispose();
        base.OnFormClosed(e);
    }

    private async void btnRefresh_Click(object sender, EventArgs e)
    {
        await RefreshAsync(showErrorDialog: true);
    }

    private async void btnExportCsv_Click(object sender, EventArgs e)
    {
        if (_device is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 檔案 (*.csv)|*.csv",
            FileName = $"{_device.DeviceId}_inventory_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = HostUiText.Window("匯出盤點 CSV", "Export inventory CSV")
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await _inventoryExportService.ExportCsvAsync(dialog.FileName, _device, _history, CancellationToken.None);
        lblStatusValue.Text = HostUiText.Bi($"CSV 已匯出：{dialog.FileName}", $"CSV exported: {dialog.FileName}");
    }

    private async void btnExportExcel_Click(object sender, EventArgs e)
    {
        if (_device is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx",
            FileName = $"{_device.DeviceId}_inventory_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            Title = HostUiText.Window("匯出盤點 Excel", "Export inventory Excel")
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await _inventoryExportService.ExportExcelAsync(dialog.FileName, _device, _history, CancellationToken.None);
        lblStatusValue.Text = HostUiText.Bi($"Excel 已匯出：{dialog.FileName}", $"Excel exported: {dialog.FileName}");
    }

    private void gridHistory_SelectionChanged(object sender, EventArgs e)
    {
        if (gridHistory.CurrentRow?.DataBoundItem is InventoryHistoryGridItem item)
        {
            BindSnapshotDetails(item.Source.Inventory);
        }
    }

    private async Task RefreshAsync(bool showErrorDialog)
    {
        try
        {
            btnRefresh.Enabled = false;
            btnExportCsv.Enabled = false;
            btnExportExcel.Enabled = false;
            lblStatusValue.Text = HostUiText.Bi("重新整理中...", "Refreshing...");

            _device = await _dashboardDataSource.GetDeviceAsync(_deviceId, CancellationToken.None);
            _history = await _dashboardDataSource.GetInventoryHistoryAsync(_deviceId, 200, CancellationToken.None);
            if (_device is null)
            {
                throw new InvalidOperationException(HostUiText.Bi("找不到裝置資料。", "The device could not be found."));
            }

            BindDeviceSummary(_device);
            BindCurrentInventory(_device.Inventory);
            BindHistory(_history);

            btnExportCsv.Enabled = true;
            btnExportExcel.Enabled = true;
            lblStatusValue.Text = HostUiText.Bi("就緒", "Ready");
        }
        catch (Exception exception)
        {
            lblStatusValue.Text = HostUiText.Bi("重新整理失敗", "Refresh failed");
            if (showErrorDialog)
            {
                MessageBox.Show(
                    HostUiText.Bi($"載入裝置詳細資訊失敗：{exception.Message}", $"Failed to load device details: {exception.Message}"),
                    HostUiText.Window("裝置詳細資訊", "Device Details"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            btnRefresh.Enabled = true;
        }
    }

    private void BindDeviceSummary(DeviceRecord device)
    {
        Text = AppBuildInfo.AppendToWindowTitle(HostUiText.Window($"裝置詳細資訊 - {device.DeviceName}", $"Device details - {device.DeviceName}"));
        lblDeviceIdValue.Text = device.DeviceId;
        lblDeviceNameValue.Text = device.DeviceName;
        lblHostNameValue.Text = device.HostName;
        lblAgentVersionValue.Text = device.AgentVersion;
        lblResolutionValue.Text = $"{device.ScreenWidth} x {device.ScreenHeight}";
        lblInventoryCollectedAtValue.Text = device.Inventory is null ? "-" : device.Inventory.CollectedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void BindCurrentInventory(AgentInventoryProfile? inventory)
    {
        gridCurrentInventory.DataSource = BuildInventoryDetailRows(inventory).ToList();
        BindSnapshotDetails(inventory);
    }

    private void BindSnapshotDetails(AgentInventoryProfile? inventory)
    {
        gridSnapshotDetails.DataSource = BuildInventoryDetailRows(inventory).ToList();
    }

    private void BindHistory(IReadOnlyList<InventoryHistoryRecord> history)
    {
        var items = history
            .OrderByDescending(static item => item.RecordedAt)
            .Select(static item => new InventoryHistoryGridItem(item))
            .ToList();
        gridHistory.DataSource = items;

        if (gridHistory.Rows.Count > 0)
        {
            gridHistory.Rows[0].Selected = true;
            gridHistory.CurrentCell = gridHistory.Rows[0].Cells[0];
        }
        else
        {
            BindSnapshotDetails(_device?.Inventory);
        }
    }

    private static IEnumerable<InventoryDetailRow> BuildInventoryDetailRows(AgentInventoryProfile? inventory)
    {
        if (inventory is null)
        {
            yield return new InventoryDetailRow("狀態", "尚未收到盤點資料");
            yield break;
        }

        yield return new InventoryDetailRow("盤點時間", inventory.CollectedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        yield return new InventoryDetailRow("CPU", inventory.CpuName);
        yield return new InventoryDetailRow("總記憶體", FormatBytes(inventory.InstalledMemoryBytes));
        yield return new InventoryDetailRow("磁碟摘要", inventory.StorageSummary);
        yield return new InventoryDetailRow("作業系統", $"{inventory.OsName} {inventory.OsVersion} ({inventory.OsBuildNumber})");
        yield return new InventoryDetailRow("Office", inventory.OfficeVersion);
        yield return new InventoryDetailRow("最後更新", $"{inventory.LastWindowsUpdateTitle} / {inventory.LastWindowsUpdateInstalledAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知日期"}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "未知";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private void InitializeUiText()
    {
        lblDeviceIdCaption.Text = HostUiText.Bi("裝置 ID", "Device ID");
        lblDeviceNameCaption.Text = HostUiText.Bi("裝置名稱", "Device name");
        lblHostNameCaption.Text = HostUiText.Bi("主機名稱", "Host name");
        lblAgentVersionCaption.Text = HostUiText.Bi("Agent 版本", "Agent version");
        lblResolutionCaption.Text = HostUiText.Bi("解析度", "Resolution");
        lblInventoryCollectedAtCaption.Text = HostUiText.Bi("最新盤點時間", "Latest collected");
        lblCurrentInventoryTitle.Text = HostUiText.Bi("目前盤點", "Current inventory");
        lblHistoryTitle.Text = HostUiText.Bi("變更追蹤", "Change history");
        lblSnapshotTitle.Text = HostUiText.Bi("選取版本明細", "Selected snapshot details");
        lblStatusCaption.Text = HostUiText.Bi("狀態", "Status");
        lblStatusValue.Text = HostUiText.Bi("就緒", "Ready");
        HostUiText.ApplyButton(btnRefresh, "重新整理", "Refresh");
        HostUiText.ApplyButton(btnExportCsv, "匯出 CSV", "Export CSV");
        HostUiText.ApplyButton(btnExportExcel, "匯出 Excel", "Export Excel");
        btnClose.Text = HostUiText.Window("關閉", "Close");

        if (gridCurrentInventory.Columns.Count >= 2)
        {
            gridCurrentInventory.Columns[0].HeaderText = HostUiText.Bi("欄位", "Field");
            gridCurrentInventory.Columns[1].HeaderText = HostUiText.Bi("值", "Value");
        }

        if (gridHistory.Columns.Count >= 4)
        {
            gridHistory.Columns[0].HeaderText = HostUiText.Bi("盤點時間", "Collected");
            gridHistory.Columns[1].HeaderText = HostUiText.Bi("記錄時間", "Recorded");
            gridHistory.Columns[2].HeaderText = HostUiText.Bi("摘要", "Summary");
            gridHistory.Columns[3].HeaderText = HostUiText.Bi("指紋", "Fingerprint");
        }

        if (gridSnapshotDetails.Columns.Count >= 2)
        {
            gridSnapshotDetails.Columns[0].HeaderText = HostUiText.Bi("欄位", "Field");
            gridSnapshotDetails.Columns[1].HeaderText = HostUiText.Bi("值", "Value");
        }
    }

    private sealed record InventoryDetailRow(string Field, string Value);

    private sealed class InventoryHistoryGridItem
    {
        public InventoryHistoryGridItem(InventoryHistoryRecord source)
        {
            Source = source;
            CollectedAt = source.CollectedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            RecordedAt = source.RecordedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            Summary = source.ChangeSummary;
            Fingerprint = source.InventoryFingerprint[..Math.Min(12, source.InventoryFingerprint.Length)];
        }

        public InventoryHistoryRecord Source { get; }

        public string CollectedAt { get; }

        public string RecordedAt { get; }

        public string Summary { get; }

        public string Fingerprint { get; }
    }
}
