using System.Text;
using AiTerminal.Models;
using Renci.SshNet;

namespace AiTerminal.Services;

public sealed class SshTerminalSession : ITerminalSession
{
    private readonly ConnectionOptions _options;
    private readonly LineLogWriter _logWriter;
    private readonly CancellationTokenSource _cancellation = new();
    private SshClient? _client;
    private ShellStream? _shell;
    private Task? _readTask;

    public SshTerminalSession(ConnectionOptions options)
    {
        _options = options;
        _logWriter = new LineLogWriter(options.LogPath, options.TimestampLogLines);
    }

    public event EventHandler<string>? OutputReceived;
    public bool IsConnected => _client?.IsConnected == true;
    public string LineEnding => "\n";

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SshHost) || string.IsNullOrWhiteSpace(_options.SshUsername))
            throw new InvalidOperationException("SSH host and username are required.");

        ConnectionInfo connectionInfo;
        if (!string.IsNullOrWhiteSpace(_options.SshPrivateKeyPath))
        {
            var keyFile = new PrivateKeyFile(_options.SshPrivateKeyPath, _options.SshPassword);
            connectionInfo = new PrivateKeyConnectionInfo(_options.SshHost, _options.SshPort, _options.SshUsername, keyFile);
        }
        else
        {
            if (string.IsNullOrEmpty(_options.SshPassword)) throw new InvalidOperationException("SSH password or private key is required.");
            connectionInfo = new PasswordConnectionInfo(_options.SshHost, _options.SshPort, _options.SshUsername, _options.SshPassword);
        }

        _client = new SshClient(connectionInfo);
        _client.Connect();
        _shell = _client.CreateShellStream("xterm", 80, 24, 0, 0, 4096);
        _readTask = Task.Run(() => ReadLoopAsync(_cancellation.Token));
        return Task.CompletedTask;
    }

    public Task SendAsync(string command, CancellationToken cancellationToken = default)
        => WriteRawAsync(command + LineEnding, cancellationToken);

    public Task WriteRawAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_shell is null || !IsConnected) throw new InvalidOperationException("SSH connection is not open.");
        _shell.Write(text);
        _shell.Flush();
        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var characters = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _shell is not null && IsConnected)
            {
                var count = await _shell.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                var decoded = decoder.GetChars(buffer.AsSpan(0, count), characters, flush: false);
                if (decoded == 0) continue;
                var text = new string(characters, 0, decoded);
                await _logWriter.WriteDeviceOutputAsync(text);
                OutputReceived?.Invoke(this, text);
            }
        }
        catch (OperationCanceledException) { }
        catch (System.IO.IOException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }

        var remaining = decoder.GetChars(ReadOnlySpan<byte>.Empty, characters, flush: true);
        if (remaining > 0)
        {
            var text = new string(characters, 0, remaining);
            await _logWriter.WriteDeviceOutputAsync(text);
            OutputReceived?.Invoke(this, text);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _shell?.Dispose();
        if (_readTask is not null) await _readTask;
        if (_client?.IsConnected == true) _client.Disconnect();
        _client?.Dispose();
        await _logWriter.DisposeAsync();
        _cancellation.Dispose();
    }
}
