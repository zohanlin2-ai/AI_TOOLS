using System.Text;
using System.IO;
using System.Globalization;

namespace AiTerminal.Services;

/// <summary>Writes only bytes received from the device. Commands are never passed to this class.</summary>
public sealed class LineLogWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly bool _timestamp;
    private readonly StringBuilder _pendingLine = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LineLogWriter(string path, bool timestamp)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        _timestamp = timestamp;
    }

    public async Task WriteDeviceOutputAsync(string text, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_timestamp)
            {
                await _writer.WriteAsync(text);
                await _writer.FlushAsync(cancellationToken);
                return;
            }

            foreach (var character in text)
            {
                _pendingLine.Append(character);
                if (character == '\n')
                {
                    var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture);
                    await _writer.WriteAsync(timestamp + _pendingLine);
                    _pendingLine.Clear();
                }
            }
            await _writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_timestamp && _pendingLine.Length > 0)
            {
                await _writer.WriteAsync(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ", CultureInfo.InvariantCulture) + _pendingLine);
            }
            await _writer.FlushAsync();
            _writer.Dispose();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
