namespace AiTerminal.Control;

public sealed record ControlRequest(string Command, Dictionary<string, string?> Arguments);
public sealed record ControlResponse(bool Ok, string Message, string? Data = null);
public static class ControlPipe { public const string Name = "AiTerminal.Control.v1"; }
