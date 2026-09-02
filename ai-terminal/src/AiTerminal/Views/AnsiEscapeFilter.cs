using System.Text;

namespace AiTerminal.Views;

/// <summary>Removes terminal control sequences from the visual-only text view.
/// The original device output is still written unchanged to the log file.</summary>
internal sealed class AnsiEscapeFilter
{
    private enum State { Text, Escape, Csi }
    private State _state;

    public string Filter(string input)
    {
        var output = new StringBuilder(input.Length);
        foreach (var character in input)
        {
            switch (_state)
            {
                case State.Text:
                    if (character == '\x1b') _state = State.Escape;
                    else output.Append(character);
                    break;
                case State.Escape:
                    _state = character == '[' ? State.Csi : State.Text;
                    break;
                case State.Csi:
                    if (character is >= '@' and <= '~') _state = State.Text;
                    break;
            }
        }
        return output.ToString();
    }
}
