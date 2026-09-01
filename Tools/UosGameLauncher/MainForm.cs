using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FrameSyncMoba.GameLauncher;

internal enum LauncherPrimaryAction
{
    Checking,
    Download,
    Update,
    Start,
    CancelDownload,
    CancelUpdate,
    Stop,
    Disabled
}

internal sealed class MainForm : Form
{
    private readonly string _settingsPath;
    private readonly LauncherSettings _settings;
    private readonly LauncherArtwork _artwork;
    private readonly GameProcessManager _processManager = new();
    private readonly CdnLauncherConfig _cdnConfig;
    private readonly CdnInstallService _installService;
    private readonly string? _cdnConfigError;
    private readonly WhiteboardPanel _banner = new();
    private readonly Label _installStatusLabel = new();
    private readonly Label _runtimeStatusLabel = new();
    private readonly Label _statusLabel = new();
    private readonly TextBox _loginNameText = new();
    private readonly Button _startButton = new();
    private readonly ProgressBar _updateProgress = new();
    private readonly Label _updateStatusLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private CancellationTokenSource? _operationCancellation;
    private bool _operationRunning;
    private LauncherPrimaryAction _primaryAction = LauncherPrimaryAction.Checking;

    public MainForm()
    {
        string? projectRoot = ProjectLocator.FindProjectRoot();
        _settingsPath = LauncherSettingsStore.DefaultSettingsPath;
        _settings = LauncherSettingsStore.LoadOrDefault(_settingsPath, projectRoot);
        _artwork = LauncherArtwork.Load();
        _cdnConfig = LoadCdnConfigSafely(out _cdnConfigError);
        _installService = new CdnInstallService(
            CdnInstallService.DefaultCacheRoot,
            CdnSigningTrust.LoadEmbeddedPublicKey());
        string gameDirectory = Path.GetDirectoryName(_settings.GameExecutablePath)!;
        _installService.RecoverInterruptedInstall(gameDirectory);

        Text = "FrameSync MOBA";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1120, 700);
        BackColor = LauncherPalette.Window;
        ForeColor = LauncherPalette.TextPrimary;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackgroundImage = _artwork.Background;
        BackgroundImageLayout = ImageLayout.Stretch;
        if (_artwork.AppIcon != null)
        {
            Icon = _artwork.AppIcon;
        }

        InitializeLayout();
        _loginNameText.Text = _settings.LoginName;
        SetPrimaryAction(LauncherPrimaryAction.Checking);
        RefreshClientStatus();
        Shown += async (_, _) => await CheckRequiredActionOnStartupAsync();

        _refreshTimer.Interval = 500;
        _refreshTimer.Tick += (_, _) => RefreshClientStatus();
        _refreshTimer.Start();
        FormClosing += OnFormClosing;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _installService.Dispose();
            _processManager.Dispose();
            _artwork.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24),
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateContent(), 0, 1);
        root.Controls.Add(CreateFooter(), 0, 2);

        _banner.Dock = DockStyle.Fill;
        _banner.Margin = new Padding(0, 0, 18, 0);
        _banner.Logo = _artwork.Logo;
        _banner.Banner = _artwork.Banner;
    }

    private Control CreateHeader()
    {
        Panel header = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };
        Label brand = new()
        {
            AutoSize = true,
            Text = "FRAME / SYNC",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = LauncherPalette.TextPrimary,
            Location = new Point(0, 0)
        };
        Label edition = new()
        {
            AutoSize = true,
            Text = "MOBA  ·  DEMO CLIENT",
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            ForeColor = LauncherPalette.Accent,
            Location = new Point(2, 35)
        };
        header.Controls.Add(brand);
        header.Controls.Add(edition);
        return header;
    }

    private Control CreateContent()
    {
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        Panel left = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        left.Controls.Add(_banner);

        content.Controls.Add(left, 0, 0);
        content.Controls.Add(CreateStatusCard(), 1, 0);
        return content;
    }

    private Control CreateStatusCard()
    {
        Panel card = CreateCard();
        Label title = CreateCardTitle("进入游戏");
        title.Location = new Point(22, 22);
        card.Controls.Add(title);

        _installStatusLabel.AutoSize = true;
        _installStatusLabel.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        _installStatusLabel.Location = new Point(22, 62);
        card.Controls.Add(_installStatusLabel);

        Label loginCaption = new()
        {
            AutoSize = true,
            Text = "登录名",
            ForeColor = LauncherPalette.TextMuted,
            Location = new Point(22, 106)
        };
        card.Controls.Add(loginCaption);

        _loginNameText.Location = new Point(22, 130);
        _loginNameText.Size = new Size(280, 30);
        _loginNameText.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _loginNameText.BackColor = LauncherPalette.Input;
        _loginNameText.ForeColor = LauncherPalette.TextPrimary;
        _loginNameText.BorderStyle = BorderStyle.FixedSingle;
        _loginNameText.PlaceholderText = "输入登录名";
        card.Controls.Add(_loginNameText);

        _startButton.Text = "开始游戏";
        _startButton.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        _startButton.FlatStyle = FlatStyle.Flat;
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.BackColor = LauncherPalette.Accent;
        _startButton.ForeColor = Color.White;
        _startButton.Cursor = Cursors.Hand;
        _startButton.Height = 52;
        _startButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _startButton.Location = new Point(22, 190);
        _startButton.Width = 280;
        _startButton.Click += PrimaryActionClicked;
        card.Controls.Add(_startButton);

        _runtimeStatusLabel.AutoSize = false;
        _runtimeStatusLabel.ForeColor = LauncherPalette.TextMuted;
        _runtimeStatusLabel.Location = new Point(22, 266);
        _runtimeStatusLabel.Size = new Size(280, 68);
        card.Controls.Add(_runtimeStatusLabel);

        _updateStatusLabel.AutoSize = false;
        _updateStatusLabel.ForeColor = LauncherPalette.TextMuted;
        _updateStatusLabel.Location = new Point(22, 344);
        _updateStatusLabel.Size = new Size(280, 42);
        card.Controls.Add(_updateStatusLabel);

        _updateProgress.Location = new Point(22, 392);
        _updateProgress.Size = new Size(280, 16);
        _updateProgress.Style = ProgressBarStyle.Continuous;
        _updateProgress.Visible = false;
        card.Controls.Add(_updateProgress);

        card.Resize += (_, _) =>
        {
            int width = Math.Max(120, card.ClientSize.Width - 44);
            _loginNameText.Width = width;
            _startButton.Width = width;
            _runtimeStatusLabel.Width = width;
            _updateStatusLabel.Width = width;
            _updateProgress.Width = width;
        };
        return card;
    }

    private Control CreateFooter()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Left;
        _statusLabel.ForeColor = LauncherPalette.TextMuted;
        footer.Controls.Add(_statusLabel, 0, 0);

        Label version = new()
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Text = "DEMO CLIENT",
            ForeColor = LauncherPalette.TextMuted
        };
        footer.Controls.Add(version, 1, 0);
        return footer;
    }

    private static Panel CreateCard()
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(232, 16, 24, 38),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        card.Paint += (_, eventArgs) =>
        {
            using Pen pen = new(LauncherPalette.CardBorder, 1F);
            eventArgs.Graphics.DrawRectangle(
                pen,
                0,
                0,
                Math.Max(0, card.ClientSize.Width - 1),
                Math.Max(0, card.ClientSize.Height - 1));
        };
        return card;
    }

    private static Label CreateCardTitle(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = LauncherPalette.TextPrimary
        };
    }

    private void RefreshClientStatus()
    {
        GameInstallStatus install = GameInstallLocator.Check(_settings.GameExecutablePath);
        ManagedGameProcess? process = _processManager.Current;
        bool running = process is { HasExited: false };
        if (_operationRunning)
        {
            _loginNameText.Enabled = false;
            return;
        }

        if (running)
        {
            _installStatusLabel.Text = "游戏运行中";
            _installStatusLabel.ForeColor = LauncherPalette.Accent;
            _runtimeStatusLabel.Text = $"PID {process!.Process.Id}";
            SetPrimaryAction(LauncherPrimaryAction.Stop);
            _loginNameText.Enabled = false;
            SetStatus("客户端正在运行。");
            return;
        }

        if (_primaryAction == LauncherPrimaryAction.Stop)
        {
            SetPrimaryAction(install.IsReady
                ? LauncherPrimaryAction.Start
                : LauncherPrimaryAction.Download);
            SetStatus("客户端已关闭。");
        }

        if (!install.IsReady)
        {
            SetPrimaryAction(_cdnConfig.Enabled
                ? LauncherPrimaryAction.Download
                : LauncherPrimaryAction.Disabled);
        }

        _installStatusLabel.Text = _primaryAction switch
        {
            LauncherPrimaryAction.Download => "尚未安装客户端",
            LauncherPrimaryAction.Update => "客户端需要更新",
            LauncherPrimaryAction.Checking => "正在检查客户端",
            _ => install.IsReady ? "客户端已就绪" : "客户端未就绪"
        };
        _installStatusLabel.ForeColor = _primaryAction switch
        {
            LauncherPrimaryAction.Start => LauncherPalette.Success,
            LauncherPrimaryAction.Checking => LauncherPalette.Accent,
            _ => LauncherPalette.Warning
        };
        _runtimeStatusLabel.Text = process == null
            ? _cdnConfig.Enabled
                ? "下载、更新和启动是三个独立动作。"
                : "准备完成后点击“开始游戏”。"
            : process.Status;
        _loginNameText.Enabled = true;
        if (!install.IsReady)
        {
            SetStatus(_cdnConfig.Enabled
                ? "本地客户端为空；点击“下载游戏”后从 UOS CDN 安装。"
                : _cdnConfigError ?? install.Message);
        }
        else if (string.IsNullOrWhiteSpace(_statusLabel.Text) || _statusLabel.Text == "客户端正在运行。")
        {
            SetStatus("本地客户端就绪。");
        }
    }

    private async Task CheckRequiredActionOnStartupAsync()
    {
        if (_operationRunning || !_cdnConfig.Enabled)
        {
            if (!_cdnConfig.Enabled)
            {
                GameInstallStatus local = GameInstallLocator.Check(_settings.GameExecutablePath);
                SetPrimaryAction(local.IsReady
                    ? LauncherPrimaryAction.Start
                    : LauncherPrimaryAction.Disabled);
                RefreshClientStatus();
            }

            return;
        }

        string gameDirectory = Path.GetDirectoryName(_settings.GameExecutablePath) ??
                               throw new InvalidOperationException("无法解析 Game 目录。");
        _operationRunning = true;
        _operationCancellation = new CancellationTokenSource();
        SetPrimaryAction(LauncherPrimaryAction.Checking);
        SetStatus("正在检查客户端状态……");
        RefreshClientStatus();
        try
        {
            CdnClientCheckResult result = await _installService.CheckRequiredActionAsync(
                gameDirectory,
                _cdnConfig,
                _operationCancellation.Token);
            ApplyCheckResult(result);
        }
        catch (OperationCanceledException)
        {
            SetPrimaryAction(GameInstallLocator.Check(_settings.GameExecutablePath).IsReady
                ? LauncherPrimaryAction.Start
                : LauncherPrimaryAction.Download);
            SetStatus("客户端检查已取消。");
        }
        catch (Exception exception)
        {
            bool trusted = await _installService.ValidateTrustedInstallAsync(
                gameDirectory,
                CancellationToken.None);
            SetPrimaryAction(trusted
                ? LauncherPrimaryAction.Start
                : GameInstallLocator.Check(_settings.GameExecutablePath).IsReady
                    ? LauncherPrimaryAction.Update
                    : LauncherPrimaryAction.Download);
            SetStatus("启动检查失败，将在执行操作时重试：" + exception.Message);
        }
        finally
        {
            _operationRunning = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
            RefreshClientStatus();
        }
    }

    private async void PrimaryActionClicked(object? sender, EventArgs eventArgs)
    {
        switch (_primaryAction)
        {
            case LauncherPrimaryAction.CancelDownload:
            case LauncherPrimaryAction.CancelUpdate:
                _operationCancellation?.Cancel();
                _startButton.Enabled = false;
                SetStatus("正在安全取消，请稍候……");
                return;
            case LauncherPrimaryAction.Download:
            case LauncherPrimaryAction.Update:
                await InstallOrUpdateAsync(_primaryAction);
                return;
            case LauncherPrimaryAction.Start:
                if (await RecheckBeforeLaunchAsync())
                {
                    LaunchGame();
                }

                return;
            case LauncherPrimaryAction.Stop:
                await StopGameAsync();
                return;
            default:
                return;
        }
    }

    private async Task InstallOrUpdateAsync(LauncherPrimaryAction requestedAction)
    {
        if (_operationRunning)
        {
            return;
        }

        bool isDownload = requestedAction == LauncherPrimaryAction.Download;
        string gameDirectory = Path.GetDirectoryName(_settings.GameExecutablePath) ??
                               throw new InvalidOperationException("无法解析 Game 目录。");
        _operationRunning = true;
        _operationCancellation = new CancellationTokenSource();
        _updateProgress.Value = 0;
        _updateProgress.Visible = true;
        SetPrimaryAction(isDownload
            ? LauncherPrimaryAction.CancelDownload
            : LauncherPrimaryAction.CancelUpdate);
        Progress<CdnUpdateProgress> progress = new(UpdateProgress);
        RefreshClientStatus();
        try
        {
            CdnInstallResult result = await _installService.EnsureCurrentAsync(
                gameDirectory,
                _cdnConfig,
                progress,
                _operationCancellation.Token);
            SetPrimaryAction(LauncherPrimaryAction.Start);
            SetStatus(result.Changed
                ? $"客户端 {result.ClientVersion} 已准备完成，请点击“开始游戏”。"
                : $"客户端 {result.ClientVersion} 已是最新版本，请点击“开始游戏”。");
        }
        catch (OperationCanceledException)
        {
            SetPrimaryAction(requestedAction);
            SetStatus("下载或更新已取消，本地已安装版本未被替换。");
        }
        catch (Exception exception)
        {
            bool trusted = await _installService.ValidateTrustedInstallAsync(
                gameDirectory,
                CancellationToken.None);
            SetPrimaryAction(trusted
                ? LauncherPrimaryAction.Start
                : GameInstallLocator.Check(_settings.GameExecutablePath).IsReady
                    ? LauncherPrimaryAction.Update
                    : LauncherPrimaryAction.Download);
            ShowError(isDownload ? "下载失败" : "更新失败", exception);
        }
        finally
        {
            _operationRunning = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _updateProgress.Visible = false;
            RefreshClientStatus();
        }
    }

    private async Task<bool> RecheckBeforeLaunchAsync()
    {
        if (!_cdnConfig.Enabled)
        {
            return true;
        }

        string gameDirectory = Path.GetDirectoryName(_settings.GameExecutablePath) ??
                               throw new InvalidOperationException("无法解析 Game 目录。");
        _operationRunning = true;
        _operationCancellation = new CancellationTokenSource();
        SetPrimaryAction(LauncherPrimaryAction.Checking);
        SetStatus("开始游戏前正在重新检查……");
        RefreshClientStatus();
        try
        {
            CdnClientCheckResult result = await _installService.CheckRequiredActionAsync(
                gameDirectory,
                _cdnConfig,
                _operationCancellation.Token);
            ApplyCheckResult(result);
            if (result.RequiredAction != CdnRequiredAction.Start)
            {
                SetStatus(result.RequiredAction == CdnRequiredAction.Download
                    ? "客户端本体缺失，请点击“下载游戏”。"
                    : "检测到客户端需要更新，请点击“更新”。");
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            SetPrimaryAction(GameInstallLocator.Check(_settings.GameExecutablePath).IsReady
                ? LauncherPrimaryAction.Start
                : LauncherPrimaryAction.Download);
            SetStatus("启动前检查已取消。");
            return false;
        }
        catch (Exception exception)
        {
            bool trusted = await _installService.ValidateTrustedInstallAsync(
                gameDirectory,
                CancellationToken.None);
            if (trusted)
            {
                SetPrimaryAction(LauncherPrimaryAction.Start);
                DialogResult result = MessageBox.Show(
                    this,
                    "无法检查 CDN 更新，但本地客户端已通过签名与全文件哈希校验。是否启动本地版本？\n\n" +
                    exception.Message,
                    "更新检查失败",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                return result == DialogResult.Yes;
            }

            SetPrimaryAction(GameInstallLocator.Check(_settings.GameExecutablePath).IsReady
                ? LauncherPrimaryAction.Update
                : LauncherPrimaryAction.Download);
            ShowError("启动前检查失败", exception);
            return false;
        }
        finally
        {
            _operationRunning = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
            RefreshClientStatus();
        }
    }

    private void LaunchGame()
    {
        try
        {
            string loginName = _loginNameText.Text.Trim();
            if (string.IsNullOrWhiteSpace(loginName))
            {
                throw new InvalidOperationException("请先填写登录名。");
            }

            _settings.Normalize(ProjectLocator.FindProjectRoot());
            _settings.LoginName = loginName;
            SaveSettingsSilently();
            GameInstallLocator.ValidateOrThrow(_settings.GameExecutablePath);
            if (GameProcessManager.IsExecutableRunning(_settings.GameExecutablePath))
            {
                throw new GameClientRunningException();
            }

            ManagedGameProcess process = _processManager.Start(_settings);
            SetStatus($"客户端已启动（PID {process.Process.Id}）。");
            RefreshClientStatus();
        }
        catch (Exception exception)
        {
            ShowError("启动失败", exception);
            RefreshClientStatus();
        }
    }

    private async Task StopGameAsync()
    {
        if (_processManager.Current is not { HasExited: false })
        {
            return;
        }

        DialogResult result = MessageBox.Show(
            this,
            "确定关闭正在运行的客户端吗？",
            "关闭游戏",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _processManager.StopAsync();
            SetStatus("客户端已关闭。");
        }
        catch (Exception exception)
        {
            ShowError("关闭失败", exception);
        }

        RefreshClientStatus();
    }

    private void ApplyCheckResult(CdnClientCheckResult result)
    {
        SetPrimaryAction(result.RequiredAction switch
        {
            CdnRequiredAction.Download => LauncherPrimaryAction.Download,
            CdnRequiredAction.Update => LauncherPrimaryAction.Update,
            CdnRequiredAction.Start => LauncherPrimaryAction.Start,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        });
        SetStatus(result.RequiredAction switch
        {
            CdnRequiredAction.Download => "尚未安装客户端，请点击“下载游戏”。",
            CdnRequiredAction.Update => $"客户端 {result.ClientVersion} 可用，请点击“更新”。",
            CdnRequiredAction.Start => $"客户端 {result.ClientVersion} 已是最新版本。",
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        });
    }

    private void SetPrimaryAction(LauncherPrimaryAction action)
    {
        _primaryAction = action;
        _startButton.Text = action switch
        {
            LauncherPrimaryAction.Checking => "检查中……",
            LauncherPrimaryAction.Download => "下载游戏",
            LauncherPrimaryAction.Update => "更新",
            LauncherPrimaryAction.Start => "开始游戏",
            LauncherPrimaryAction.CancelDownload => "取消下载",
            LauncherPrimaryAction.CancelUpdate => "取消更新",
            LauncherPrimaryAction.Stop => "关闭游戏",
            LauncherPrimaryAction.Disabled => "客户端不可用",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        _startButton.Enabled = action != LauncherPrimaryAction.Checking &&
                               action != LauncherPrimaryAction.Disabled;
        _startButton.BackColor = action == LauncherPrimaryAction.Stop
            ? LauncherPalette.DangerButton
            : LauncherPalette.Accent;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_operationRunning)
        {
            _operationCancellation?.Cancel();
            SetStatus("正在安全取消下载，请稍候后再次关闭。");
            eventArgs.Cancel = true;
            return;
        }

        _settings.LoginName = _loginNameText.Text.Trim();
        try
        {
            _settings.Normalize(ProjectLocator.FindProjectRoot());
            LauncherSettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception exception)
        {
            DialogResult result = MessageBox.Show(
                this,
                "登录名保存失败，仍要关闭启动器吗？\n\n" + exception.Message,
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

    private void SaveSettingsSilently()
    {
        try
        {
            LauncherSettingsStore.Save(_settingsPath, _settings);
        }
        catch (Exception exception)
        {
            SetStatus("登录名暂未保存：" + exception.Message);
        }
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ShowError(string title, Exception exception)
    {
        SetStatus(title + "：" + exception.Message);
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void UpdateProgress(CdnUpdateProgress progress)
    {
        _updateStatusLabel.Text = progress.Message;
        _updateProgress.Value = progress.TotalBytes <= 0 ? 0 : progress.Percent;
        SetStatus(progress.Message);
    }

    private static CdnLauncherConfig LoadCdnConfigSafely(out string? error)
    {
        try
        {
            CdnLauncherConfig config = CdnLauncherConfig.LoadOrDisabled();
            if (config.Enabled)
            {
                _ = config.GetManifestUri();
            }

            error = null;
            return config;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            error = "CDN 配置不可用：" + exception.Message;
            return new CdnLauncherConfig();
        }
    }
}

internal static class LauncherPalette
{
    public static readonly Color Window = Color.FromArgb(12, 17, 27);
    public static readonly Color CardBorder = Color.FromArgb(67, 89, 119);
    public static readonly Color Input = Color.FromArgb(17, 25, 38);
    public static readonly Color TextPrimary = Color.FromArgb(238, 244, 255);
    public static readonly Color TextMuted = Color.FromArgb(145, 163, 188);
    public static readonly Color Accent = Color.FromArgb(53, 151, 255);
    public static readonly Color Success = Color.FromArgb(81, 205, 149);
    public static readonly Color Warning = Color.FromArgb(247, 184, 81);
    public static readonly Color DangerButton = Color.FromArgb(132, 57, 75);
}

internal sealed class WhiteboardPanel : Panel
{
    public Image? Logo { get; set; }

    public Image? Banner { get; set; }

    public WhiteboardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        BackColor = Color.FromArgb(210, 7, 13, 24);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (Banner != null)
        {
            DrawCover(graphics, Banner, ClientRectangle);
            using SolidBrush shade = new(Color.FromArgb(112, 5, 10, 18));
            graphics.FillRectangle(shade, ClientRectangle);
        }
        else
        {
            using LinearGradientBrush gradient = new(
                ClientRectangle,
                Color.FromArgb(24, 55, 92),
                Color.FromArgb(8, 17, 31),
                LinearGradientMode.ForwardDiagonal);
            graphics.FillRectangle(gradient, ClientRectangle);
            using Pen grid = new(Color.FromArgb(35, 149, 192, 229), 1F);
            for (int x = -ClientSize.Height; x < ClientSize.Width; x += 42)
            {
                graphics.DrawLine(grid, x, ClientSize.Height, x + ClientSize.Height, 0);
            }
        }

        if (Logo != null)
        {
            DrawContain(graphics, Logo, new Rectangle(30, 26, Math.Min(420, ClientSize.Width - 60), 110));
        }
        else
        {
            using Font logoFont = new("Segoe UI", 28F, FontStyle.Bold);
            using SolidBrush logoBrush = new(Color.FromArgb(242, 248, 255));
            graphics.DrawString("FRAME / SYNC", logoFont, logoBrush, 30, 32);
        }

        using Font kickerFont = new("Microsoft YaHei UI", 9F, FontStyle.Bold);
        using SolidBrush kickerBrush = new(LauncherPalette.Accent);
        graphics.DrawString("DEMO CLIENT", kickerFont, kickerBrush, 34, 145);

        using Font titleFont = new("Microsoft YaHei UI", 21F, FontStyle.Bold);
        using SolidBrush titleBrush = new(Color.FromArgb(242, 247, 255));
        graphics.DrawString("准备进入战场", titleFont, titleBrush, 30, Math.Max(175, ClientSize.Height - 112));

        using Pen accentLine = new(LauncherPalette.Accent, 3F);
        graphics.DrawLine(accentLine, 34, ClientSize.Height - 30, Math.Min(ClientSize.Width - 34, 210), ClientSize.Height - 30);
    }

    private static void DrawCover(Graphics graphics, Image image, Rectangle bounds)
    {
        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float sourceRatio = image.Width / (float)image.Height;
        float targetRatio = bounds.Width / (float)bounds.Height;
        Rectangle source;
        if (sourceRatio > targetRatio)
        {
            int width = (int)(image.Height * targetRatio);
            source = new Rectangle((image.Width - width) / 2, 0, width, image.Height);
        }
        else
        {
            int height = (int)(image.Width / targetRatio);
            source = new Rectangle(0, (image.Height - height) / 2, image.Width, height);
        }

        graphics.DrawImage(image, bounds, source, GraphicsUnit.Pixel);
    }

    private static void DrawContain(Graphics graphics, Image image, Rectangle bounds)
    {
        float scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        Rectangle destination = new(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
        graphics.DrawImage(image, destination);
    }
}
