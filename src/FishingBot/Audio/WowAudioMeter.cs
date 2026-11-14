using CSCore.CoreAudioAPI;
using DeskHelper.Infrastructure;
using DeskHelper.Windowing;

namespace DeskHelper.Audio;

/// <summary>Reads the peak output volume of World of Warcraft's audio session.</summary>
internal sealed class WowAudioMeter : IDisposable
{
    private readonly AudioSessionControl _session;
    private readonly AudioMeterInformation _meter;

    private WowAudioMeter(AudioSessionControl session)
    {
        _session = session;
        _meter = session.QueryInterface<AudioMeterInformation>();
    }

    /// <summary>Peak volume as a percentage (0-100).</summary>
    public double PeakPercent => _meter.GetPeakValue() * 100;

    public static WowAudioMeter Attach()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        Log.Info($"Default Audio device found: {device.FriendlyName}");

        using var sessionManager = AudioSessionManager2.FromMMDevice(device);
        using var sessions = sessionManager.GetSessionEnumerator();

        foreach (var session in sessions)
        {
            if (!BelongsToWow(session))
            {
                session.Dispose();
                continue;
            }

            Log.Info("World of Warcraft audio session was found");
            return new WowAudioMeter(session);
        }

        throw new InvalidOperationException("No World of Warcraft audio session was found");
    }

    private static bool BelongsToWow(AudioSessionControl session)
    {
        using var session2 = session.QueryInterface<AudioSessionControl2>();
        try
        {
            return session2.Process?.ProcessName == WowProcessLocator.ProcessName;
        }
        catch (ArgumentException)
        {
            // sessions such as system sounds have no running process
            return false;
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
        _session.Dispose();
    }
}
