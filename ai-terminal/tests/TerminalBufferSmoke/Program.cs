using AiTerminal.Views;

var buffer = new TerminalBuffer();

buffer.Write("\x1b[1;32muart:~$ \x1b[m");
AssertEqual("ANSI styling", "uart:~$ ", buffer.ToString());

buffer.Write("cd\b \bx");
AssertEqual("Backspace redraw", "uart:~$ cx", buffer.ToString());

buffer.Write("\x1b[D/");
AssertEqual("Cursor-left overwrite", "uart:~$ c/", buffer.ToString());

buffer.Write("\x1b[A\x1b[B");
AssertEqual("Cursor up/down", "uart:~$ c/" + Environment.NewLine, buffer.ToString());

buffer.Write("\r\nOK");
AssertEqual("CRLF", "uart:~$ c/" + Environment.NewLine + Environment.NewLine + "OK", buffer.ToString());

Console.WriteLine("TerminalBuffer smoke tests passed.");

static void AssertEqual(string name, string expected, string actual)
{
    if (expected == actual) return;
    Console.Error.WriteLine($"{name} failed. Expected: [{expected}] Actual: [{actual}]");
    Environment.Exit(1);
}
