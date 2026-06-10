namespace CrossDeviceTracker.Desktop;

public class DeviceLinkingDialog : Form
{
    private TextBox? _emailTextBox;
    private TextBox? _passwordTextBox;
    private Button? _linkButton;
    private Button? _cancelButton;
    private Label? _statusLabel;
    private bool _isLinking = false;

    public string Email { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public bool Success { get; private set; }

    public DeviceLinkingDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Link Device to CrossDeviceTracker";
        Width = 400;
        Height = 300;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        // Title
        var titleLabel = new Label
        {
            Text = "Link Your Device",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 35,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Email label and textbox
        var emailLabel = new Label
        {
            Text = "Email:",
            Font = new Font("Segoe UI", 10),
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(0, 10, 0, 0)
        };

        _emailTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(5)
        };

        // Password label and textbox
        var passwordLabel = new Label
        {
            Text = "Password:",
            Font = new Font("Segoe UI", 10),
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(0, 10, 0, 0)
        };

        _passwordTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            PasswordChar = '●',
            Padding = new Padding(5)
        };

        // Status label
        _statusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(0, 10, 0, 0),
            ForeColor = Color.Red,
            AutoSize = true
        };

        // Buttons panel
        var buttonsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 10, 0, 0)
        };

        _linkButton = new Button
        {
            Text = "Link Device",
            Width = 100,
            Height = 35,
            Location = new Point(150, 8),
            BackColor = Color.Green,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        _linkButton.Click += LinkButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Width = 100,
            Height = 35,
            Location = new Point(260, 8)
        };
        _cancelButton.Click += CancelButton_Click;

        buttonsPanel.Controls.Add(_linkButton);
        buttonsPanel.Controls.Add(_cancelButton);

        // Add controls to panel in reverse order (bottom to top)
        panel.Controls.Add(buttonsPanel);
        panel.Controls.Add(_statusLabel);
        panel.Controls.Add(_passwordTextBox);
        panel.Controls.Add(passwordLabel);
        panel.Controls.Add(_emailTextBox);
        panel.Controls.Add(emailLabel);
        panel.Controls.Add(titleLabel);

        Controls.Add(panel);
    }

    private void LinkButton_Click(object? sender, EventArgs e)
    {
        if (_isLinking) return;

        var email = _emailTextBox?.Text?.Trim();
        var password = _passwordTextBox?.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Please enter email and password", Color.Red);
            return;
        }

        Email = email;
        Password = password;
        Success = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        Success = false;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    public void ShowStatus(string message, Color color)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = color;
        }
    }
}
