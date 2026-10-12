using System.Diagnostics;
using Spectre.Console;

namespace Create_Web_API;

public static class ProcessRunner
{
    public static int Run(string fileName, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new() { StartInfo = startInfo };
        List<string> errorLines = new();

        process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                errorLines.Add(e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0 && errorLines.Count > 0)
        {
            foreach (string err in errorLines)
            {
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(err)}[/]");
            }
        }

        return process.ExitCode;
    }
}
