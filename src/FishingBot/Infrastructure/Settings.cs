namespace DeskHelper.Infrastructure;

internal static class Settings
{
    /// <summary>Set the FISHINGBOT_ALT_TAB environment variable to "true" to alt-tab back after taking focus. Off by default.</summary>
    public static bool AltTabBack { get; } = IsTrue("FISHINGBOT_ALT_TAB");

    private static bool IsTrue(string name) =>
        string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);
}
