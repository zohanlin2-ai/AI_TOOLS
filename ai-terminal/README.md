# AI Terminal Development Plan

## Goal

Develop a Windows desktop application that provides Tera Term-like COM port and SSH terminal functionality.

Both the user and AI can read device output and send commands for verification through the same connection. The terminal view and log file must faithfully show only the data actually returned by the device.

## First Release Scope

- Support Serial (COM port) and SSH connections.
- Continuously receive device output, display it in the terminal UI, and write it to a log file.
- Let users enter and send commands from the terminal UI.
- Let AI use a simple local CLI to create connections, read recent logs, send commands, and show or hide the terminal UI.
- Connections, log writing, and AI operations must continue while the UI is hidden.
- Support only one active session in the first release.

## Technology Choices

- Platform: Windows
- Runtime: .NET 8
- UI: WPF
- Serial: `System.IO.Ports`
- SSH: SSH.NET
- Background process / CLI communication: Windows Named Pipe

## Architecture

```text
User configuration UI ─┐
Terminal UI ───────────┼─> Background Terminal Session ─> Serial / SSH
AI CLI ────────────────┘                │
                                         └─> Log file
```

The Terminal Session is the only component that owns the Serial or SSH connection. The UI only subscribes to output; hiding or closing the terminal UI must not interrupt the background connection.

## UI 1: Connection Settings

### Serial Settings

- COM port selection and rescan.
- Baud rate; default: 115200.
- Data bits, parity, stop bits, and flow control.
- Serial line ending; default: CRLF.

### SSH Settings

- Host.
- Port; default: 22.
- Username.
- Password or private-key file.
- Terminal size; default: 80 x 24.

### Shared Settings and Actions

- Log file location and browse button.
- `Add timestamp to each log line` checkbox, enabled by default.
- `Connect`: validate settings, create a session, start the connection, and open the terminal UI.
- `Cancel`: close the application.

If a connection fails, remain on the settings UI and show an error message.

## UI 2: Terminal

The terminal provides a single output view similar to Tera Term:

```text
root@genio-360-evk:/# gst-play-1.0 ...
Press 'k' to see a list of keyboard shortcuts.
Now playing /tmp/Black_Hole.mp4
Redistribute latency...
Reached end of play list.
```

- The main area shows only data received from Serial or SSH.
- Do not add source labels such as `[AI]`, `[USER]`, or `[DEVICE]`.
- Do not locally echo commands.
- Do not separately display commands sent by the user or AI.
- If the device echoes commands itself, that output naturally appears in the terminal and log; the application does not handle it separately.
- Support auto-scroll; pause following when the user reads older output, and indicate that new output is available.
- Support copy, search, and user command history.
- The command input at the bottom sends commands with Enter or the Send button.

Closing the terminal window hides the UI rather than interrupting the session. The terminal can be shown again through the system-tray icon or CLI.

## Log Rules

- Log files record only output received from the device.
- Do not record commands sent by the user.
- Do not record commands sent by AI.
- The timestamp option affects only the log file, not the terminal UI.
- Timestamps are enabled by default. When disabled, the log retains the device's original text.

Example with timestamps enabled:

```text
2026-08-21 10:00:01.125 root@genio-360-evk:/# gst-play-1.0 ...
2026-08-21 10:00:01.310 Press 'k' to see a list of keyboard shortcuts.
2026-08-21 10:00:02.101 Now playing /tmp/Black_Hole.mp4
```

Data received through Serial and SSH is not guaranteed to arrive one line at a time. The application must reassemble the data, then apply a timestamp and write a logical line only after detecting its line ending.

## AI Control CLI

Do not use MCP. AI interacts with the application through a local CLI, for example:

```powershell
aiterm connect serial --port COM3 --baud 115200 --log C:\logs\dut.log
aiterm connect ssh --host 192.168.1.10 --user root --password xxx --log C:\logs\dut.log

aiterm status
aiterm log --tail 100
aiterm send "uname -a"

aiterm ui show
aiterm ui hide

aiterm disconnect
```

The CLI passes requests to the background Terminal Session. AI can create connections, read logs, and enter commands while the terminal UI is hidden.

Operational risk from AI actions is handled by the conversation rules between AI and the user. The first release does not include MCP, command analysis, command approval, or AI identity features.

## Development Order

1. Create the .NET 8 WPF project and connection-settings UI.
2. Implement the Serial session: connect, receive, send commands, write logs, and disconnect.
3. Implement the terminal UI: real-time output, command entry, auto-scroll, search, and hiding.
4. Implement the SSH interactive shell: connect, receive, send commands, and provide basic ANSI support.
5. Extract a shared session interface to unify Serial and SSH behavior.
6. Separate the session from the UI; add background persistence and system-tray support.
7. Implement the Named Pipe and CLI: `connect`, `status`, `log`, `send`, `ui show/hide`, and `disconnect`.
8. Validate on real devices: segmented COM data, different line endings, SSH prompts, long-running logs, and continued operation while the UI is hidden.
9. Package as a single Windows executable and add CLI usage documentation.

## Out of Scope for the First Release

- MCP.
- Multiple sessions or multi-device management.
- AI/user source labels.
- Command-send records and audit logs.
- Command analysis, dangerous-command blocking, or approval workflows.
- Macros, runbooks, scheduling, or automatic reconnection.
- Advanced Tera Term features such as XMODEM and file transfer.
