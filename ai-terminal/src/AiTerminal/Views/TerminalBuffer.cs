using System.Text;

namespace AiTerminal.Views;

/// <summary>Small VT-style screen and bounded scrollback for line-oriented terminals.</summary>
internal sealed class TerminalBuffer
{
    public const int Columns = 80;
    public const int Rows = 24;
    public const int MaxLines = 1000;

    private readonly List<Line> _lines = [new()];
    private readonly StringBuilder _escape = new();
    private int _row;
    private int _column;
    private bool _wrapPending;
    private EscapeState _escapeState;

    private int ScreenTop => Math.Max(0, _lines.Count - Rows);

    public void Write(string text)
    {
        foreach (var c in text)
        {
            if (_escapeState != EscapeState.None)
            {
                ConsumeEscape(c);
                continue;
            }

            switch (c)
            {
                case '\x1b': _escapeState = EscapeState.Start; break;
                case '\r': MoveCursor(_row, 0); break;
                case '\n': AdvanceLine(softWrap: false); break;
                case '\b':
                case '\x7f': MoveCursor(_row, _column - 1); break;
                case '\t': MoveCursor(_row, (_column / 8 + 1) * 8); break;
                default:
                    if (!char.IsControl(c)) WriteCharacter(c);
                    break;
            }
        }

        var overflow = _lines.Count - MaxLines;
        if (overflow > 0)
        {
            _lines.RemoveRange(0, overflow);
            _row -= overflow;
        }
    }

    private void WriteCharacter(char character)
    {
        if (_wrapPending)
        {
            AdvanceLine(softWrap: true);
            _column = 0;
        }

        var line = _lines[_row].Text;
        if (line.Length < _column) line.Append(' ', _column - line.Length);
        if (_column < line.Length) line[_column] = character;
        else line.Append(character);

        if (_column == Columns - 1) _wrapPending = true;
        else _column++;
    }

    private void AdvanceLine(bool softWrap)
    {
        _lines[_row].SoftWrap = softWrap;
        _row++;
        EnsureLine();
        _wrapPending = false;
    }

    public string Render(bool showCursor, List<int>? softBreakOffsets = null)
    {
        softBreakOffsets?.Clear();
        var rendered = new StringBuilder();

        for (var row = 0; row < _lines.Count; row++)
        {
            var line = _lines[row];
            var start = rendered.Length;
            rendered.Append(line.Text);
            if (showCursor && row == _row)
            {
                var cursorColumn = _column + (_wrapPending ? 1 : 0);
                if (line.Text.Length < cursorColumn) rendered.Append(' ', cursorColumn - line.Text.Length);
                rendered.Insert(start + cursorColumn, '▌');
            }

            if (row == _lines.Count - 1) break;
            if (line.SoftWrap) softBreakOffsets?.Add(rendered.Length);
            rendered.Append(Environment.NewLine);
        }

        return rendered.ToString();
    }

    public override string ToString() => Render(showCursor: false);

    private void ConsumeEscape(char c)
    {
        switch (_escapeState)
        {
            case EscapeState.Start:
                _escape.Clear();
                _escapeState = c switch
                {
                    '[' => EscapeState.Csi,
                    ']' => EscapeState.Osc,
                    '\x1b' => EscapeState.Start,
                    _ => EscapeState.None,
                };
                break;
            case EscapeState.Csi:
                if (c is >= '@' and <= '~')
                {
                    ApplyCsi(c, _escape.ToString());
                    _escapeState = EscapeState.None;
                }
                else if (c is >= ' ' and <= '?' && _escape.Length < 64)
                {
                    _escape.Append(c);
                }
                else
                {
                    _escapeState = c == '\x1b' ? EscapeState.Start : EscapeState.None;
                }
                break;
            case EscapeState.Osc:
                if (c == '\a') _escapeState = EscapeState.None;
                else if (c == '\x1b') _escapeState = EscapeState.OscEnd;
                break;
            case EscapeState.OscEnd:
                _escapeState = c is '\\' or '\a' ? EscapeState.None : EscapeState.Osc;
                break;
        }
    }

    private void ApplyCsi(char command, string parameters)
    {
        if (parameters.Length > 0 && parameters[0] is < '0' or > ';') return;
        var separator = parameters.IndexOf(';');
        var first = separator < 0 ? parameters.AsSpan() : parameters.AsSpan(0, separator);
        var column = separator < 0 ? 1 : ReadParameter(parameters.AsSpan(separator + 1));
        var amount = Math.Clamp(ReadParameter(first), 1, Columns);
        var mode = int.TryParse(first, out var value) ? value : 0;

        switch (command)
        {
            case 'A': MoveCursor(_row - amount, _column); break;
            case 'B': MoveCursor(_row + amount, _column); break;
            case 'C': MoveCursor(_row, _column + amount); break;
            case 'D': MoveCursor(_row, _column - amount); break;
            case 'E': MoveCursor(_row + amount, 0); break;
            case 'F': MoveCursor(_row - amount, 0); break;
            case 'G': MoveCursor(_row, amount - 1); break;
            case 'H':
            case 'f': MoveCursor(ScreenTop + Math.Min(amount, Rows) - 1, column - 1); break;
            case 'K': EraseLine(mode); break;
            case 'P':
                var line = _lines[_row].Text;
                if (_column < line.Length) line.Remove(_column, Math.Min(amount, line.Length - _column));
                _wrapPending = false;
                break;
            case 'J':
                if (mode is 2 or 3)
                {
                    _lines.Clear();
                    _lines.Add(new Line());
                    _row = 0;
                    _column = 0;
                    _wrapPending = false;
                }
                else if (mode == 0)
                {
                    EraseLine(mode);
                    _lines.RemoveRange(_row + 1, _lines.Count - _row - 1);
                }
                break;
        }
    }

    private static int ReadParameter(ReadOnlySpan<char> parameter) =>
        int.TryParse(parameter, out var value) && value > 0 ? value : 1;

    private void EraseLine(int mode)
    {
        var line = _lines[_row];
        switch (mode)
        {
            case 0:
                if (_column < line.Text.Length) line.Text.Length = _column;
                line.SoftWrap = false;
                break;
            case 1:
                var count = Math.Min(_column + 1, line.Text.Length);
                for (var i = 0; i < count; i++) line.Text[i] = ' ';
                break;
            case 2:
                line.Text.Clear();
                line.SoftWrap = false;
                break;
        }
    }

    private void MoveCursor(int row, int column)
    {
        var top = ScreenTop;
        _row = Math.Clamp(row, top, top + Rows - 1);
        _column = Math.Clamp(column, 0, Columns - 1);
        _wrapPending = false;
        EnsureLine();
    }

    private void EnsureLine()
    {
        while (_lines.Count <= _row) _lines.Add(new Line());
    }

    private sealed class Line
    {
        public StringBuilder Text { get; } = new();
        public bool SoftWrap { get; set; }
    }

    private enum EscapeState { None, Start, Csi, Osc, OscEnd }
}
