using System.Text.RegularExpressions;
using AiTerminal.Services;
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

var prompt = "$ cd ";
var directory = new string('a', TerminalBuffer.Columns - prompt.Length - 1);
var firstRow = prompt + "/" + directory;
var secondRow = "/" + new string('b', TerminalBuffer.Columns - 1);
var command = new TerminalBuffer();
command.Write(firstRow);
AssertEqual("No wrap until the next printable character", firstRow, command.ToString());
command.Write(secondRow + "/leaf");
var softBreakOffsets = new List<int>();
AssertEqual("Long path wraps without losing text", string.Join(Environment.NewLine, firstRow, secondRow, "/leaf"),
    command.Render(showCursor: false, softBreakOffsets));
AssertEqual("Both display line breaks are soft", $"80,{160 + Environment.NewLine.Length}", string.Join(",", softBreakOffsets));

command.Write("\x1b[");
command.Write("2A\x1b[80GZ\x1b[1B\x1b[2G\x1b[Knode\bX");
AssertEqual("Cursor movement and redraw across wrapped rows",
    string.Join(Environment.NewLine, firstRow[..^1] + "Z", "/nodX", "/leaf"), command.ToString());
command.Write("\x1b[3;6H!\r\nOK");
AssertEqual("Absolute cursor positioning and explicit newline",
    string.Join(Environment.NewLine, firstRow[..^1] + "Z", "/nodX", "/leaf!", "OK"),
    command.Render(showCursor: false, softBreakOffsets));
AssertEqual("Explicit line break is not a soft wrap", "80", string.Join(",", softBreakOffsets));
command.Write("\x1b[3;2H\x1b[2P");
AssertEqual("Delete characters on a previously wrapped row",
    string.Join(Environment.NewLine, firstRow[..^1] + "Z", "/nodX", "/af!", "OK"), command.ToString());

var margin = new TerminalBuffer();
margin.Write(firstRow + "\x1b[0m\r\nnext");
AssertEqual("CRLF at the right margin does not add an extra row", firstRow + Environment.NewLine + "next", margin.ToString());

const int recordCount = 100_000;
var stress = new TerminalBuffer();
var rawLines = Enumerable.Range(0, recordCount).Select(i => $"log {i:D6}\r\n").ToArray();
var logDirectory = Directory.CreateTempSubdirectory("aiterm-smoke-");
var rawPath = Path.Combine(logDirectory.FullName, "raw.log");
var timestampPath = Path.Combine(logDirectory.FullName, "timestamp.log");

try
{
    await using (var log = new LineLogWriter(rawPath, timestamp: false))
    {
        for (var offset = 0; offset < recordCount; offset += 100)
        {
            var chunk = string.Concat(rawLines.Skip(offset).Take(100));
            stress.Write(chunk);
            await log.WriteDeviceOutputAsync(chunk);
        }
    }

    stress.Write("$ cd /still-responsive");
    var expectedHistory = string.Join(Environment.NewLine, rawLines.TakeLast(TerminalBuffer.MaxLines - 1)
        .Select(line => line.TrimEnd('\r', '\n')).Append("$ cd /still-responsive"));
    AssertEqual("Large output retains only recent screen rows", expectedHistory, stress.ToString());
    AssertEqual("Large raw log retains all received lines", string.Concat(rawLines), await File.ReadAllTextAsync(rawPath));

    await using (var log = new LineLogWriter(timestampPath, timestamp: true))
    {
        await log.WriteDeviceOutputAsync("first\r");
        await log.WriteDeviceOutputAsync("\nsecond\nthird");
        await log.WriteDeviceOutputAsync(" line");
    }

    var timestamped = await File.ReadAllTextAsync(timestampPath);
    var timestamps = new Regex(@"(?m)^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} ");
    AssertEqual("Timestamp prefixes include the last partial line", "3", timestamps.Matches(timestamped).Count.ToString());
    AssertEqual("Timestamping preserves device output", "first\r\nsecond\nthird line", timestamps.Replace(timestamped, ""));
}
finally
{
    File.Delete(rawPath);
    File.Delete(timestampPath);
    logDirectory.Delete();
}

Console.WriteLine("Terminal buffer and log smoke tests passed (100,000 log lines).");

static void AssertEqual(string name, string expected, string actual)
{
    if (expected == actual) return;
    Console.Error.WriteLine($"{name} failed. Expected: [{expected}] Actual: [{actual}]");
    Environment.Exit(1);
}
