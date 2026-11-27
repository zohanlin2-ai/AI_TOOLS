using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AiTerminal.Services;
using AiTerminal.Views;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                await TestTypingWithScrollbackAsync();
                Console.WriteLine("Terminal viewport smoke test passed.");
                app.Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                app.Shutdown(1);
            }
        };
        return app.Run();
    }

    private static async Task TestTypingWithScrollbackAsync()
    {
        var session = new EchoSession();
        var window = new TerminalWindow(session)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = SystemParameters.VirtualScreenLeft - 2000,
            Top = SystemParameters.VirtualScreenTop - 2000,
            Width = 820,
            Height = 300,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        window.Show();
        try
        {
            var output = (TextBox)window.FindName("OutputBox");
            var history = string.Concat(Enumerable.Range(1, 250).Select(i => $"log {i:D3}\r\n"));
            session.Receive(history + "$ ");
            await WaitUntilAsync(() => output.Text.EndsWith("$ ▌") && AtBottom(output), "Initial prompt did not scroll into view.");

            output.CaretIndex = 0;
            output.ScrollToHome();
            await WaitUntilAsync(() => output.VerticalOffset == 0, "Could not scroll to old output.");

            foreach (var key in new[] { "l", "s" })
            {
                var composition = new TextComposition(InputManager.Current, output, key);
                output.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
                });

                var prompt = "$ " + session.Input + "▌";
                await WaitUntilAsync(() => output.Text.EndsWith(prompt) && AtBottom(output)
                    && output.CaretIndex == output.Text.Length, $"Typed '{key}' but the caret or viewport did not follow the echo.");
            }

            if (session.Input != "ls") throw new InvalidOperationException("The simulated terminal did not receive both keystrokes.");
        }
        finally
        {
            window.Close();
        }
    }

    private static bool AtBottom(TextBox output) =>
        output.ExtentHeight > output.ViewportHeight &&
        output.VerticalOffset + output.ViewportHeight >= output.ExtentHeight - 1;

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            try { await Task.Delay(20, timeout.Token); }
            catch (OperationCanceledException) { throw new InvalidOperationException(message); }
        }
    }

    private sealed class EchoSession : ITerminalSession
    {
        public event EventHandler<string>? OutputReceived;
        public bool IsConnected => true;
        public string LineEnding => "\r";
        public string Input { get; private set; } = "";

        public void Receive(string text) => OutputReceived?.Invoke(this, text);

        public Task WriteRawAsync(string text, CancellationToken cancellationToken = default)
        {
            Input += text;
            Receive(text);
            return Task.CompletedTask;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(string command, CancellationToken cancellationToken = default) => WriteRawAsync(command + LineEnding, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
