using CrossDeviceTracker.Desktop.Services;

namespace CrossDeviceTracker.Desktop;

public class DeviceLinkingDialog : Form
{
    private TextBox? _tokenTextBox;
    private Label? _statusLabel;
    private readonly IDeviceAuthService _authService;

    public DeviceLinkingDialog(IDeviceAuthService authService, string? message = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        InitializeComponent(message);
    }

    private void InitializeComponent(string? message)
    {
        Text = "Link Device";
        Width = 460;
        Height = 270;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        var titleLabel = new Label
        {
            Text = "Link this device",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var messageLabel = new Label
        {
            Text = message ?? "Paste the device token from your account to continue.",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Height = 42
        };

        var tokenLabel = new Label
        {
            Text = "Device token",
            Font = new Font("Segoe UI", 10),
            Dock = DockStyle.Top,
            Height = 26,
            Padding = new Padding(0, 8, 0, 0)
        };

        _tokenTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 76,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        _statusLabel = new Label
        {
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.Red,
            Padding = new Padding(0, 8, 0, 0)
        };

        var buttonsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48
        };

        var linkButton = new Button
        {
            Text = "Link Device",
            Width = 110,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(210, 8)
        };
        linkButton.Click += LinkButton_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Width = 90,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(330, 8)
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonsPanel.Controls.Add(linkButton);
        buttonsPanel.Controls.Add(cancelButton);

        panel.Controls.Add(buttonsPanel);
        panel.Controls.Add(_statusLabel);
        panel.Controls.Add(_tokenTextBox);
        panel.Controls.Add(tokenLabel);
        panel.Controls.Add(messageLabel);
        panel.Controls.Add(titleLabel);

        Controls.Add(panel);
        AcceptButton = linkButton;
        CancelButton = cancelButton;
    }

    private async void LinkButton_Click(object? sender, EventArgs e)
    {
        var token = _tokenTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowStatus("Link token is required.", Color.Red);
            return;
        }

        ShowStatus("Linking device...", Color.Blue);

        try
        {
            await _authService.LinkDeviceAsync(token);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, Color.Red);
        }
    }

    private void ShowStatus(string message, Color color)
    {
        if (_statusLabel == null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
    }
}
