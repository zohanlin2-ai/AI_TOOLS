using System.IO.Ports;
using System.Text;
using AiTerminal.Models;

namespace AiTerminal.Services;

public sealed class SerialTerminalSession : ITerminalSession
{
    private readonly ConnectionOptions _options;
    private readonly SerialPort _port;
    private readonly LineLogWriter _logWriter;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Task? _readTask;

    public SerialTerminalSession(ConnectionOptions options)
    {
        _options = options;
        _port = new SerialPort(options.SerialPort, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
        {
            Handshake = options.Handshake,
            Encoding = Encoding.UTF8,
            NewLine = options.SerialLineEnding,
            ReadTimeout = -1,
            WriteTimeout = 5000,
        };
        _logWriter = new LineLogWriter(options.LogPath, options.TimestampLogLines);
    }

    public event EventHandler<string>? OutputReceived;
    public bool IsConnected => _port.IsOpen;
    public string LineEnding => _options.SerialLineEnding;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _port.Open();
        _readTask = Task.Run(() => ReadLoopAsync(_cancellation.Token));
        return Task.CompletedTask;
    }

    public Task SendAsync(string command, CancellationToken cancellationToken = default)
        => WriteRawAsync(command + LineEnding, cancellationToken);

    public async Task WriteRawAsync(string text, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_port.IsOpen) throw new InvalidOperationException("Serial connection is not open.");
            var data = _port.Encoding.GetBytes(text);
            var stream = _port.BaseStream;
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var encoding = _port.Encoding;
        var decoder = encoding.GetDecoder();
        var characters = new char[encoding.GetMaxCharCount(buffer.Length)];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _port.IsOpen)
            {
                var count = await _port.BaseStream.ReadAsync(buffer, cancellationToken);
                if (count == 0) continue;
                var decoded = decoder.GetChars(buffer.AsSpan(0, count), characters, flush: false);
                if (decoded == 0) continue;
                var text = new string(characters, 0, decoded);
                await _logWriter.WriteDeviceOutputAsync(text);
                OutputReceived?.Invoke(this, text);
            }
        }
        catch (OperationCanceledException) { }
        catch (System.IO.IOException) when (cancellationToken.IsCancellationRequested) { }
        catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested) { }

        var remaining = decoder.GetChars(ReadOnlySpan<byte>.Empty, characters, flush: true);
        if (remaining > 0) await _logWriter.WriteDeviceOutputAsync(new string(characters, 0, remaining));
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        if (_port.IsOpen) _port.Close();
        if (_readTask is not null) await _readTask;
        await _logWriter.DisposeAsync();
        _port.Dispose();
        _cancellation.Dispose();
    }
}
