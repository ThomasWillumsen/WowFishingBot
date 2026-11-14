using System.Runtime.Versioning;
using DeskHelper;
using DeskHelper.Infrastructure;

[assembly: SupportedOSPlatform("windows")]

Console.Title = "DeskHelper";
NativeMethods.timeBeginPeriod(1);

var dpiAware = NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessPerMonitorV2);
Log.Info($"Fishing bot initializing... (DPI aware: {dpiAware})");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    using var worker = new Worker();
    worker.Run(cts.Token);
    Log.Info("Fishing bot stopped");
}
catch (Exception e)
{
    Log.Info(e.Message);
    Log.Info("Press any key to exit");
    Console.ReadKey();
}
finally
{
    NativeMethods.timeEndPeriod(1);
}
