# AI Terminal Development Plan

## Goal

Develop a Windows desktop application that provides Tera Term-like COM port and SSH terminal functionality.

Both the user and AI can read device output and send commands for verification through the same connection. The terminal view and log file must faithfully show only the data actually returned by the device.

## Language

- README files, user-interface text, command-line output, and any application-generated log content use English.
- Device output is written and displayed unchanged. The application does not translate, localize, or otherwise alter it.
- When timestamps are enabled, they use the invariant `yyyy-MM-dd HH:mm:ss.fff` format.

## First Release Scope

- Support Serial (COM port) and SSH connections.
- Continuously receive device output, display it in the terminal UI, and write it to a log file.
- Let users enter and send commands from the terminal UI.
- Let AI use a simple local CLI to create connections, read recent logs, send commands, and show or hide the terminal UI.
- Connections, log writing, and AI operations must continue while the UI is hidden.
- Support only one active session in the first release.

## Implementation Status

Implemented and build-verified:

- WPF connection settings window.
- Serial (COM Port) connection, device-output reception, command sending, and disconnecting.
- Terminal window that displays only device output; no local command echo or source labels are injected.
- Log writer that writes only device output and optionally prefixes each completed logical line with a timestamp.
- Closing the terminal window hides it; the system tray menu can show it again or disconnect the session.
- SSH interactive-shell sessions with password or private-key authentication.
- Local Named Pipe control service and the `aiterm` CLI for background connection, command input, recent-log retrieval, and UI visibility.
- Terminal buffer handling for device echo, cursor movement, and common control sequences.

Still requires real-device validation. Full ANSI terminal emulation is intentionally not included; output remains a plain text view of the device stream.

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
- Serial line ending; default: CR (the conventional Enter sequence for an interactive UART shell).

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
- Automatically follow the latest device output with a visible cursor. The view retains up to 1,000 display lines; the session log file keeps the full device output.
- Support selecting and copying terminal text. Screen refresh is paused while text is selected and resumes when the selection is cleared.
- The terminal uses an 80-column grid for line wrapping and cursor movement. Right-click copying rejoins soft-wrapped lines and keeps line breaks received from the device.
- Right-click copies selected terminal text only when the pointer is within that selected range. A right-click outside the selected range, or with no selection, pastes Unicode clipboard text directly to the active terminal session. Pasted line breaks use the connection's configured line ending.
- Type directly in the Terminal window. Enter sends the configured connection line ending.

Closing the terminal window hides the UI rather than interrupting the session. The terminal can be shown again through the system-tray icon or CLI.

## Log Rules

- Log files record only output received from the device.
- Do not separately add commands sent by the user or AI to the log.
- If the device echoes a command, that echoed data is device output and is retained unchanged in the log.
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
aiterm connect --type serial --port COM3 --baud 115200 --log C:\logs\dut.log
aiterm connect --type ssh --host 192.168.1.10 --ssh-port 22 --user root --password xxx --log C:\logs\dut.log

aiterm status
aiterm log --tail 100
aiterm send --command "uname -a"
aiterm input --text "partial input"
aiterm key --name enter

aiterm ui --action show
aiterm ui --action hide

aiterm disconnect
```

The CLI passes requests to the background Terminal Session. AI can create connections, read logs, and enter commands while the terminal UI is hidden.

Use `aiterm input` only for raw interactive input. Do not pass secrets through `aiterm send`, because the remote device may echo the complete command into its output and log.

Operational risk from AI actions is handled by the conversation rules between AI and the user. The first release does not include MCP, command analysis, command approval, or AI identity features.

## Development Order

1. Create the .NET 8 WPF project and connection-settings UI.
2. Implement the Serial session: connect, receive, send commands, write logs, and disconnect.
3. Implement the terminal UI: real-time output, direct keyboard input, auto-scroll, and hiding.
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
