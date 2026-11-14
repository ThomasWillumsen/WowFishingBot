namespace DeskHelper.Infrastructure;

internal static class Settings
{
    /// <summary>Set the FISHINGBOT_ALT_TAB environment variable to "true" to alt-tab back after taking focus. Off by default.</summary>
    public static bool AltTabBack { get; } = IsTrue("FISHINGBOT_ALT_TAB", false);

    /// <summary>Set FISHINGBOT_USE_AUDIO to "true" to also require a bite sound before reeling in. Off by default (bobber movement only).</summary>
    public static bool UseAudio { get; } = IsTrue("FISHINGBOT_USE_AUDIO", false);

    /// <summary>Set FISHINGBOT_CURSOR_CHECK to "false" to click the bobber without first checking that the cursor changed over it. On by default.</summary>
    public static bool CursorCheck { get; } = IsTrue("FISHINGBOT_CURSOR_CHECK", true);

    private static bool IsTrue(string name, bool defaultValue) =>
        Environment.GetEnvironmentVariable(name) is { } value
            ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            : defaultValue;
}
