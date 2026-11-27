using System.Windows;
using AiTerminal.Models;
using AiTerminal.Services;
using AiTerminal.Views;
using Forms = System.Windows.Forms;
using AiTerminal.Control;

namespace AiTerminal;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = "Local\\AiTerminal.Instance.v1";
    private const string ShowWindowEventName = "Local\\AiTerminal.ShowWindow.v1";
    private Forms.NotifyIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private TerminalWindow? _terminalWindow;
    private ITerminalSession? _session;
    private LocalControlServer? _controlServer;
    private readonly Queue<string> _recentOutput = new();
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _showWindowRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) => Dispatcher.BeginInvoke(ShowTerminal),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        CreateTrayIcon();
        _controlServer = new LocalControlServer(HandleControlRequestAsync);
        _controlServer.Start();
        if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase)) ShowSettings();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show terminal", null, (_, _) => ShowTerminal());
        menu.Items.Add("Disconnect", null, async (_, _) => await DisconnectAsync());
        menu.Items.Add("New connection", null, async (_, _) => await ShowNewConnectionAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "AI Terminal",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowTerminal();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            RestoreAndActivate(_settingsWindow);
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.ConnectRequested += ConnectAsync;
        _settingsWindow.CancelRequested += async (_, _) => await ExitAsync();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private async void ConnectAsync(object? sender, ConnectionOptions options)
    {
        try
        {
            _settingsWindow?.SetBusy(true);
            await ConnectCoreAsync(options);
            EnsureTerminalWindow();
            _settingsWindow?.Hide();
            _terminalWindow?.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _settingsWindow?.SetBusy(false);
        }
    }

    private void ShowTerminal()
    {
        if (_terminalWindow is null && _session?.IsConnected == true) EnsureTerminalWindow();

        if (_terminalWindow is not null)
        {
            RestoreAndActivate(_terminalWindow);
        }
        else
        {
            ShowSettings();
        }
    }

    private void EnsureTerminalWindow()
    {
        if (_terminalWindow is not null || _session is null) return;

        _terminalWindow = new TerminalWindow(_session);
        _terminalWindow.Closing += (_, args) =>
        {
            if (_terminalWindow.CanClose) return;
            args.Cancel = true;
            _terminalWindow.Hide();
        };
    }

    private static void RestoreAndActivate(Window window)
    {
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private async Task ShowNewConnectionAsync()
    {
        await DisconnectAsync();
        ShowSettings();
    }

    private async Task DisconnectAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        if (_terminalWindow is not null)
        {
            _terminalWindow.AllowClose();
            _terminalWindow.Close();
            _terminalWindow = null;
        }
    }

    private async Task ExitAsync()
    {
        await DisconnectAsync();
        if (_controlServer is not null) await _controlServer.DisposeAsync();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _showWindowRegistration?.Unregister(null);
        _showWindowEvent?.Dispose();
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        Shutdown();
    }

    private async Task ConnectCoreAsync(ConnectionOptions options)
    {
        await DisconnectAsync();
        lock (_recentOutput) _recentOutput.Clear();
        _session = options.Kind == ConnectionKind.Serial ? new SerialTerminalSession(options) : new SshTerminalSession(options);
        _session.OutputReceived += (_, text) =>
        {
            lock (_recentOutput)
            {
                _recentOutput.Enqueue(text);
                if (_recentOutput.Count > 5000) _recentOutput.Dequeue();
            }
        };
        try
        {
            EnsureTerminalWindow();
            await _session.ConnectAsync();
            if (_terminalWindow is not null) _terminalWindow.Title = "AI Terminal - Connected";
        }
        catch
        {
            await DisconnectAsync();
            throw;
        }
    }

    private async Task<ControlResponse> HandleControlRequestAsync(ControlRequest request)
    {
        return await Dispatcher.InvokeAsync(async () =>
        {
            var args = request.Arguments;
            switch (request.Command)
            {
                case "status": return new ControlResponse(true, _session?.IsConnected == true ? "Connected" : "Disconnected");
                case "connect":
                    var kind = args.GetValueOrDefault("type", "serial")!.Equals("ssh", StringComparison.OrdinalIgnoreCase) ? ConnectionKind.Ssh : ConnectionKind.Serial;
                    var options = new ConnectionOptions(kind, Require(args, "log"), args.GetValueOrDefault("timestamp", "true") != "false",
                        args.GetValueOrDefault("port", "")!, int.TryParse(args.GetValueOrDefault("baud"), out var baud) ? baud : 115200,
                        SshHost: args.GetValueOrDefault("host", "")!, SshPort: int.TryParse(args.GetValueOrDefault("ssh-port"), out var sshPort) ? sshPort : 22,
                        SshUsername: args.GetValueOrDefault("user", "")!, SshPassword: args.GetValueOrDefault("password"), SshPrivateKeyPath: args.GetValueOrDefault("key"));
                    await ConnectCoreAsync(options);
                    EnsureTerminalWindow();
                    _settingsWindow?.Hide();
                    return new ControlResponse(true, "Connected");
                case "send":
                    if (_session?.IsConnected != true) return new ControlResponse(false, "No active session.");
                    await _session.SendAsync(Require(args, "command"));
                    return new ControlResponse(true, "Command sent.");
                case "input":
                    if (_session?.IsConnected != true) return new ControlResponse(false, "No active session.");
                    await _session.WriteRawAsync(Require(args, "text"));
                    return new ControlResponse(true, "Input sent.");
                case "key":
                    if (_session?.IsConnected != true) return new ControlResponse(false, "No active session.");
                    var keyName = args.GetValueOrDefault("name") ?? "";
                    if (!keyName.Equals("enter", StringComparison.OrdinalIgnoreCase))
                        return new ControlResponse(false, "Only --name enter is currently supported.");
                    await _session.WriteRawAsync(_session.LineEnding);
                    return new ControlResponse(true, "Key sent.");
                case "log":
                    var count = int.TryParse(args.GetValueOrDefault("tail"), out var tail) ? Math.Clamp(tail, 1, 5000) : 100;
                    lock (_recentOutput) return new ControlResponse(true, "Recent device output.", string.Concat(_recentOutput.TakeLast(count)));
                case "ui":
                    if (args.GetValueOrDefault("action") == "hide") _terminalWindow?.Hide(); else ShowTerminal();
                    return new ControlResponse(true, "UI updated.");
                case "disconnect": await DisconnectAsync(); return new ControlResponse(true, "Disconnected.");
                default: return new ControlResponse(false, "Unknown command.");
            }
        }).Task.Unwrap();
    }

    private static string Require(Dictionary<string, string?> args, string name) =>
        !string.IsNullOrWhiteSpace(args.GetValueOrDefault(name)) ? args[name]! : throw new ArgumentException($"--{name} is required.");

    private static void SignalExistingInstance()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
