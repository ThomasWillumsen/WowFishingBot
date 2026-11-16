namespace DeskHelper.Infrastructure;

internal static class Log
{
    private const string LogDirectory = "logs";
    private const string CatchLogFile = "catches.log";

    // set the FISHINGBOT_DEBUG environment variable to "true" (see .vscode/launch.json) to enable debug output
    public static bool IsDebugEnabled { get; } =
        string.Equals(Environment.GetEnvironmentVariable("FISHINGBOT_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

    public static void Info(string message) => Console.WriteLine(message);

    /// <summary>Writes the message to the console and, in debug mode only, appends it timestamped to logs/catches.log.</summary>
    public static void Catch(string message)
    {
        Info(message);

        if (!IsDebugEnabled)
            return;

        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(Path.Combine(LogDirectory, CatchLogFile), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch (IOException e)
        {
            Info($"Could not write to the catch log: {e.Message}");
        }
    }

    public static void Debug(string message)
    {
        if (IsDebugEnabled)
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
