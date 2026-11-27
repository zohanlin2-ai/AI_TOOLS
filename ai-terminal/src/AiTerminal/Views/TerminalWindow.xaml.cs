using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AiTerminal.Services;
using MessageBox = System.Windows.MessageBox;
using System.Text;

namespace AiTerminal.Views;

public partial class TerminalWindow : Window
{
    private readonly ITerminalSession _session;
    private bool _allowClose;
    private bool _rightClickStartedInSelection;
    private readonly TerminalBuffer _terminalBuffer = new();
    private readonly List<int> _displayedSoftBreaks = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _displayDirty;

    public TerminalWindow(ITerminalSession session)
    {
        InitializeComponent();
        _session = session;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(67),
        };
        _refreshTimer.Tick += (_, _) => RefreshOutput();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _refreshTimer.Start();
            else _refreshTimer.Stop();
        };
        _session.OutputReceived += OnOutputReceived;
        Title = $"AI Terminal - {(_session.IsConnected ? "Connected" : "Disconnected")}";
    }

    public void AllowClose() => _allowClose = true;
    public bool CanClose => _allowClose;

    private void OnOutputReceived(object? sender, string text)
    {
        lock (_terminalBuffer)
        {
            _terminalBuffer.Write(text);
            _displayDirty = true;
        }
    }

    private void RefreshOutput()
    {
        if (OutputBox.SelectionLength != 0 ||
            (OutputBox.IsMouseCaptureWithin && Mouse.LeftButton == MouseButtonState.Pressed)) return;

        string text;
        lock (_terminalBuffer)
        {
            if (!_displayDirty) return;
            text = _terminalBuffer.Render(showCursor: true, _displayedSoftBreaks);
            _displayDirty = false;
        }

        if (string.Equals(OutputBox.Text, text, StringComparison.Ordinal)) return;
        OutputBox.Text = text;
        OutputBox.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _session.OutputReceived -= OnOutputReceived;
        base.OnClosed(e);
    }

    private void TerminalTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && !char.IsControl(e.Text, 0)) _ = WriteRawAsync(e.Text);
        e.Handled = true;
    }

    private async void TerminalKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && TryGetControlCharacter(e.Key, out var controlCharacter))
        {
            _ = WriteRawAsync(controlCharacter);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            _ = WriteRawAsync(" ");
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            _ = WriteRawAsync("\t");
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await WriteRawAsync(_session.LineEnding);
        }
        else if (e.Key == Key.Back)
        {
            _ = WriteRawAsync("\b");
            e.Handled = true;
        }
        else if (TryGetTerminalSequence(e.Key, out var sequence)) { _ = WriteRawAsync(sequence); e.Handled = true; }
    }

    private void TerminalRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rightClickStartedInSelection = IsPointerInSelection(e.GetPosition(OutputBox));
        e.Handled = true;
    }

    private async void TerminalRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        try
        {
            if (_rightClickStartedInSelection && OutputBox.SelectionLength != 0)
            {
                var selectedText = GetSelectedText();
                if (selectedText.Length != 0)
                    System.Windows.Clipboard.SetText(selectedText, System.Windows.TextDataFormat.UnicodeText);
                return;
            }

            if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)) return;

            var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
            if (text.Length == 0) return;

            await WriteRawAsync(NormalizePastedText(text));
            OutputBox.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Paste failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _rightClickStartedInSelection = false;
        }
    }

    private bool IsPointerInSelection(System.Windows.Point point)
    {
        if (OutputBox.SelectionLength == 0) return false;

        var characterIndex = OutputBox.GetCharacterIndexFromPoint(point, snapToText: false);
        return characterIndex >= OutputBox.SelectionStart &&
               characterIndex < OutputBox.SelectionStart + OutputBox.SelectionLength;
    }

    private string GetSelectedText()
    {
        var text = OutputBox.SelectedText;
        var selectionStart = OutputBox.SelectionStart;
        var lineBreakLength = Environment.NewLine.Length;
        var copied = new StringBuilder(text.Length);
        var start = 0;

        foreach (var offset in _displayedSoftBreaks)
        {
            var index = offset - selectionStart;
            if (index < 0) continue;
            if (index + lineBreakLength > text.Length) break;
            copied.Append(text, start, index - start);
            start = index + lineBreakLength;
        }

        return copied.Append(text, start, text.Length - start).ToString();
    }

    private string NormalizePastedText(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", _session.LineEnding, StringComparison.Ordinal);

    private async Task WriteRawAsync(string text)
    {
        if (OutputBox.SelectionLength != 0) OutputBox.Select(OutputBox.SelectionStart, 0);
        try { await _session.WriteRawAsync(text); }
        catch (Exception ex) { _ = Dispatcher.BeginInvoke(() => MessageBox.Show(ex.Message, "Send failed", MessageBoxButton.OK, MessageBoxImage.Error)); }
    }

    private static bool TryGetControlCharacter(Key key, out string value)
    {
        var controls = new Dictionary<Key, string>
        {
            [Key.A] = "\x01", [Key.B] = "\x02", [Key.C] = "\x03", [Key.D] = "\x04", [Key.E] = "\x05", [Key.F] = "\x06", [Key.G] = "\x07", [Key.H] = "\x08", [Key.I] = "\x09", [Key.J] = "\x0A", [Key.K] = "\x0B", [Key.L] = "\x0C", [Key.M] = "\x0D", [Key.N] = "\x0E", [Key.O] = "\x0F", [Key.P] = "\x10", [Key.Q] = "\x11", [Key.R] = "\x12", [Key.S] = "\x13", [Key.T] = "\x14", [Key.U] = "\x15", [Key.V] = "\x16", [Key.W] = "\x17", [Key.X] = "\x18", [Key.Y] = "\x19", [Key.Z] = "\x1A", [Key.Oem4] = "\x1B", [Key.Oem5] = "\x1C", [Key.Oem6] = "\x1D", [Key.Space] = "\0",
        };
        return controls.TryGetValue(key, out value!);
    }

    private static bool TryGetTerminalSequence(Key key, out string value)
    {
        var sequences = new Dictionary<Key, string>
        {
            [Key.Escape] = "\x1B", [Key.Left] = "\x1B[D", [Key.Right] = "\x1B[C", [Key.Up] = "\x1B[A", [Key.Down] = "\x1B[B", [Key.Home] = "\x1B[H", [Key.End] = "\x1B[F", [Key.Delete] = "\x1B[3~", [Key.Insert] = "\x1B[2~", [Key.PageUp] = "\x1B[5~", [Key.PageDown] = "\x1B[6~", [Key.F1] = "\x1BOP", [Key.F2] = "\x1BOQ", [Key.F3] = "\x1BOR", [Key.F4] = "\x1BOS", [Key.F5] = "\x1B[15~", [Key.F6] = "\x1B[17~", [Key.F7] = "\x1B[18~", [Key.F8] = "\x1B[19~", [Key.F9] = "\x1B[20~", [Key.F10] = "\x1B[21~", [Key.F11] = "\x1B[23~", [Key.F12] = "\x1B[24~",
        };
        return sequences.TryGetValue(key, out value!);
    }

}
