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
    private Button? _clearLogsButton;

    public MainForm()
    {
        _repository = new SqliteLogRepository();
        _tracker = new AppTracker(_repository);
        _deviceAuthService = new DeviceAuthService();
        _apiClient = new ApiClient(_deviceAuthService, _repository);

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
            Text = "🎯 App Tracker - Running",
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
            Text = "Recent Activity:",
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

        _clearLogsButton = new Button
        {
            Text = "Clear Logs",
            Dock = DockStyle.Bottom,
            Height = 40,
            Margin = new Padding(0, 10, 0, 0)
        };
        _clearLogsButton.Click += ClearLogsButton_Click;

        panel.Controls.Add(_logsListView);
        panel.Controls.Add(_clearLogsButton);
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
        contextMenu.Items.Add("Show", null, (s, e) => Show());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Link Device", null, (s, e) => _ = LinkDeviceAsync());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => Show();
    }

    private async void StartTracker()
    {
        // Initialize database
        await _repository.InitializeAsync();

        // Start the AppTracker in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _tracker.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "App Tracker Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        // Start the SyncService in background
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
            }
        });

        // UI update timer
        var updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };
        updateTimer.Tick += (s, e) => UpdateUI();
        updateTimer.Start();
    }

    private async void UpdateUI()
    {
        // Update current app
        if (_currentAppLabel != null)
        {
            _currentAppLabel.Text = $"Current App: {_tracker.GetCurrentAppName() ?? "N/A"}";
        }

        // Refresh logs from database
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
        if (_logsListView == null) return;

        // Clear existing items but keep columns
        _logsListView.Items.Clear();

        // Add logs in reverse order (most recent first)
        foreach (var log in logs.OrderByDescending(l => l.StartTime))
        {
            var item = new ListViewItem(log.AppName);
            item.SubItems.Add(log.StartTime.ToString("HH:mm:ss"));
            item.SubItems.Add($"{log.Duration.TotalSeconds:F0}s");
            _logsListView.Items.Add(item);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            WindowState = FormWindowState.Minimized;
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
            "Are you sure you want to clear all logs?",
            "Clear Logs",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            try
            {
                await _repository.DeleteAllLogsAsync();
                _logsListView?.Items.Clear();
                MessageBox.Show("All logs cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task LinkDeviceAsync()
    {
        try
        {
            // Check if already authenticated
            var isAuthenticated = await _deviceAuthService.IsAuthenticatedAsync();
            if (isAuthenticated)
            {
                MessageBox.Show(
                    "Device is already linked. Use 'Unlink Device' to change accounts.",
                    "Device Linked",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Show linking dialog
            using (var dialog = new DeviceLinkingDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var linkingService = new DeviceLinkingService(_deviceAuthService);
                    var result = await linkingService.LinkDeviceAsync(dialog.Email, dialog.Password);

                    if (result)
                    {
                        MessageBox.Show(
                            $"✅ Device linked successfully!\n\nEmail: {dialog.Email}\nDevice: {Environment.MachineName}",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to link device. Please check your credentials and try again.",
                            "Linking Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error linking device: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void ExitApplication()
    {
        await _tracker.StopAsync();

        if (_syncService != null)
        {
            await _syncService.StopAsync();
        }

        _notifyIcon?.Dispose();
        Application.Exit();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Show();
    }
}
