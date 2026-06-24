using CrossDeviceTracker.Desktop.Core;
using CrossDeviceTracker.Desktop.Data;
using CrossDeviceTracker.Desktop.Services;

namespace CrossDeviceTracker.Desktop;

public class MainForm : Form
{
    private readonly AppTracker _tracker;
    private readonly ILogRepository _repository;
    private readonly IDeviceAuthService _deviceAuthService;
    private readonly IApiClient _apiClient;
    private ISyncService? _syncService;
    private NotifyIcon? _notifyIcon;
    private Label? _statusLabel;
    private Label? _currentAppLabel;
    private ListView? _logsListView;
    private bool _isExiting;
    private bool _isRelinkDialogOpen;

    public MainForm(IDeviceAuthService deviceAuthService, IApiClient apiClient, ILogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tracker = new AppTracker(_repository);
        _deviceAuthService = deviceAuthService ?? throw new ArgumentNullException(nameof(deviceAuthService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _apiClient.DeviceUnauthorized += ApiClient_DeviceUnauthorized;

        InitializeComponent();
        SetupTrayIcon();
        StartTracker();
    }

    private void InitializeComponent()
    {
        Text = "App Tracker";
        Width = 600;
        Height = 500;
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += MainForm_FormClosing;
        Resize += MainForm_Resize;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };

        _statusLabel = new Label
        {
            Text = "App Tracker - Running",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 35,
            ForeColor = Color.Green
        };

        _currentAppLabel = new Label
        {
            Text = "Current App: Loading...",
            Font = new Font("Segoe UI", 11),
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 10, 0, 0)
        };

        var logsLabel = new Label
        {
            Text = "Pending / Failed Activity:",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(0, 10, 0, 0)
        };

        _logsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        _logsListView.Columns.Add("App", 150);
        _logsListView.Columns.Add("Start Time", 150);
        _logsListView.Columns.Add("Duration", 100);
        _logsListView.Columns.Add("Sync", 90);

        var clearLogsButton = new Button
        {
            Text = "Clear Logs",
            Dock = DockStyle.Bottom,
            Height = 40,
            Margin = new Padding(0, 10, 0, 0)
        };
        clearLogsButton.Click += ClearLogsButton_Click;

        panel.Controls.Add(_logsListView);
        panel.Controls.Add(clearLogsButton);
        panel.Controls.Add(logsLabel);
        panel.Controls.Add(_currentAppLabel);
        panel.Controls.Add(_statusLabel);

        Controls.Add(panel);
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "App Tracker - Running"
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Relink Device", null, (_, _) => _ = LinkDeviceAsync(forceRelink: true));
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private async void StartTracker()
    {
        await _repository.InitializeAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await _tracker.StartAsync();
            }
            catch (Exception ex)
            {
                BeginInvoke(() => MessageBox.Show(
                    this,
                    $"Error: {ex.Message}",
                    "App Tracker Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error));
            }
        });

        _syncService = new SyncService(_apiClient);
        _ = Task.Run(async () =>
        {
            try
            {
                await _syncService.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync service error: {ex.Message}");
                BeginInvoke(() =>
                {
                    _notifyIcon?.ShowBalloonTip(3000, "Sync Error",
                        $"Sync service stopped unexpectedly: {ex.Message}", ToolTipIcon.Error);
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text = "App Tracker - Sync Error";
                        _statusLabel.ForeColor = Color.Red;
                    }
                });
            }
        });

        var updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };
        updateTimer.Tick += (_, _) => UpdateUI();
        updateTimer.Start();
    }

    private async void UpdateUI()
    {
        if (_currentAppLabel != null)
        {
            _currentAppLabel.Text = $"Current App: {_tracker.GetCurrentAppName() ?? "N/A"}";
        }

        if (_statusLabel != null)
        {
            var isLinked = await _deviceAuthService.IsLinkedAsync();
            _statusLabel.Text = isLinked ? "App Tracker - Running" : "App Tracker - Relink Required";
            _statusLabel.ForeColor = isLinked ? Color.Green : Color.DarkOrange;
        }

        try
        {
            var logs = await _repository.GetPendingLogsAsync();
            RefreshLogsListView(logs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading logs: {ex.Message}");
        }
    }

    private void RefreshLogsListView(List<Models.Log> logs)
    {
        if (_logsListView == null)
        {
            return;
        }

        _logsListView.Items.Clear();

        foreach (var log in logs.OrderByDescending(l => l.StartTime))
        {
            var item = new ListViewItem(log.AppName);
            item.SubItems.Add(log.StartTime.ToString("HH:mm:ss"));
            item.SubItems.Add($"{log.Duration.TotalSeconds:F0}s");
            item.SubItems.Add(log.SyncStatus.ToString());
            _logsListView.Items.Add(item);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            WindowState = FormWindowState.Minimized;
            _notifyIcon?.ShowBalloonTip(1500, "App Tracker", "Tracking continues in the background.", ToolTipIcon.Info);
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    private async void ClearLogsButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Are you sure you want to clear all logs?",
            "Clear Logs",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await _repository.DeleteAllLogsAsync();
            _logsListView?.Items.Clear();
            MessageBox.Show(this, "All logs cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error clearing logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LinkDeviceAsync(bool forceRelink = false)
    {
        if (_isRelinkDialogOpen)
        {
            return;
        }

        try
        {
            var isLinked = await _deviceAuthService.IsLinkedAsync();
            if (isLinked && !forceRelink)
            {
                MessageBox.Show(this, "Device is already linked.", "Device Linked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isRelinkDialogOpen = true;
            RestoreFromTray();

            using var dialog = new DeviceLinkingDialog(_deviceAuthService,
                forceRelink ? "Paste a new device token to relink this device." : null);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                MessageBox.Show(this, "Device linked successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _apiClient.SyncPendingLogsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error linking device: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRelinkDialogOpen = false;
        }
    }

    private void ApiClient_DeviceUnauthorized(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(async () =>
        {
            _notifyIcon?.ShowBalloonTip(2500, "Relink required", "Your device token expired or was revoked.", ToolTipIcon.Warning);
            await _deviceAuthService.UnlinkAsync();
            _ = LinkDeviceAsync(forceRelink: true);
        });
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async void ExitApplication()
    {
        _isExiting = true;

        await _tracker.StopAsync();

        if (_syncService != null)
        {
            await _syncService.StopAsync();
        }

        _notifyIcon?.Dispose();
        Application.Exit();
    }
}
