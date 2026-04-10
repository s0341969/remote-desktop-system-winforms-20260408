using RemoteDesktop.Host.Services.Auditing;

namespace RemoteDesktop.Host.Forms.Audit;

public sealed class AuditLogForm : Form
{
    private readonly DataGridView _gridLogs;
    private readonly Button _btnRefresh;
    private readonly Button _btnClose;
    private readonly Label _lblStatus;
    private IAuditService? _auditService;

    public AuditLogForm()
    {
        Text = HostUiText.Window("稽核紀錄", "Audit Log");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 560);
        Width = 1100;
        Height = 660;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        Controls.Add(root);

        var header = new Panel
        {
            Dock = DockStyle.Fill
        };
        root.Controls.Add(header, 0, 0);

        var lblTitle = new Label
        {
            Text = HostUiText.Bi("稽核紀錄", "Audit Log"),
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font("Microsoft JhengHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point)
        };
        header.Controls.Add(lblTitle);

        _btnRefresh = new Button
        {
            Name = "btnRefreshAudit",
            Text = HostUiText.Bi("重新整理", "Refresh"),
            Width = 110,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(940, 2)
        };
        _btnRefresh.Click += async (_, _) => await RefreshAsync();
        header.Controls.Add(_btnRefresh);

        _gridLogs = new DataGridView
        {
            Name = "gridAuditLogs",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OccurredAt", HeaderText = HostUiText.Bi("發生時間", "Occurred at"), FillWeight = 135F });
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Actor", HeaderText = HostUiText.Bi("執行者", "Actor"), FillWeight = 120F });
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Action", HeaderText = HostUiText.Bi("動作", "Action"), FillWeight = 110F });
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Target", HeaderText = HostUiText.Bi("目標", "Target"), FillWeight = 120F });
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Succeeded", HeaderText = HostUiText.Bi("結果", "Result"), FillWeight = 70F });
        _gridLogs.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Details", HeaderText = HostUiText.Bi("詳細資訊", "Details"), FillWeight = 245F });
        root.Controls.Add(_gridLogs, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        root.Controls.Add(footer, 0, 2);

        _lblStatus = new Label
        {
            Name = "lblAuditStatus",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        };
        footer.Controls.Add(_lblStatus, 0, 0);

        _btnClose = new Button
        {
            Name = "btnCloseAudit",
            Text = HostUiText.Bi("關閉", "Close"),
            DialogResult = DialogResult.OK,
            Width = 96,
            Anchor = AnchorStyles.Right
        };
        footer.Controls.Add(_btnClose, 1, 0);
    }

    public void Bind(IAuditService auditService)
    {
        _auditService = auditService;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_auditService is null)
        {
            _lblStatus.Text = HostUiText.Bi("稽核服務尚未初始化。", "Audit service is not initialized.");
            return;
        }

        try
        {
            _btnRefresh.Enabled = false;
            _lblStatus.Text = HostUiText.Bi("正在載入稽核紀錄...", "Loading audit entries...");
            var items = await _auditService.GetRecentAsync(250, CancellationToken.None);
            _gridLogs.DataSource = items
                .Select(static item => new AuditGridItem(item))
                .ToList();
            _lblStatus.Text = HostUiText.Bi($"已載入 {items.Count} 筆稽核紀錄。", $"Loaded {items.Count} audit entries.");
        }
        catch (Exception exception)
        {
            _lblStatus.Text = HostUiText.Bi($"載入失敗：{exception.Message}", $"Load failed: {exception.Message}");
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    private sealed class AuditGridItem
    {
        public AuditGridItem(Models.AuditLogEntry entry)
        {
            OccurredAt = entry.OccurredAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            Actor = string.IsNullOrWhiteSpace(entry.ActorDisplayName)
                ? entry.ActorUserName
                : $"{entry.ActorDisplayName} ({entry.ActorUserName})";
            Action = FormatAction(entry.Action);
            Target = FormatTarget(entry.TargetType, entry.TargetId);
            Succeeded = entry.Succeeded ? HostUiText.Bi("成功", "Success") : HostUiText.Bi("失敗", "Failed");
            Details = entry.Details;
        }

        public string OccurredAt { get; }
        public string Actor { get; }
        public string Action { get; }
        public string Target { get; }
        public string Succeeded { get; }
        public string Details { get; }
    }

    private static string FormatAction(string action)
    {
        return action switch
        {
            "user-sign-in" => HostUiText.Bi("使用者登入", "User sign-in"),
            "host-settings-save" => HostUiText.Bi("儲存 Host 設定", "Save Host settings"),
            "user-account-create" => HostUiText.Bi("建立使用者帳號", "Create user account"),
            "user-account-update" => HostUiText.Bi("更新使用者帳號", "Update user account"),
            "user-account-save" => HostUiText.Bi("儲存使用者帳號", "Save user account"),
            "user-account-delete" => HostUiText.Bi("刪除使用者帳號", "Delete user account"),
            "viewer-session-open" => HostUiText.Bi("開啟 Viewer 工作階段", "Open viewer session"),
            "viewer-session-close" => HostUiText.Bi("關閉 Viewer 工作階段", "Close viewer session"),
            "viewer-remote-control" => HostUiText.Bi("送出遠端控制輸入", "Send remote control input"),
            "file-upload-start" => HostUiText.Bi("開始檔案上傳", "Start file upload"),
            "file-upload-complete" => HostUiText.Bi("完成檔案上傳", "Complete file upload"),
            "file-download-start" => HostUiText.Bi("開始檔案下載", "Start file download"),
            "file-download-complete" => HostUiText.Bi("完成檔案下載", "Complete file download"),
            "clipboard-set-start" => HostUiText.Bi("開始送出剪貼簿", "Start clipboard send"),
            "clipboard-set-complete" => HostUiText.Bi("完成送出剪貼簿", "Complete clipboard send"),
            "clipboard-get-start" => HostUiText.Bi("開始取得剪貼簿", "Start clipboard fetch"),
            "clipboard-get-complete" => HostUiText.Bi("完成取得剪貼簿", "Complete clipboard fetch"),
            "device-authorization-grant" => HostUiText.Bi("核准裝置授權", "Grant device authorization"),
            "device-authorization-revoke" => HostUiText.Bi("撤銷裝置授權", "Revoke device authorization"),
            _ => action
        };
    }

    private static string FormatTarget(string targetType, string? targetId)
    {
        var displayTargetType = targetType switch
        {
            "console" => HostUiText.Bi("主控台", "Console"),
            "device" => HostUiText.Bi("裝置", "Device"),
            "host-settings" => HostUiText.Bi("Host 設定", "Host settings"),
            "user-account" => HostUiText.Bi("使用者帳號", "User account"),
            _ => targetType
        };

        return string.IsNullOrWhiteSpace(targetId)
            ? displayTargetType
            : $"{displayTargetType}: {targetId}";
    }
}
