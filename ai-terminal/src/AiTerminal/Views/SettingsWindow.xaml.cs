using System.IO.Ports;
using System.Windows;
using Microsoft.Win32;
using AiTerminal.Models;
using System.IO;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AiTerminal.Views;

public partial class SettingsWindow : Window
{
    public event EventHandler<ConnectionOptions>? ConnectRequested;
    public event EventHandler? CancelRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        LogPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ai-terminal.log");
        RescanPorts(this, new RoutedEventArgs());
    }

    public void SetBusy(bool busy) => ConnectButton.IsEnabled = !busy;

    private void RescanPorts(object? sender, RoutedEventArgs e)
    {
        var selected = PortBox.Text;
        PortBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        PortBox.Text = selected;
    }

    private void ConnectionTypeChanged(object sender, RoutedEventArgs e)
    {
        if (PortBox is null) return;
        var serial = SerialRadio.IsChecked == true;
        PortBox.IsEnabled = serial;
        BaudBox.IsEnabled = serial;
        HostBox.IsEnabled = !serial;
        UsernameBox.IsEnabled = !serial;
        SshPortBox.IsEnabled = !serial;
        PasswordBox.IsEnabled = !serial;
        PrivateKeyBox.IsEnabled = !serial;
        BrowseKeyButton.IsEnabled = !serial;
    }

    private void BrowseLog(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Log files (*.log)|*.log|All files (*.*)|*.*", FileName = Path.GetFileName(LogPathBox.Text) };
        if (dialog.ShowDialog() == true) LogPathBox.Text = dialog.FileName;
    }

    private void BrowseKey(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Private key files (*.*)|*.*" };
        if (dialog.ShowDialog() == true) PrivateKeyBox.Text = dialog.FileName;
    }

    private void ConnectClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LogPathBox.Text))
        {
            MessageBox.Show("Choose a log file location.", "Missing log file", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SerialRadio.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(PortBox.Text) || !int.TryParse(BaudBox.Text, out var baud))
            {
                MessageBox.Show("Choose a COM port and valid baud rate.", "Invalid serial settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ConnectRequested?.Invoke(this, new ConnectionOptions(ConnectionKind.Serial, LogPathBox.Text, TimestampBox.IsChecked == true, PortBox.Text, baud));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(HostBox.Text) || string.IsNullOrWhiteSpace(UsernameBox.Text) ||
                !int.TryParse(SshPortBox.Text, out var sshPort) || (string.IsNullOrEmpty(PasswordBox.Password) && string.IsNullOrWhiteSpace(PrivateKeyBox.Text)))
            {
                MessageBox.Show("Enter SSH host, port, username, and a password or private key.", "Invalid SSH settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ConnectRequested?.Invoke(this, new ConnectionOptions(
                ConnectionKind.Ssh,
                LogPathBox.Text,
                TimestampBox.IsChecked == true,
                SshHost: HostBox.Text,
                SshPort: sshPort,
                SshUsername: UsernameBox.Text,
                SshPassword: PasswordBox.Password,
                SshPrivateKeyPath: string.IsNullOrWhiteSpace(PrivateKeyBox.Text) ? null : PrivateKeyBox.Text));
        }
    }

    private void CancelClicked(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);
}
