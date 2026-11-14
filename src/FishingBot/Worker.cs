using System.Drawing;
using DeskHelper.Audio;
using DeskHelper.Infrastructure;
using DeskHelper.Input;
using DeskHelper.Vision;
using DeskHelper.Windowing;
using WindowsInput.Native;

namespace DeskHelper;

internal sealed class Worker : IDisposable
{
    private const double FishThresholdPercent = 10; // often hits 13-15 with 100% master volume and sound
    private const double QuietSoundLogFloorPercent = 0.5;
    private const VirtualKeyCode CastKey = VirtualKeyCode.VK_0;

    private const double MissChance = 1.0 / 50; // let a fish get away now and then
    private const double IdleChance = 0.08; // pause for a few seconds before recasting
    private const double ParkCursorChance = 0.85; // move the cursor away from the bobber after reeling in

    private static readonly TimeSpan IgnoreSoundAfterCast = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private const string Separator = "====================================";

    private readonly HumanInput _input = new();
    private readonly WowWindow _window;
    private readonly WowAudioMeter _audio;
    private readonly BobberLocator _bobberLocator;

    private DateTime _lastCastTime = DateTime.MinValue;

    public Worker()
    {
        var process = WowProcessLocator.Find();
        _audio = WowAudioMeter.Attach();
        _window = new WowWindow(process, _input);
        _bobberLocator = new BobberLocator(_window);
    }

    public void Run(CancellationToken cancellationToken)
    {
        Log.Info("Fishing bot started");
        Log.Info(Separator);
        Log.Info("Cast fishing rod");

        using (_window.FocusTemporarily())
        {
            RandomDelay.Sleep(250, 750);
            CastFishingRod();
            RandomDelay.Sleep(200, 400);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (FishDetected())
                CatchFish();

            cancellationToken.WaitHandle.WaitOne(PollInterval);
        }
    }

    private bool FishDetected()
    {
        var peak = _audio.PeakPercent;

        // the cast itself makes noise, so ignore sounds right after casting
        if (DateTime.Now - _lastCastTime < IgnoreSoundAfterCast)
        {
            if (peak >= QuietSoundLogFloorPercent)
                Log.Debug($"Sound ignored (within {IgnoreSoundAfterCast.TotalSeconds}s of cast): peak {peak:F2}");
            return false;
        }

        if (peak < FishThresholdPercent)
        {
            // log quieter sounds too, so the threshold can be tuned
            if (peak >= QuietSoundLogFloorPercent)
                Log.Debug($"Sound heard: peak {peak:F2} (below threshold {FishThresholdPercent})");
            return false;
        }

        Log.Debug($"Sound heard: peak {peak:F2} (>= threshold {FishThresholdPercent})");
        Log.Info("Possible fish detected");
        return true;
    }

    private void CatchFish()
    {
        var missed = RandomDelay.Chance(MissChance);
        if (missed)
        {
            // a player who missed the bite notices later, then simply recasts
            Log.Info("Letting this fish get away");
            RandomDelay.Sleep(2500, 5000);
        }
        else
        {
            RandomDelay.Sleep(250, 750);
        }

        using (_window.FocusTemporarily())
        {
            RandomDelay.Sleep(250, 750);

            if (!missed)
            {
                try
                {
                    ReelInFish();
                }
                catch (Exception e)
                {
                    Log.Info(e.Message);
                }
            }

            RandomDelay.Sleep(500, 1500);

            if (RandomDelay.Chance(IdleChance))
            {
                Log.Info("Pausing for a moment");
                RandomDelay.Sleep(3000, 8000);
            }

            Log.Info(Separator);
            Log.Info("Recast fishing rod");
            CastFishingRod();
            RandomDelay.Sleep(200, 400);
        }
    }

    private void CastFishingRod()
    {
        _input.PressKey(CastKey);
        _lastCastTime = DateTime.Now;
    }

    private void ReelInFish()
    {
        Log.Info("Locating fishing bobber");
        var bobber = _bobberLocator.Locate();

        Log.Info("Reeling in fish");
        Log.Info($"Bobber position: ({bobber.X}, {bobber.Y})");

        CursorMover.MoveTo(bobber);
        RandomDelay.Sleep(220, 570);
        _input.RightClick();

        Log.Info("Fish reeled in");

        if (RandomDelay.Chance(ParkCursorChance))
        {
            RandomDelay.Sleep(400, 900);
            MoveCursorAway();
        }
    }

    private void MoveCursorAway()
    {
        var area = _window.GetClientAreaOnScreen();
        var target = new Point(
            area.X + (int)(area.Width * (0.2 + Random.Shared.NextDouble() * 0.6)),
            area.Y + (int)(area.Height * (0.45 + Random.Shared.NextDouble() * 0.4)));

        CursorMover.MoveTo(target, targetWidth: 300);
    }

    public void Dispose() => _audio.Dispose();
}
