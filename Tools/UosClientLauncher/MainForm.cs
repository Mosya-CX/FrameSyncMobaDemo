using System.ComponentModel;
using System.Diagnostics;

namespace FrameSyncMoba.UosClientLauncher;

internal sealed class MainForm : Form
{
    private readonly string _settingsPath;
    private readonly LauncherSettings _settings;
    private readonly BindingList<ClientLaunchProfile> _profiles;
    private readonly ClientProcessManager _processManager = new();
    private readonly TextBox _clientPathText = new();
    private readonly TextBox _logDirectoryText = new();
    private readonly TextBox _matchmakingConfigText = new();
    private readonly TextBox _regionText = new();
    private readonly TextBox _extraArgumentsText = new();
    private readonly NumericUpDown _widthInput = new();
    private readonly NumericUpDown _heightInput = new();
    private readonly CheckBox _windowedCheck = new();
    private readonly CheckBox _checksumDetailCheck = new();
    private readonly CheckBox _disableDiagnosticsCheck = new();
    private readonly DataGridView _profileGrid = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    public MainForm()
    {
        string? projectRoot = ProjectLocator.FindProjectRoot();
        _settingsPath = LauncherSettingsStore.DefaultSettingsPath;
        _settings = LauncherSettingsStore.LoadOrDefault(
            _settingsPath,
            projectRoot);
        _profiles = new BindingList<ClientLaunchProfile>(_settings.Profiles);

        Text = "FrameSync MOBA - UOS 客户端启动器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 660);
        Size = new Size(1120, 760);
        Font = new Font("Microsoft YaHei UI", 9F);

        InitializeLayout();
        LoadSettingsIntoControls();

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += (_, _) => _profileGrid.Invalidate();
        _refreshTimer.Start();
        FormClosing += OnFormClosing;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _processManager.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(CreateSettingsPanel(), 0, 0);
        root.Controls.Add(CreateProfileGrid(), 0, 1);
        root.Controls.Add(CreateButtonPanel(), 0, 2);

        StatusStrip statusStrip = new();
        statusStrip.Items.Add(_statusLabel);
        root.Controls.Add(statusStrip, 0, 3);
        _statusLabel.Text = "就绪。客户端登录由 UOS Client 内部完成。";
    }

    private Control CreateSettingsPanel()
    {
        GroupBox group = new()
        {
            Text = "启动设置",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10)
        };
        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 6
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        group.Controls.Add(table);

        AddPathRow(
            table,
            0,
            "客户端程序",
            _clientPathText,
            BrowseClientExecutable);
        AddPathRow(
            table,
            1,
            "日志目录",
            _logDirectoryText,
            BrowseLogDirectory);

        table.Controls.Add(CreateLabel("匹配配置 ID"), 0, 2);
        table.Controls.Add(_matchmakingConfigText, 1, 2);
        table.Controls.Add(CreateLabel("区域 ID"), 2, 2);
        table.Controls.Add(_regionText, 3, 2);
        _matchmakingConfigText.Dock = DockStyle.Fill;
        _regionText.Dock = DockStyle.Fill;

        table.Controls.Add(CreateLabel("窗口尺寸"), 0, 3);
        FlowLayoutPanel sizePanel = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false
        };
        ConfigureDimensionInput(_widthInput);
        ConfigureDimensionInput(_heightInput);
        sizePanel.Controls.Add(_widthInput);
        sizePanel.Controls.Add(new Label
        {
            Text = "×",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 6, 4, 0)
        });
        sizePanel.Controls.Add(_heightInput);
        table.Controls.Add(sizePanel, 1, 3);

        FlowLayoutPanel optionsPanel = new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        _windowedCheck.Text = "窗口化";
        _checksumDetailCheck.Text = "详细校验日志";
        _disableDiagnosticsCheck.Text = "关闭帧同步异步诊断";
        optionsPanel.Controls.AddRange(
            new Control[]
            {
                _windowedCheck,
                _checksumDetailCheck,
                _disableDiagnosticsCheck
            });
        table.Controls.Add(CreateLabel("运行选项"), 2, 3);
        table.Controls.Add(optionsPanel, 3, 3);

        table.Controls.Add(CreateLabel("额外参数"), 0, 4);
        _extraArgumentsText.Dock = DockStyle.Fill;
        table.Controls.Add(_extraArgumentsText, 1, 4);
        table.SetColumnSpan(_extraArgumentsText, 3);

        Label note = new()
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "每个客户端必须使用不同的 TestAccountId；日志文件会自动按实例和时间分别创建。",
            Padding = new Padding(0, 5, 0, 0)
        };
        table.Controls.Add(note, 1, 5);
        table.SetColumnSpan(note, 3);
        return group;
    }

    private Control CreateProfileGrid()
    {
        _profileGrid.Dock = DockStyle.Fill;
        _profileGrid.AutoGenerateColumns = false;
        _profileGrid.AllowUserToAddRows = false;
        _profileGrid.AllowUserToDeleteRows = false;
        _profileGrid.MultiSelect = true;
        _profileGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _profileGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _profileGrid.DataSource = _profiles;
        _profileGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "启动",
            DataPropertyName = nameof(ClientLaunchProfile.Enabled),
            FillWeight = 15
        });
        _profileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "TestAccountId",
            DataPropertyName = nameof(ClientLaunchProfile.AccountId),
            FillWeight = 55
        });
        _profileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "窗口名",
            DataPropertyName = nameof(ClientLaunchProfile.WindowTitle),
            FillWeight = 35
        });
        _profileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StatusColumn",
            HeaderText = "状态",
            ReadOnly = true,
            FillWeight = 25
        });
        _profileGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "PidColumn",
            HeaderText = "PID",
            ReadOnly = true,
            FillWeight = 15
        });
        _profileGrid.CellFormatting += OnProfileCellFormatting;
        return _profileGrid;
    }

    private Control CreateButtonPanel()
    {
        FlowLayoutPanel panel = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        panel.Controls.Add(CreateButton("启动已勾选", LaunchEnabled));
        panel.Controls.Add(CreateButton("停止选中", StopSelected));
        panel.Controls.Add(CreateButton("停止全部", StopAll));
        panel.Controls.Add(CreateButton("新增实例", AddProfile));
        panel.Controls.Add(CreateButton("删除选中", RemoveSelectedProfiles));
        panel.Controls.Add(CreateButton("重新生成账号 ID", RegenerateSelectedAccountIds));
        panel.Controls.Add(CreateButton("打开日志目录", OpenLogDirectory));
        panel.Controls.Add(CreateButton("保存设置", SaveSettings));
        return panel;
    }

    private static Button CreateButton(string text, EventHandler click)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(5, 2, 5, 2)
        };
        button.Click += click;
        return button;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 7, 8, 3)
        };
    }

    private static void ConfigureDimensionInput(NumericUpDown input)
    {
        input.Minimum = 480;
        input.Maximum = 7680;
        input.Increment = 10;
        input.Width = 80;
    }

    private static void AddPathRow(
        TableLayoutPanel table,
        int row,
        string label,
        TextBox textBox,
        EventHandler browse)
    {
        table.Controls.Add(CreateLabel(label), 0, row);
        textBox.Dock = DockStyle.Fill;
        table.Controls.Add(textBox, 1, row);
        table.SetColumnSpan(textBox, 2);
        Button button = CreateButton("浏览…", browse);
        button.Dock = DockStyle.Fill;
        table.Controls.Add(button, 3, row);
    }

    private void LoadSettingsIntoControls()
    {
        _clientPathText.Text = _settings.ClientExecutablePath;
        _logDirectoryText.Text = _settings.LogDirectory;
        _matchmakingConfigText.Text = _settings.MatchmakingConfigId;
        _regionText.Text = _settings.RegionId;
        _widthInput.Value = Math.Clamp(_settings.WindowWidth, 640, 7680);
        _heightInput.Value = Math.Clamp(_settings.WindowHeight, 480, 4320);
        _windowedCheck.Checked = _settings.Windowed;
        _checksumDetailCheck.Checked = _settings.ChecksumDetail;
        _disableDiagnosticsCheck.Checked =
            _settings.DisableFrameSyncDiagnostics;
        _extraArgumentsText.Text = _settings.ExtraArguments;
    }

    private void ReadControlsIntoSettings()
    {
        _profileGrid.EndEdit();
        _settings.ClientExecutablePath = _clientPathText.Text.Trim();
        _settings.LogDirectory = _logDirectoryText.Text.Trim();
        _settings.MatchmakingConfigId = _matchmakingConfigText.Text.Trim();
        _settings.RegionId = _regionText.Text.Trim();
        _settings.WindowWidth = decimal.ToInt32(_widthInput.Value);
        _settings.WindowHeight = decimal.ToInt32(_heightInput.Value);
        _settings.Windowed = _windowedCheck.Checked;
        _settings.ChecksumDetail = _checksumDetailCheck.Checked;
        _settings.DisableFrameSyncDiagnostics =
            _disableDiagnosticsCheck.Checked;
        _settings.ExtraArguments = _extraArgumentsText.Text;
        _settings.Profiles = _profiles.ToList();
    }

    private void BrowseClientExecutable(object? sender, EventArgs eventArgs)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "选择 UOS 客户端",
            Filter = "Windows 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            FileName = _clientPathText.Text
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _clientPathText.Text = dialog.FileName;
        }
    }

    private void BrowseLogDirectory(object? sender, EventArgs eventArgs)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "选择客户端日志目录",
            SelectedPath = _logDirectoryText.Text,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _logDirectoryText.Text = dialog.SelectedPath;
        }
    }

    private void LaunchEnabled(object? sender, EventArgs eventArgs)
    {
        try
        {
            ReadControlsIntoSettings();
            ValidateLaunchSettings();
            int started = 0;
            foreach (ClientLaunchProfile profile in _profiles)
            {
                if (!profile.Enabled)
                {
                    continue;
                }

                ManagedClientProcess? existing = _processManager.Get(profile.Id);
                if (existing is { HasExited: false })
                {
                    continue;
                }

                _processManager.Start(_settings, profile);
                started++;
            }

            LauncherSettingsStore.Save(_settingsPath, _settings);
            SetStatus(started == 0
                ? "没有需要启动的实例。"
                : $"已启动 {started} 个 UOS 客户端。窗口标题会在窗口创建后自动设置。");
            _profileGrid.Invalidate();
        }
        catch (Exception exception)
        {
            ShowError("启动失败", exception);
        }
    }

    private async void StopSelected(object? sender, EventArgs eventArgs)
    {
        List<ClientLaunchProfile> selected = GetSelectedProfiles();
        await StopProfilesAsync(selected);
    }

    private async void StopAll(object? sender, EventArgs eventArgs)
    {
        await StopProfilesAsync(_profiles.ToList());
    }

    private async Task StopProfilesAsync(
        IReadOnlyCollection<ClientLaunchProfile> profiles)
    {
        List<ClientLaunchProfile> running = profiles
            .Where(profile =>
                _processManager.Get(profile.Id) is { HasExited: false })
            .ToList();
        if (running.Count == 0)
        {
            SetStatus("选定范围内没有运行中的客户端。");
            return;
        }

        DialogResult confirmation = MessageBox.Show(
            this,
            $"确定停止 {running.Count} 个客户端吗？\n" +
            "会先请求正常退出，3 秒后仍未退出才会强制结束进程。",
            "停止客户端",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            foreach (ClientLaunchProfile profile in running)
            {
                await _processManager.StopAsync(profile.Id);
            }

            SetStatus($"已停止 {running.Count} 个客户端。");
            _profileGrid.Invalidate();
        }
        catch (Exception exception)
        {
            ShowError("停止客户端失败", exception);
        }
    }

    private void AddProfile(object? sender, EventArgs eventArgs)
    {
        ClientLaunchProfile profile = ClientLaunchProfile.Create(
            $"UOS Client {_profiles.Count + 1}");
        _profiles.Add(profile);
        int rowIndex = _profiles.Count - 1;
        _profileGrid.ClearSelection();
        _profileGrid.Rows[rowIndex].Selected = true;
        SetStatus("已新增客户端实例配置。记得保存设置。");
    }

    private void RemoveSelectedProfiles(object? sender, EventArgs eventArgs)
    {
        List<ClientLaunchProfile> selected = GetSelectedProfiles();
        foreach (ClientLaunchProfile profile in selected)
        {
            if (_processManager.Get(profile.Id) is { HasExited: false })
            {
                MessageBox.Show(
                    this,
                    $"{profile.DisplayName} 仍在运行，请先停止它。",
                    "无法删除",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                continue;
            }

            _profiles.Remove(profile);
        }
    }

    private void RegenerateSelectedAccountIds(
        object? sender,
        EventArgs eventArgs)
    {
        foreach (ClientLaunchProfile profile in GetSelectedProfiles())
        {
            if (_processManager.Get(profile.Id) is { HasExited: false })
            {
                continue;
            }

            profile.AccountId = Guid.NewGuid().ToString("N");
        }

        _profileGrid.Refresh();
        SetStatus("已为未运行的选中实例生成新 TestAccountId。");
    }

    private void OpenLogDirectory(object? sender, EventArgs eventArgs)
    {
        try
        {
            string path = _logDirectoryText.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("请先填写日志目录。");
            }

            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetFullPath(path),
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ShowError("无法打开日志目录", exception);
        }
    }

    private void SaveSettings(object? sender, EventArgs eventArgs)
    {
        try
        {
            ReadControlsIntoSettings();
            LauncherSettingsStore.Save(_settingsPath, _settings);
            SetStatus($"设置已保存：{_settingsPath}");
        }
        catch (Exception exception)
        {
            ShowError("保存设置失败", exception);
        }
    }

    private void ValidateLaunchSettings()
    {
        if (!File.Exists(_settings.ClientExecutablePath))
        {
            throw new FileNotFoundException(
                "客户端程序不存在，请重新选择 UOS Client。",
                _settings.ClientExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(_settings.LogDirectory))
        {
            throw new InvalidOperationException("日志目录不能为空。");
        }

        List<ClientLaunchProfile> enabled = _profiles
            .Where(profile => profile.Enabled)
            .ToList();
        if (enabled.Count == 0)
        {
            throw new InvalidOperationException("请至少勾选一个客户端实例。");
        }

        ClientLaunchProfile? missingAccount = enabled.FirstOrDefault(
            profile => string.IsNullOrWhiteSpace(profile.AccountId));
        if (missingAccount != null)
        {
            throw new InvalidOperationException(
                $"{missingAccount.DisplayName} 的 TestAccountId 不能为空。");
        }

        string? duplicateAccount = enabled
            .GroupBy(
                profile => profile.AccountId.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateAccount != null)
        {
            throw new InvalidOperationException(
                $"已勾选的实例使用了重复 TestAccountId：{duplicateAccount}");
        }

        _ = WindowsCommandLine.Split(_settings.ExtraArguments);
    }

    private List<ClientLaunchProfile> GetSelectedProfiles()
    {
        return _profileGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as ClientLaunchProfile)
            .Where(profile => profile != null)
            .Cast<ClientLaunchProfile>()
            .Distinct()
            .ToList();
    }

    private void OnProfileCellFormatting(
        object? sender,
        DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            _profileGrid.Rows[eventArgs.RowIndex].DataBoundItem is not
                ClientLaunchProfile profile)
        {
            return;
        }

        ManagedClientProcess? process = _processManager.Get(profile.Id);
        string columnName = _profileGrid.Columns[eventArgs.ColumnIndex].Name;
        if (columnName == "StatusColumn")
        {
            eventArgs.Value = process?.Status ?? "未启动";
            eventArgs.FormattingApplied = true;
        }
        else if (columnName == "PidColumn")
        {
            eventArgs.Value = process == null
                ? string.Empty
                : process.Process.Id.ToString();
            eventArgs.FormattingApplied = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        try
        {
            ReadControlsIntoSettings();
            LauncherSettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception exception)
        {
            DialogResult result = MessageBox.Show(
                this,
                "设置保存失败，仍要关闭启动器吗？\n\n" + exception.Message,
                "保存失败",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                eventArgs.Cancel = true;
            }
        }
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ShowError(string title, Exception exception)
    {
        SetStatus(title + "：" + exception.Message);
        MessageBox.Show(
            this,
            exception.Message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
