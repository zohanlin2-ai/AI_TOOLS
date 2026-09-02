using System.Text;

namespace AiTerminal.Views;

/// <summary>Small VT-style text buffer for line-oriented serial and shell terminals.</summary>
internal sealed class TerminalBuffer
{
    private readonly List<StringBuilder> _lines = [new()];
    private readonly StringBuilder _escape = new();
    private int _row;
    private int _column;
    private bool _inEscape;

    public void Write(string text)
    {
        foreach (var c in text)
        {
            if (_inEscape) { ConsumeEscape(c); continue; }
            if (c == '\x1b') { _inEscape = true; _escape.Clear(); continue; }
            if (c == '\r') { _column = 0; continue; }
            if (c == '\n') { _row++; EnsureLine(); continue; }
            if (c is '\b' or '\x7f') { if (_column > 0) _column--; continue; }
            if (char.IsControl(c)) continue;
            var line = _lines[_row];
            while (line.Length < _column) line.Append(' ');
            if (_column < line.Length) line[_column] = c; else line.Append(c);
            _column++;
        }
    }

    public string Render(bool showCursor)
    {
        var rendered = _lines.Select(x => x.ToString()).ToList();
        if (!showCursor) return string.Join(Environment.NewLine, rendered);

        EnsureLine();
        var line = rendered[_row];
        if (line.Length < _column) line = line.PadRight(_column);
        rendered[_row] = line.Insert(_column, "▌");
        return string.Join(Environment.NewLine, rendered);
    }

    public override string ToString() => Render(showCursor: false);

    private void ConsumeEscape(char c)
    {
        _escape.Append(c);
        if (_escape.Length == 1)
        {
            if (c != '[') _inEscape = false;
            return;
        }
        if (c is < '@' or > '~') return;
        var parameter = _escape.Length > 2 ? _escape.ToString()[1..^1] : string.Empty;
        var amount = int.TryParse(parameter.Split(';')[0], out var value) ? Math.Max(value, 1) : 1;
        switch (c)
        {
            case 'D': _column = Math.Max(0, _column - amount); break;
            case 'C': _column += amount; break;
            case 'A': _row = Math.Max(0, _row - amount); break;
            case 'B': _row += amount; EnsureLine(); break;
            case 'K': if (_column < _lines[_row].Length) _lines[_row].Remove(_column, _lines[_row].Length - _column); break;
            case 'J':
                if (parameter is "2" or "3") { _lines.Clear(); _lines.Add(new StringBuilder()); _row = 0; _column = 0; }
                break;
        }
        _inEscape = false;
    }

    private void EnsureLine()
    {
        while (_lines.Count <= _row) _lines.Add(new StringBuilder());
    }
}
