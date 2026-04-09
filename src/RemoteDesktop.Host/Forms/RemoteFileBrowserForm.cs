using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Forms;

public class RemoteFileBrowserForm : Form
{
    private readonly Func<string?, CancellationToken, Task<RemoteDirectoryListingResult>> _loadDirectoryAsync;
    private readonly Func<string, string, CancellationToken, Task<RemoteMoveResult>>? _moveEntryAsync;
    private readonly string? _initialPath;
    private readonly TextBox _txtPath;
    private readonly Button _btnLoad;
    private readonly Button _btnRefresh;
    private readonly Button _btnUp;
    private readonly Button _btnMove;
    private readonly Button _btnDownload;
    private readonly Button _btnCancel;
    private readonly Label _lblStatus;
    private readonly Label _lblHints;
    private readonly ListView _listEntries;
    private readonly ContextMenuStrip _entriesMenu;
    private readonly ToolStripMenuItem _menuRefresh;
    private readonly ToolStripMenuItem _menuMove;
    private readonly ToolStripMenuItem _menuDownload;
    private string? _currentDirectoryPath;
    private string? _parentDirectoryPath;
    private bool _isLoading;

    public RemoteFileBrowserForm(
        string? initialPath,
        Func<string?, CancellationToken, Task<RemoteDirectoryListingResult>> loadDirectoryAsync,
        Func<string, string, CancellationToken, Task<RemoteMoveResult>>? moveEntryAsync = null)
    {
        _initialPath = initialPath;
        _loadDirectoryAsync = loadDirectoryAsync;
        _moveEntryAsync = moveEntryAsync;

        Text = HostUiText.Window("遠端檔案總管", "Remote File Browser");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 580);
        ClientSize = new Size(1020, 660);
        KeyPreview = true;

        var panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 168
        };

        var lblPath = new Label
        {
            AutoSize = false,
            Location = new Point(16, 12),
            Size = new Size(120, 44),
            Text = HostUiText.Bi("遠端路徑", "Remote Path")
        };

        _txtPath = new TextBox
        {
            Location = new Point(142, 18),
            Size = new Size(620, 23),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Name = "txtPath"
        };

        _btnLoad = new Button
        {
            Location = new Point(774, 12),
            Size = new Size(116, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnLoad"
        };
        HostUiText.ApplyButton(_btnLoad, "載入", "Load");
        _btnLoad.Click += async (_, _) => await LoadFromPathBoxAsync();

        _lblStatus = new Label
        {
            AutoSize = false,
            Location = new Point(16, 54),
            Size = new Size(974, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Name = "lblStatus",
            Text = HostUiText.Bi("等待載入遠端資料夾。", "Waiting to load the remote directory.")
        };

        _btnRefresh = new Button
        {
            Location = new Point(16, 90),
            Size = new Size(130, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Name = "btnRefresh"
        };
        HostUiText.ApplyButton(_btnRefresh, "重新整理", "Refresh", minHeight: 36);
        _btnRefresh.Click += async (_, _) => await RefreshCurrentDirectoryAsync();

        _btnUp = new Button
        {
            Location = new Point(152, 90),
            Size = new Size(120, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Name = "btnUp"
        };
        HostUiText.ApplyButton(_btnUp, "上一層", "Up", minHeight: 36);
        _btnUp.Click += async (_, _) => await LoadDirectoryAsync(_parentDirectoryPath);

        _btnMove = new Button
        {
            Location = new Point(278, 90),
            Size = new Size(180, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Name = "btnMove"
        };
        HostUiText.ApplyButton(_btnMove, "移動所選項目", "Move Selected", minHeight: 36);
        _btnMove.Click += async (_, _) => await MoveSelectedEntryAsync();

        _btnDownload = new Button
        {
            Location = new Point(464, 90),
            Size = new Size(190, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Name = "btnDownload"
        };
        HostUiText.ApplyButton(_btnDownload, "下載所選檔案", "Download Selected", minHeight: 36);
        _btnDownload.Click += (_, _) => ConfirmSelectedFile();

        _lblHints = new Label
        {
            AutoSize = false,
            Location = new Point(16, 132),
            Size = new Size(974, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Name = "lblHints",
            Text = HostUiText.Bi("提示：F5 重新整理，雙擊資料夾可進入。", "Hint: Press F5 to refresh. Double-click a folder to open it.")
        };

        panelTop.Controls.Add(lblPath);
        panelTop.Controls.Add(_txtPath);
        panelTop.Controls.Add(_btnLoad);
        panelTop.Controls.Add(_lblStatus);
        panelTop.Controls.Add(_btnRefresh);
        panelTop.Controls.Add(_btnUp);
        panelTop.Controls.Add(_btnMove);
        panelTop.Controls.Add(_btnDownload);
        panelTop.Controls.Add(_lblHints);

        _entriesMenu = new ContextMenuStrip();
        _menuRefresh = new ToolStripMenuItem(HostUiText.Window("重新整理", "Refresh"));
        _menuMove = new ToolStripMenuItem(HostUiText.Window("移動所選項目", "Move Selected"));
        _menuDownload = new ToolStripMenuItem(HostUiText.Window("下載所選檔案", "Download Selected"));
        _menuRefresh.Click += async (_, _) => await RefreshCurrentDirectoryAsync();
        _menuMove.Click += async (_, _) => await MoveSelectedEntryAsync();
        _menuDownload.Click += (_, _) => ConfirmSelectedFile();
        _entriesMenu.Items.AddRange(new ToolStripItem[]
        {
            _menuRefresh,
            _menuMove,
            _menuDownload
        });

        _listEntries = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            View = View.Details,
            HideSelection = false,
            Name = "listEntries",
            ContextMenuStrip = _entriesMenu
        };
        _listEntries.Columns.Add(HostUiText.Window("名稱", "Name"), 350);
        _listEntries.Columns.Add(HostUiText.Window("類型", "Type"), 130);
        _listEntries.Columns.Add(HostUiText.Window("大小", "Size"), 120);
        _listEntries.Columns.Add(HostUiText.Window("修改時間", "Modified"), 220);
        _listEntries.SelectedIndexChanged += (_, _) => SyncSelectionState();
        _listEntries.ItemActivate += async (_, _) => await HandleItemActivateAsync();

        var panelBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56
        };

        _btnCancel = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(876, 8),
            Size = new Size(114, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnCancel"
        };
        HostUiText.ApplyButton(_btnCancel, "取消", "Cancel", minHeight: 40);

        panelBottom.Controls.Add(_btnCancel);

        Controls.Add(_listEntries);
        Controls.Add(panelBottom);
        Controls.Add(panelTop);

        AcceptButton = _btnDownload;
        CancelButton = _btnCancel;
        SyncSelectionState();
    }

    public string? SelectedFilePath { get; private set; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadDirectoryAsync(_initialPath);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            _ = RefreshCurrentDirectoryAsync();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task LoadFromPathBoxAsync()
    {
        await LoadDirectoryAsync(_txtPath.Text.Trim());
    }

    private Task RefreshCurrentDirectoryAsync()
    {
        return LoadDirectoryAsync(_currentDirectoryPath);
    }

    private async Task LoadDirectoryAsync(string? path)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        SyncSelectionState();
        _lblStatus.Text = HostUiText.Bi("正在載入遠端資料夾...", "Loading remote directory...");

        try
        {
            var result = await _loadDirectoryAsync(path, CancellationToken.None);
            _currentDirectoryPath = result.DirectoryPath;
            _parentDirectoryPath = result.CanNavigateUp ? result.ParentDirectoryPath : null;
            _txtPath.Text = result.DirectoryPath;
            _lblStatus.Text = result.Message;
            PopulateEntries(result.Entries);

            if (result.EntriesTruncated)
            {
                _lblStatus.Text = HostUiText.Bi(
                    $"{result.Message} 目前只顯示前 2,000 個項目。",
                    $"{result.Message} Only the first 2,000 items are shown.");
            }
        }
        catch (Exception exception)
        {
            var errorMessage = exception.Message;
            _lblStatus.Text = errorMessage;
            MessageBox.Show(
                errorMessage,
                HostUiText.Window("遠端檔案總管", "Remote File Browser"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isLoading = false;
            SyncSelectionState();
        }
    }

    private void PopulateEntries(IReadOnlyList<RemoteFileBrowserEntry> entries)
    {
        _listEntries.BeginUpdate();
        try
        {
            _listEntries.Items.Clear();
            foreach (var entry in entries
                         .OrderByDescending(static item => item.IsDirectory)
                         .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem(entry.Name)
                {
                    Tag = entry
                };

                item.SubItems.Add(entry.IsDirectory
                    ? HostUiText.Window("資料夾", "Folder")
                    : HostUiText.Window("檔案", "File"));
                item.SubItems.Add(entry.IsDirectory ? string.Empty : FormatBytes(entry.Size));
                item.SubItems.Add(entry.LastModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                _listEntries.Items.Add(item);
            }
        }
        finally
        {
            _listEntries.EndUpdate();
        }
    }

    private async Task HandleItemActivateAsync()
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await LoadDirectoryAsync(entry.FullPath);
            return;
        }

        ConfirmSelectedFile();
    }

    private async Task MoveSelectedEntryAsync()
    {
        if (_moveEntryAsync is null)
        {
            return;
        }

        var entry = GetSelectedEntry();
        if (entry is null)
        {
            return;
        }

        var destinationDirectoryPath = await SelectMoveDestinationDirectoryAsync(this, entry.FullPath, _currentDirectoryPath);
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            return;
        }

        _isLoading = true;
        SyncSelectionState();
        _lblStatus.Text = HostUiText.Bi(
            $"正在移動「{entry.Name}」...",
            $"Moving '{entry.Name}'...");
        var shouldRefreshCurrentDirectory = false;

        try
        {
            var result = await MoveEntryAsyncCore(entry.FullPath, destinationDirectoryPath);
            _lblStatus.Text = result.Message;
            shouldRefreshCurrentDirectory = true;
        }
        catch (Exception exception)
        {
            var errorMessage = exception.Message;
            _lblStatus.Text = errorMessage;
            MessageBox.Show(
                errorMessage,
                HostUiText.Window("遠端檔案總管", "Remote File Browser"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isLoading = false;
            SyncSelectionState();
        }

        if (shouldRefreshCurrentDirectory)
        {
            await RefreshCurrentDirectoryAsync();
        }
    }

    protected virtual Task<RemoteMoveResult> MoveEntryAsyncCore(string sourcePath, string destinationDirectoryPath)
    {
        if (_moveEntryAsync is null)
        {
            throw new InvalidOperationException(HostUiText.Bi("目前不支援移動遠端項目。", "Moving remote items is not available."));
        }

        return _moveEntryAsync(sourcePath, destinationDirectoryPath, CancellationToken.None);
    }

    protected virtual Task<string?> SelectMoveDestinationDirectoryAsync(IWin32Window owner, string sourcePath, string? currentDirectoryPath)
    {
        var sourceName = GetPathDisplayName(sourcePath);
        using var dialog = new RemoteFolderPickerForm(
            currentDirectoryPath,
            _loadDirectoryAsync,
            HostUiText.Bi(
                $"請選擇「{sourceName}」的目的資料夾。",
                $"Choose a destination folder for '{sourceName}'."));
        return Task.FromResult(dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.SelectedDirectoryPath
            : null);
    }

    private void ConfirmSelectedFile()
    {
        var entry = GetSelectedEntry();
        if (entry is null || entry.IsDirectory)
        {
            return;
        }

        SelectedFilePath = entry.FullPath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private RemoteFileBrowserEntry? GetSelectedEntry()
    {
        return _listEntries.SelectedItems.Count > 0
            ? _listEntries.SelectedItems[0].Tag as RemoteFileBrowserEntry
            : null;
    }

    private void SyncSelectionState()
    {
        var selectedEntry = GetSelectedEntry();
        var canRefresh = !_isLoading && !string.IsNullOrWhiteSpace(_currentDirectoryPath);
        var canNavigateUp = !_isLoading && !string.IsNullOrWhiteSpace(_parentDirectoryPath);
        var canDownload = !_isLoading && selectedEntry is { IsDirectory: false };
        var canMove = !_isLoading && _moveEntryAsync is not null && selectedEntry is not null;

        _btnLoad.Enabled = !_isLoading;
        _btnRefresh.Enabled = canRefresh;
        _btnUp.Enabled = canNavigateUp;
        _btnDownload.Enabled = canDownload;
        _btnMove.Enabled = canMove;
        _menuRefresh.Enabled = canRefresh;
        _menuMove.Enabled = canMove;
        _menuDownload.Enabled = canDownload;
    }

    private static string GetPathDisplayName(string path)
    {
        var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmedPath);
    }

    private static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double display = bytes;
        var unitIndex = 0;
        while (display >= 1024d && unitIndex < units.Length - 1)
        {
            display /= 1024d;
            unitIndex++;
        }

        return $"{display:0.##} {units[unitIndex]}";
    }
}

internal sealed class RemoteFolderPickerForm : Form
{
    private readonly Func<string?, CancellationToken, Task<RemoteDirectoryListingResult>> _loadDirectoryAsync;
    private readonly string? _initialPath;
    private readonly TextBox _txtPath;
    private readonly Label _lblStatus;
    private readonly ListView _listDirectories;
    private readonly Button _btnLoad;
    private readonly Button _btnRefresh;
    private readonly Button _btnUp;
    private readonly Button _btnSelectCurrent;
    private string? _currentDirectoryPath;
    private string? _parentDirectoryPath;
    private bool _isLoading;

    public RemoteFolderPickerForm(
        string? initialPath,
        Func<string?, CancellationToken, Task<RemoteDirectoryListingResult>> loadDirectoryAsync,
        string instructionText)
    {
        _initialPath = initialPath;
        _loadDirectoryAsync = loadDirectoryAsync;

        Text = HostUiText.Window("選擇目的資料夾", "Choose Destination Folder");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 520);
        ClientSize = new Size(960, 600);
        KeyPreview = true;

        var panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112
        };

        var lblInstruction = new Label
        {
            AutoSize = false,
            Location = new Point(16, 10),
            Size = new Size(928, 34),
            Text = instructionText
        };

        var lblPath = new Label
        {
            AutoSize = false,
            Location = new Point(16, 50),
            Size = new Size(120, 44),
            Text = HostUiText.Bi("遠端路徑", "Remote Path")
        };

        _txtPath = new TextBox
        {
            Location = new Point(142, 56),
            Size = new Size(520, 23),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Name = "txtFolderPath"
        };

        _btnLoad = new Button
        {
            Location = new Point(676, 50),
            Size = new Size(82, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnFolderLoad"
        };
        HostUiText.ApplyButton(_btnLoad, "載入", "Load");
        _btnLoad.Click += async (_, _) => await LoadFromPathBoxAsync();

        _btnRefresh = new Button
        {
            Location = new Point(764, 50),
            Size = new Size(104, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnFolderRefresh"
        };
        HostUiText.ApplyButton(_btnRefresh, "重新整理", "Refresh");
        _btnRefresh.Click += async (_, _) => await LoadDirectoryAsync(_currentDirectoryPath);

        _btnUp = new Button
        {
            Location = new Point(874, 50),
            Size = new Size(70, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnFolderUp"
        };
        HostUiText.ApplyButton(_btnUp, "上一層", "Up");
        _btnUp.Click += async (_, _) => await LoadDirectoryAsync(_parentDirectoryPath);

        panelTop.Controls.Add(lblInstruction);
        panelTop.Controls.Add(lblPath);
        panelTop.Controls.Add(_txtPath);
        panelTop.Controls.Add(_btnLoad);
        panelTop.Controls.Add(_btnRefresh);
        panelTop.Controls.Add(_btnUp);

        _lblStatus = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 26,
            Name = "lblFolderStatus",
            Padding = new Padding(16, 4, 16, 0),
            Text = HostUiText.Bi("等待載入遠端資料夾。", "Waiting to load the remote directory.")
        };

        _listDirectories = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            View = View.Details,
            HideSelection = false,
            Name = "listDirectories"
        };
        _listDirectories.Columns.Add(HostUiText.Window("名稱", "Name"), 500);
        _listDirectories.Columns.Add(HostUiText.Window("修改時間", "Modified"), 240);
        _listDirectories.SelectedIndexChanged += (_, _) => SyncSelectionState();
        _listDirectories.ItemActivate += async (_, _) => await OpenSelectedDirectoryAsync();

        var panelBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72
        };

        _btnSelectCurrent = new Button
        {
            Location = new Point(694, 14),
            Size = new Size(170, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnSelectCurrentFolder"
        };
        HostUiText.ApplyButton(_btnSelectCurrent, "移動到這裡", "Move Here");
        _btnSelectCurrent.Click += (_, _) => ConfirmCurrentDirectory();

        var btnCancel = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(876, 14),
            Size = new Size(68, 44),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Name = "btnFolderCancel"
        };
        HostUiText.ApplyButton(btnCancel, "取消", "Cancel");

        panelBottom.Controls.Add(_btnSelectCurrent);
        panelBottom.Controls.Add(btnCancel);

        Controls.Add(_listDirectories);
        Controls.Add(panelBottom);
        Controls.Add(_lblStatus);
        Controls.Add(panelTop);

        CancelButton = btnCancel;
        SyncSelectionState();
    }

    public string? SelectedDirectoryPath { get; private set; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadDirectoryAsync(_initialPath);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5)
        {
            _ = LoadDirectoryAsync(_currentDirectoryPath);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task LoadFromPathBoxAsync()
    {
        await LoadDirectoryAsync(_txtPath.Text.Trim());
    }

    private async Task LoadDirectoryAsync(string? path)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        SyncSelectionState();
        _lblStatus.Text = HostUiText.Bi("正在載入遠端資料夾...", "Loading remote directory...");

        try
        {
            var result = await _loadDirectoryAsync(path, CancellationToken.None);
            _currentDirectoryPath = result.DirectoryPath;
            _parentDirectoryPath = result.CanNavigateUp ? result.ParentDirectoryPath : null;
            _txtPath.Text = result.DirectoryPath;
            _lblStatus.Text = result.Message;

            _listDirectories.BeginUpdate();
            try
            {
                _listDirectories.Items.Clear();
                foreach (var entry in result.Entries
                             .Where(static item => item.IsDirectory)
                             .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var item = new ListViewItem(entry.Name)
                    {
                        Tag = entry
                    };
                    item.SubItems.Add(entry.LastModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                    _listDirectories.Items.Add(item);
                }
            }
            finally
            {
                _listDirectories.EndUpdate();
            }
        }
        catch (Exception exception)
        {
            var errorMessage = exception.Message;
            _lblStatus.Text = errorMessage;
            MessageBox.Show(
                errorMessage,
                HostUiText.Window("選擇目的資料夾", "Choose Destination Folder"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isLoading = false;
            SyncSelectionState();
        }
    }

    private async Task OpenSelectedDirectoryAsync()
    {
        if (_listDirectories.SelectedItems.Count == 0)
        {
            return;
        }

        if (_listDirectories.SelectedItems[0].Tag is not RemoteFileBrowserEntry entry)
        {
            return;
        }

        await LoadDirectoryAsync(entry.FullPath);
    }

    private void ConfirmCurrentDirectory()
    {
        if (string.IsNullOrWhiteSpace(_currentDirectoryPath))
        {
            return;
        }

        SelectedDirectoryPath = _currentDirectoryPath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SyncSelectionState()
    {
        _btnLoad.Enabled = !_isLoading;
        _btnRefresh.Enabled = !_isLoading && !string.IsNullOrWhiteSpace(_currentDirectoryPath);
        _btnUp.Enabled = !_isLoading && !string.IsNullOrWhiteSpace(_parentDirectoryPath);
        _btnSelectCurrent.Enabled = !_isLoading && !string.IsNullOrWhiteSpace(_currentDirectoryPath);
    }
}
