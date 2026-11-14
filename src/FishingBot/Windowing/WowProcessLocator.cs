using System.Diagnostics;
using DeskHelper.Infrastructure;

namespace DeskHelper.Windowing;

internal static class WowProcessLocator
{
    public const string ProcessName = "Wow";

    public static Process Find()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0)
            throw new InvalidOperationException("No World of Warcraft process was found on the machine");

        if (processes.Length > 1)
            Log.Info("Multiple World of Warcraft processes are running. Choosing the first one");

        var process = processes[0];
        Log.Info($"A World of Warcraft process ({process.ProcessName}) was found");
        return process;
    }
}
