namespace DeskHelper.Infrastructure;

internal static class Log
{
    // set the FISHINGBOT_DEBUG environment variable to "true" (see .vscode/launch.json) to enable debug output
    public static bool IsDebugEnabled { get; } =
        string.Equals(Environment.GetEnvironmentVariable("FISHINGBOT_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

    public static void Info(string message) => Console.WriteLine(message);

    public static void Debug(string message)
    {
        if (IsDebugEnabled)
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
