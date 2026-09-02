using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AiTerminal.Control;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("Usage: aiterm <connect|status|log|send|input|key|ui|disconnect> [--name value]");
    return;
}

var arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
for (var i = 1; i < args.Length; i++)
{
    if (!args[i].StartsWith("--")) continue;
    arguments[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
}

var request = new ControlRequest(args[0].ToLowerInvariant(), arguments);
var response = await SendAsync(request);
if (!response.Ok)
{
    Console.Error.WriteLine($"Error: {response.Message}");
    Environment.ExitCode = 1;
    return;
}
if (!string.IsNullOrEmpty(response.Data)) Console.Write(response.Data);
else Console.WriteLine(response.Message);

static async Task<ControlResponse> SendAsync(ControlRequest request)
{
    for (var attempt = 0; attempt < 20; attempt++)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", ControlPipe.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(300);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request));
            return JsonSerializer.Deserialize<ControlResponse>(await reader.ReadLineAsync() ?? "") ?? new ControlResponse(false, "Invalid response.");
        }
        catch (TimeoutException) when (attempt == 0)
        {
            var host = Path.Combine(AppContext.BaseDirectory, "AiTerminal.exe");
            if (!File.Exists(host)) return new ControlResponse(false, "AiTerminal.exe was not found next to aiterm.exe.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(host, "--background") { UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
        }
        catch (IOException) { await Task.Delay(150); }
    }
    return new ControlResponse(false, "AI Terminal background service is not available.");
}
