using System.IO.Ports;

namespace AiTerminal.Models;

public enum ConnectionKind { Serial, Ssh }

public sealed record ConnectionOptions(
    ConnectionKind Kind,
    string LogPath,
    bool TimestampLogLines,
    string SerialPort = "",
    int BaudRate = 115200,
    int DataBits = 8,
    Parity Parity = Parity.None,
    StopBits StopBits = StopBits.One,
    Handshake Handshake = Handshake.None,
    string SerialLineEnding = "\r",
    string SshHost = "",
    int SshPort = 22,
    string SshUsername = "",
    string? SshPassword = null,
    string? SshPrivateKeyPath = null);
