using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;

namespace RemoteDesktop.Host.Forms.Users;

public sealed class UserManagementForm : Form
{
    private readonly DataGridView _gridUsers;
    private readonly TextBox _txtUserName;
    private readonly TextBox _txtDisplayName;
    private readonly ComboBox _cboRole;
    private readonly TextBox _txtPassword;
    private readonly CheckBox _chkEnabled;
    private readonly Button _btnNew;
    private readonly Button _btnSave;
    private readonly Button _btnDelete;
    private readonly Button _btnClose;
    private readonly Label _lblStatus;

    private IAuthenticationService? _authenticationService;
    private IAuditService? _auditService;
    private AuthenticatedUserSession? _currentUser;
    private List<UserAccount> _accounts = new();
    private string? _selectedUserName;

    public UserManagementForm()
    {
        Text = HostUiText.Window("使用者管理", "User Management");
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 520);
        Width = 960;
        Height = 600;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        Controls.Add(root);

        var lblTitle = new Label
        {
            Text = HostUiText.Bi("本機帳號", "Local Accounts"),
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft JhengHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(lblTitle, 0, 0);
        root.SetColumnSpan(lblTitle, 2);

        _gridUsers = new DataGridView
        {
            Name = "gridUsers",
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UserName", HeaderText = HostUiText.Bi("帳號", "User name"), FillWeight = 120F });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplayName", HeaderText = HostUiText.Bi("顯示名稱", "Display name"), FillWeight = 140F });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Role", HeaderText = HostUiText.Bi("角色", "Role"), FillWeight = 90F });
        _gridUsers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "IsEnabled", HeaderText = HostUiText.Bi("啟用", "Enabled"), FillWeight = 70F });
        _gridUsers.SelectionChanged += (_, _) => LoadSelectedAccount();
        root.Controls.Add(_gridUsers, 0, 1);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 7; i++)
        {
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        }
        root.Controls.Add(editor, 1, 1);

        editor.Controls.Add(new Label { Text = HostUiText.Bi("帳號", "User name"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _txtUserName = new TextBox { Name = "txtUserName", Dock = DockStyle.Fill };
        editor.Controls.Add(_txtUserName, 1, 0);

        editor.Controls.Add(new Label { Text = HostUiText.Bi("顯示名稱", "Display name"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtDisplayName = new TextBox { Name = "txtDisplayName", Dock = DockStyle.Fill };
        editor.Controls.Add(_txtDisplayName, 1, 1);

        editor.Controls.Add(new Label { Text = HostUiText.Bi("角色", "Role"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _cboRole = new ComboBox
        {
            Name = "cboRole",
            Dock = DockStyle.Left,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220
        };
        _cboRole.DisplayMember = nameof(RoleOption.DisplayName);
        _cboRole.ValueMember = nameof(RoleOption.Value);
        _cboRole.DataSource = RoleOption.CreateItems();
        editor.Controls.Add(_cboRole, 1, 2);

        editor.Controls.Add(new Label { Text = HostUiText.Bi("密碼", "Password"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        _txtPassword = new TextBox { Name = "txtPassword", Dock = DockStyle.Fill, PasswordChar = '*' };
        editor.Controls.Add(_txtPassword, 1, 3);

        editor.Controls.Add(new Label { Text = HostUiText.Bi("啟用", "Enabled"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        _chkEnabled = new CheckBox { Name = "chkEnabled", Text = HostUiText.Bi("帳號已啟用", "Account is enabled"), AutoSize = true, Anchor = AnchorStyles.Left };
        editor.Controls.Add(_chkEnabled, 1, 4);

        var help = new Label
        {
            AutoSize = true,
            Text = HostUiText.Bi("編輯帳號時若密碼留空，將保留原密碼。", "Leave password blank to keep the existing password when editing."),
            ForeColor = Color.DimGray,
            Anchor = AnchorStyles.Left
        };
        editor.Controls.Add(help, 1, 5);

        var editorButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        _btnNew = new Button { Name = "btnNew", Text = HostUiText.Bi("新增", "New"), Width = 96, Height = 44 };
        _btnSave = new Button { Name = "btnSave", Text = HostUiText.Bi("儲存", "Save"), Width = 96, Height = 44 };
        _btnDelete = new Button { Name = "btnDelete", Text = HostUiText.Bi("刪除", "Delete"), Width = 96, Height = 44 };
        editorButtons.Controls.AddRange(new Control[] { _btnNew, _btnSave, _btnDelete });
        editor.Controls.Add(editorButtons, 1, 6);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        root.Controls.Add(footer, 0, 2);
        root.SetColumnSpan(footer, 2);

        _lblStatus = new Label
        {
            Name = "lblStatus",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray
        };
        footer.Controls.Add(_lblStatus, 0, 0);

        _btnClose = new Button
        {
            Name = "btnClose",
            Text = HostUiText.Bi("關閉", "Close"),
            DialogResult = DialogResult.OK,
            Width = 96,
            Height = 44,
            Anchor = AnchorStyles.Right
        };
        footer.Controls.Add(_btnClose, 1, 0);

        _btnNew.Click += (_, _) => BeginCreateAccount();
        _btnSave.Click += async (_, _) => await SaveAsync();
        _btnDelete.Click += async (_, _) => await DeleteAsync();

        BeginCreateAccount();
    }

    public void Bind(IAuthenticationService authenticationService, IAuditService auditService, AuthenticatedUserSession currentUser)
    {
        _authenticationService = authenticationService;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RefreshAccountsAsync();
    }

    private async Task RefreshAccountsAsync()
    {
        if (_authenticationService is null)
        {
            return;
        }

        _accounts = (await _authenticationService.GetAccountsAsync(CancellationToken.None)).ToList();
        _gridUsers.DataSource = _accounts
            .Select(static account => new UserGridItem(account))
            .ToList();

        if (!string.IsNullOrWhiteSpace(_selectedUserName))
        {
            foreach (DataGridViewRow row in _gridUsers.Rows)
            {
                if (row.DataBoundItem is not UserGridItem item || !string.Equals(item.UserName, _selectedUserName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                row.Selected = true;
                _gridUsers.CurrentCell = row.Cells[0];
                break;
            }
        }
    }

    private void LoadSelectedAccount()
    {
        if (_gridUsers.CurrentRow?.DataBoundItem is not UserGridItem selected)
        {
            return;
        }

        var account = _accounts.FirstOrDefault(item => string.Equals(item.UserName, selected.UserName, StringComparison.OrdinalIgnoreCase));
        if (account is null)
        {
            return;
        }

        _selectedUserName = account.UserName;
        _txtUserName.Text = account.UserName;
        _txtUserName.Enabled = false;
        _txtDisplayName.Text = account.DisplayName;
        SelectRole(account.Role);
        _txtPassword.Text = string.Empty;
        _chkEnabled.Checked = account.IsEnabled;
        _btnDelete.Enabled = !string.Equals(account.UserName, _currentUser?.UserName, StringComparison.OrdinalIgnoreCase);
        _lblStatus.Text = HostUiText.Bi($"正在編輯帳號「{account.UserName}」。", $"Editing account '{account.UserName}'.");
    }

    private void BeginCreateAccount()
    {
        _selectedUserName = null;
        _txtUserName.Enabled = true;
        _txtUserName.Text = string.Empty;
        _txtDisplayName.Text = string.Empty;
        SelectRole(UserRole.Operator);
        _txtPassword.Text = string.Empty;
        _chkEnabled.Checked = true;
        _btnDelete.Enabled = false;
        _lblStatus.Text = HostUiText.Bi("建立新的本機帳號。", "Create a new local account.");
    }

    private async Task SaveAsync()
    {
        if (_authenticationService is null)
        {
            return;
        }

        try
        {
            _btnSave.Enabled = false;
            _btnDelete.Enabled = false;
            _btnNew.Enabled = false;
            _lblStatus.Text = HostUiText.Bi("正在儲存帳號...", "Saving account...");

            var model = new UserAccountEditorModel
            {
                UserName = _txtUserName.Text.Trim(),
                DisplayName = _txtDisplayName.Text.Trim(),
                Role = GetSelectedRole(),
                Password = _txtPassword.Text,
                IsEnabled = _chkEnabled.Checked
            };

            var isCreate = string.IsNullOrWhiteSpace(_selectedUserName);
            await _authenticationService.SaveAccountAsync(model, CancellationToken.None);
            if (_auditService is not null && _currentUser is not null)
            {
                await _auditService.WriteAsync(
                    isCreate ? "user-account-create" : "user-account-update",
                    _currentUser,
                    "user-account",
                    model.UserName,
                    true,
                    $"帳號「{model.UserName}」已以角色「{model.Role}」儲存。 / Account '{model.UserName}' was saved with role '{model.Role}'.",
                    CancellationToken.None);
            }

            _selectedUserName = model.UserName;
            _lblStatus.Text = HostUiText.Bi($"帳號「{model.UserName}」已儲存。", $"Account '{model.UserName}' saved successfully.");
            await RefreshAccountsAsync();
        }
        catch (Exception exception)
        {
            if (_auditService is not null && _currentUser is not null)
            {
                await _auditService.WriteAsync(
                    "user-account-save",
                    _currentUser,
                    "user-account",
                    _txtUserName.Text.Trim(),
                    false,
                    $"帳號儲存失敗：{exception.Message} / Account save failed: {exception.Message}",
                    CancellationToken.None);
            }

            _lblStatus.Text = HostUiText.Bi($"儲存失敗：{exception.Message}", $"Save failed: {exception.Message}");
            MessageBox.Show(HostUiText.Bi($"儲存帳號失敗：{exception.Message}", $"Failed to save account: {exception.Message}"), HostUiText.Window("使用者管理", "User Management"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSave.Enabled = true;
            _btnDelete.Enabled = _selectedUserName is not null && !string.Equals(_selectedUserName, _currentUser?.UserName, StringComparison.OrdinalIgnoreCase);
            _btnNew.Enabled = true;
        }
    }

    private async Task DeleteAsync()
    {
        if (_authenticationService is null || _currentUser is null || string.IsNullOrWhiteSpace(_selectedUserName))
        {
            return;
        }

        var result = MessageBox.Show(
            HostUiText.Bi($"確定要刪除帳號「{_selectedUserName}」嗎？", $"Delete account '{_selectedUserName}'?"),
            HostUiText.Window("使用者管理", "User Management"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _authenticationService.DeleteAccountAsync(_selectedUserName, _currentUser.UserName, CancellationToken.None);
            if (_auditService is not null)
            {
                await _auditService.WriteAsync(
                    "user-account-delete",
                    _currentUser,
                    "user-account",
                    _selectedUserName,
                    true,
                    $"帳號「{_selectedUserName}」已刪除。 / Account '{_selectedUserName}' was deleted.",
                    CancellationToken.None);
            }

            _lblStatus.Text = HostUiText.Bi($"帳號「{_selectedUserName}」已刪除。", $"Account '{_selectedUserName}' deleted.");
            BeginCreateAccount();
            await RefreshAccountsAsync();
        }
        catch (Exception exception)
        {
            if (_auditService is not null)
            {
                await _auditService.WriteAsync(
                    "user-account-delete",
                    _currentUser,
                    "user-account",
                    _selectedUserName,
                    false,
                    $"帳號刪除失敗：{exception.Message} / Account deletion failed: {exception.Message}",
                    CancellationToken.None);
            }

            _lblStatus.Text = HostUiText.Bi($"刪除失敗：{exception.Message}", $"Delete failed: {exception.Message}");
            MessageBox.Show(HostUiText.Bi($"刪除帳號失敗：{exception.Message}", $"Failed to delete account: {exception.Message}"), HostUiText.Window("使用者管理", "User Management"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class UserGridItem
    {
        public UserGridItem(UserAccount account)
        {
            UserName = account.UserName;
            DisplayName = account.DisplayName;
            Role = account.Role switch
            {
                UserRole.Administrator => HostUiText.Bi("管理員", "Administrator"),
                UserRole.Operator => HostUiText.Bi("操作員", "Operator"),
                UserRole.Viewer => HostUiText.Bi("檢視者", "Viewer"),
                _ => account.Role.ToString()
            };
            IsEnabled = account.IsEnabled ? HostUiText.Bi("是", "Yes") : HostUiText.Bi("否", "No");
        }

        public string UserName { get; }

        public string DisplayName { get; }

        public string Role { get; }

        public string IsEnabled { get; }
    }

    private UserRole GetSelectedRole()
    {
        return _cboRole.SelectedItem is RoleOption option
            ? option.Value
            : UserRole.Operator;
    }

    private void SelectRole(UserRole role)
    {
        if (_cboRole.DataSource is IEnumerable<RoleOption> options)
        {
            var match = options.FirstOrDefault(option => option.Value == role);
            if (match is not null)
            {
                _cboRole.SelectedItem = match;
                return;
            }
        }
    }

    private sealed class RoleOption
    {
        public required UserRole Value { get; init; }

        public required string DisplayName { get; init; }

        public static RoleOption[] CreateItems()
        {
            return
            [
                new RoleOption { Value = UserRole.Administrator, DisplayName = HostUiText.Bi("管理員", "Administrator") },
                new RoleOption { Value = UserRole.Operator, DisplayName = HostUiText.Bi("操作員", "Operator") },
                new RoleOption { Value = UserRole.Viewer, DisplayName = HostUiText.Bi("檢視者", "Viewer") }
            ];
        }
    }
}
