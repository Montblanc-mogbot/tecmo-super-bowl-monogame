using System;
using System.IO;
using System.Text;

namespace TecmoSBGame.Diagnostics;

/// <summary>
/// Simple file-backed log that tees Console output to a timestamped log file.
///
/// Goal: capture extremely verbose debug output without needing to paste it from the terminal.
/// </summary>
public static class GameLog
{
    private static readonly object _gate = new();
    private static bool _installed;
    private static string? _logPath;

    public static string? LogPath => _logPath;

    /// <summary>
    /// Installs a Console.Out / Console.Error tee that writes to a log file as well as the original streams.
    /// Safe to call multiple times.
    /// </summary>
    public static void InstallConsoleTee(string? logDir = null)
    {
        lock (_gate)
        {
            if (_installed)
                return;

            logDir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TecmoSBGame",
                "Logs");

            Directory.CreateDirectory(logDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logPath = Path.Combine(logDir, $"tecmosb_{stamp}.log");

            // Open in append mode + allow readers (tail/less) while game runs.
            var fileStream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            var fileWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            // Tee both stdout and stderr.
            var stdout = Console.Out;
            var stderr = Console.Error;

            Console.SetOut(new TeeTextWriter(stdout, fileWriter));
            Console.SetError(new TeeTextWriter(stderr, fileWriter));

            _installed = true;

            Console.WriteLine($"[log] Console tee enabled -> {_logPath}");
        }
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _a;
        private readonly TextWriter _b;

        public TeeTextWriter(TextWriter a, TextWriter b)
        {
            _a = a;
            _b = b;
        }

        public override Encoding Encoding => _a.Encoding;

        public override void Write(char value)
        {
            lock (_gate)
            {
                _a.Write(value);
                _b.Write(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_gate)
            {
                _a.Write(value);
                _b.Write(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_gate)
            {
                _a.WriteLine(value);
                _b.WriteLine(value);
            }
        }

        public override void Flush()
        {
            lock (_gate)
            {
                _a.Flush();
                _b.Flush();
            }
        }
    }
}
