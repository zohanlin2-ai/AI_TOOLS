# Changelog

All notable changes and test findings are documented in this file.

## Unreleased

### Added

- Serial (COM Port) and SSH interactive-shell connections.
- Background session control through a local Named Pipe and the `aiterm` CLI.
- Device-output-only log writing with an optional invariant timestamp prefix.
- System-tray terminal restoration.
- Single-instance startup behaviour. Launching AI Terminal while it is already running now signals the existing process to restore and foreground its Terminal window (or its connection window when disconnected), preserving the active device session. The tray menu now also includes **New connection** for an explicit disconnect-and-reconfigure flow.
- Background-created terminal support. A connection started through `aiterm connect` now creates a hidden Terminal window so `aiterm ui --action show` can restore it correctly. Added `aiterm key --name enter` for sending an Enter key without adding a visible command character.
- Serial Enter now defaults to a single CR rather than CRLF. This avoids a residual LF being consumed as an empty response by interactive shell commands such as `read` on the validated target UART.
- Added `aiterm input --text <value>` for raw text input without an automatic line ending. It allows a background controller to supply input to an interactive, no-echo prompt and then send Enter explicitly with `aiterm key --name enter`.
- Right-click paste in the Terminal window. Unicode clipboard text is sent directly to the active session, and pasted line breaks are normalized to the configured connection line ending.
- Context-sensitive right-click handling: selected terminal text is copied to the Unicode clipboard; when no text is selected, right-click continues to paste clipboard text into the active session.
- Right-click copy now requires the pointer to be inside the selected text range. Right-clicking outside an existing selection pastes instead, matching terminal-style mouse behaviour.

### Validated

- COM8 at 115200 baud connected successfully to the target UART and reached its login prompt.
- A background-created connection can show and hide the existing Terminal window without reconnecting the Serial session.
- `aiterm input` followed by `aiterm key --name enter` successfully supplies data to an interactive no-echo shell prompt without device-side echo.
- The target's NetworkManager and `nmcli` successfully connected `wlan0` to a WPA-protected access point and obtained a DHCP lease.
- The target UART `ttymxc0` was validated with a systemd drop-in at `/etc/systemd/system/serial-getty@ttymxc0.service.d/autologin.conf`. After reboot, `agetty` reported `root (automatic login)` and opened a root shell without a login prompt.

### Fixed

- Fixed a scroll regression where typing could jump to the top of the terminal after batched output replaced the text. After layout, the view anchors the text caret at the end and follows the latest output.

- Long command lines now wrap on the 80-column terminal grid, with cursor positioning and line erasure handled across wrapped rows. Right-click copying removes display-only line breaks. Serial writes are serialized, and Serial and SSH decoding preserve UTF-8 characters split across reads.
- Sustained device output no longer queues a full redraw per read. The view batches refreshes and retains the latest 1,000 display lines, while device reads and log writes run off the UI thread. The log file still retains the full received output.

- **COM port reported as busy during testing:** Multiple manually launched AI Terminal instances could retain an active Serial session. Closing duplicate AI Terminal processes releases the port; run only one instance per device session.
- **ANSI escape sequences were visible as text:** Device output such as `ESC[1;31m` appeared literally in the terminal view. The visual terminal now filters ANSI CSI control sequences. The raw device data remains unchanged in the log file.
- **Typed commands were invisible until Enter:** The terminal now renders locally typed characters immediately, matching conventional terminal interaction. Local visual echo is not written to the log file.
- **Duplicate command echo after Enter:** When a device returns the completed command after Enter, the terminal suppresses the matching duplicate visual echo.
- **Space key was not included in commands:** WPF treated Space as a navigation action in the read-only output control. Space is now handled as terminal input.
- **Tab key did not trigger shell completion:** Input was previously buffered until Enter, so the device had not received the command prefix when Tab arrived. Keyboard input is now written to the Serial or SSH stream immediately; Tab therefore reaches the shell with the preceding input and can trigger completion.
- **Terminal editing and control keys:** Replaced the append-only visual view with a terminal buffer that processes CR, LF, Backspace, ANSI cursor movement, line erasure, and clear-screen sequences. Added standard control characters, cursor/navigation keys, Insert/Delete, Page Up/Down, Escape, and F1-F12 VT/ANSI sequences.
- **Up/Down command history:** Up and Down are sent directly as the standard terminal sequences `ESC[A` and `ESC[B`, allowing the remote shell to select history entries rather than moving the WPF text view.
- **Malformed ANSI parsing:** The CSI prefix `ESC [` was incorrectly treated as a complete control sequence, which exposed styling parameters such as `1;32m` in the terminal. Corrected the parser and added repeatable smoke tests for ANSI styling, Backspace redraw, cursor movement, and CR/LF behaviour.
- **Terminal cursor and output following:** Added a visual cursor marker at the terminal buffer position. The terminal now always follows new device output and scrolls to the latest data when the visible area is exceeded.
- **Output view layout:** Removed the Find field to keep the terminal UI focused on the device stream. Delayed auto-scroll until after WPF layout completes so new output reliably remains visible at the bottom.
- **Background controller access denied after an elevated launch:** If AI Terminal is started with elevated Windows privileges but `aiterm` runs as a standard user, Windows prevents the standard-user process from opening the application's Named Pipe. The CLI previously exposed this as an unhandled `UnauthorizedAccessException`. Run both processes at the same privilege level; a future improvement should present a clear diagnostic instead of a raw exception.
- **Interactive no-echo prompts via a UART:** A CRLF line ending left an LF that interactive shell `read` commands consumed as an empty response. Keyboard automation also did not reliably target the WPF terminal input. Serial Enter now defaults to CR, and the controller provides raw input plus an explicit Enter key for reliable no-echo interaction. Do not pass secrets through `aiterm send`, because a device shell may echo the complete command into the device log.

### Known Limitations

- The terminal is a text terminal, not a complete ANSI/VT terminal emulator. It filters ANSI styling sequences but does not reproduce terminal colours, cursor movement, full-screen applications, or all control-key sequences.
- Physical Serial and SSH device validation remains required for each target device, including its expected line ending, baud rate, and shell behaviour.
