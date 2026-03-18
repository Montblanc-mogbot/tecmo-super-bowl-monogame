using System;

namespace TecmoSBGame.Diagnostics;

/// <summary>
/// Ensures fatal/unhandled exceptions are written to the same log file as Console output.
///
/// MonoGame/SDL crashes and MSBuild task failures often end up on stderr only; this makes the log file
/// the source of truth so we don't need to copy/paste terminal output.
/// </summary>
public static class CrashLogging
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                Console.Error.WriteLine("[fatal] UnhandledException");
                if (e.ExceptionObject is Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                else
                {
                    Console.Error.WriteLine($"ExceptionObject: {e.ExceptionObject}");
                }

                Console.Error.WriteLine($"IsTerminating: {e.IsTerminating}");
            }
            catch
            {
                // Don't throw from a crash handler.
            }
        };

        // TaskScheduler exceptions (async void, unobserved tasks)
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                Console.Error.WriteLine("[fatal] UnobservedTaskException");
                Console.Error.WriteLine(e.Exception.ToString());
            }
            catch
            {
            }
        };
    }
}
