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
    private const double SoundSpikeFactor = 2.5; // a bite sound must also be this many times louder than the recent background
    private const double MinChangedPercent = 4; // % of the pixels around the bobber that must change for a visible bite
    private const double VisualSpikeFactor = 3;
    private const double QuietSoundLogFloorPercent = 0.5;
    private const double QuietChangeLogFloorPercent = 0.5;
    private const VirtualKeyCode CastKey = VirtualKeyCode.VK_0;

    private const double MissChance = 1.0 / 50; // let a fish get away now and then
    private const double IdleChance = 0.08; // pause for a few seconds before recasting
    private const double ParkCursorChance = 0.85; // move the cursor away from the bobber after reeling in

    private const int LocateAttempts = 3;
    private const int MaxLocateFailuresInARow = 3;

    // after a cast the bobber needs to land and the cast sound to fade before watching starts
    private static readonly TimeSpan BobberSettleDelay = TimeSpan.FromSeconds(2);
    // a fishing cast lasts about 20-30 seconds; after this without a bite the bot recasts
    private static readonly TimeSpan CastTimeout = TimeSpan.FromSeconds(25);
    // a sound and a visible bite must happen within this time of each other to count as a fish
    private static readonly TimeSpan BitePairWindow = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private const string Separator = "====================================";

    private enum BiteOutcome { Bite, NoBobber, TimedOut, Cancelled }

    private readonly HumanInput _input = new();
    private readonly WowWindow _window;
    private readonly WowAudioMeter? _audio;
    private readonly BobberLocator _bobberLocator;

    private DateTime _lastCastTime = DateTime.MinValue;
    private BobberInfo _bobber;
    private int _locateFailuresInARow;

    public Worker()
    {
        var process = WowProcessLocator.Find();
        _audio = Settings.UseAudio ? WowAudioMeter.Attach() : null;
        Log.Info(Settings.UseAudio ? "Bite detection: bobber movement + sound" : "Bite detection: bobber movement only");
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
            switch (WaitForBite(cancellationToken))
            {
                case BiteOutcome.Bite:
                    CatchFish();
                    break;
                case BiteOutcome.NoBobber:
                    RecastWithoutCatch("Bobber not found, recasting");
                    break;
                case BiteOutcome.TimedOut:
                    RecastWithoutCatch("No bite this cast, recasting");
                    break;
            }
        }
    }

    /// <summary>Waits for the bobber to settle, finds it, then watches the bobber (and the sound, if enabled) until a bite or the cast times out.</summary>
    private BiteOutcome WaitForBite(CancellationToken cancellationToken)
    {
        var settle = BobberSettleDelay - (DateTime.Now - _lastCastTime);
        if (settle > TimeSpan.Zero && cancellationToken.WaitHandle.WaitOne(settle))
            return BiteOutcome.Cancelled;

        if (!TryLocateBobber(cancellationToken, out var bobber))
            return cancellationToken.IsCancellationRequested ? BiteOutcome.Cancelled : BiteOutcome.NoBobber;

        _bobber = bobber;
        var half = (int)Math.Clamp(bobber.Radius * 3, 40, 120);
        var watcher = new BiteWatcher(new Rectangle(bobber.Center.X - half, bobber.Center.Y - half, half * 2, half * 2));
        var sound = new SpikeDetector(FishThresholdPercent, SoundSpikeFactor);
        var visual = new SpikeDetector(MinChangedPercent, VisualSpikeFactor);

        var lastSoundSpike = DateTime.MinValue;
        var lastVisualSpike = DateTime.MinValue;
        var deadline = _lastCastTime + CastTimeout;

        while (!cancellationToken.IsCancellationRequested && DateTime.Now < deadline)
        {
            var now = DateTime.Now;

            if (_audio is not null)
            {
                var peak = _audio.PeakPercent;
                if (sound.Update(peak))
                {
                    lastSoundSpike = now;
                    Log.Debug($"Sound spike: peak {peak:F2} (background {sound.Baseline:F2})");
                }
                else if (peak >= QuietSoundLogFloorPercent)
                {
                    Log.Debug($"Sound heard: peak {peak:F2} (background {sound.Baseline:F2})");
                }
            }

            if (watcher.MeasureChange() is { } change)
            {
                if (visual.Update(change))
                {
                    lastVisualSpike = now;
                    Log.Debug($"Bobber change: {change:F1}% of pixels (background {visual.Baseline:F1}%)");
                }
                else if (change >= QuietChangeLogFloorPercent)
                {
                    Log.Debug($"Bobber moved slightly: {change:F1}% of pixels (background {visual.Baseline:F1}%)");
                }
            }

            var soundConfirms = _audio is null
                || (lastSoundSpike != DateTime.MinValue && (lastSoundSpike - lastVisualSpike).Duration() <= BitePairWindow);

            if (lastVisualSpike != DateTime.MinValue && soundConfirms)
            {
                Log.Info("Possible fish detected");
                return BiteOutcome.Bite;
            }

            cancellationToken.WaitHandle.WaitOne(PollInterval);
        }

        return cancellationToken.IsCancellationRequested ? BiteOutcome.Cancelled : BiteOutcome.TimedOut;
    }

    private bool TryLocateBobber(CancellationToken cancellationToken, out BobberInfo bobber)
    {
        Log.Info("Locating fishing bobber");

        for (var attempt = 1; attempt <= LocateAttempts; attempt++)
        {
            if (_bobberLocator.Find(saveFailedCapture: attempt == LocateAttempts) is { } found)
            {
                _locateFailuresInARow = 0;
                bobber = found;
                return true;
            }

            if (cancellationToken.WaitHandle.WaitOne(RandomDelay.Next(400, 700)))
                break;
        }

        bobber = default;
        if (cancellationToken.IsCancellationRequested)
            return false;

        // repeatedly failing means the capture area or color detection needs attention, not endless recasting
        if (++_locateFailuresInARow >= MaxLocateFailuresInARow)
            throw new InvalidOperationException($"No bobber found in {MaxLocateFailuresInARow} casts in a row. See the failed_*.png captures in the logs folder");

        return false;
    }

    private void RecastWithoutCatch(string reason)
    {
        Log.Info(reason);
        RandomDelay.Sleep(250, 750);

        using (_window.FocusTemporarily())
        {
            RandomDelay.Sleep(250, 750);
            Log.Info(Separator);
            Log.Info("Recast fishing rod");
            CastFishingRod();
            RandomDelay.Sleep(200, 400);
        }
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
        Log.Info("Reeling in fish");

        var cursorBefore = CursorMover.GetCursorHandle();
        var target = BobberLocator.PickClickPoint(_bobber);
        Log.Info($"Bobber position: ({_bobber.Center.X}, {_bobber.Center.Y}), clicking at ({target.X}, {target.Y})");

        CursorMover.MoveTo(target);
        RandomDelay.Sleep(220, 570);

        if (Settings.CursorCheck && !CursorIsOverBobber(cursorBefore))
        {
            Log.Info("The cursor doesn't indicate the bobber, skipping the click");
            return;
        }

        _input.RightClick();

        Log.Info("Fish reeled in");

        if (RandomDelay.Chance(ParkCursorChance))
        {
            RandomDelay.Sleep(400, 900);
            MoveCursorAway();
        }
    }

    // the game swaps the cursor over interactable objects, so a changed cursor means the click would hit something
    private bool CursorIsOverBobber(IntPtr cursorBefore)
    {
        if (CursorMover.GetCursorHandle() != cursorBefore)
            return true;

        // nudge around the bobber's center in case the first spot just missed it
        var reach = (int)Math.Max(4, _bobber.Radius * 0.5);
        Point[] offsets = [new(-reach, 0), new(reach, 0), new(0, -reach), new(0, reach)];

        foreach (var offset in offsets)
        {
            CursorMover.MoveTo(new Point(_bobber.Center.X + offset.X, _bobber.Center.Y + offset.Y), targetWidth: reach * 2);
            RandomDelay.Sleep(120, 250);

            if (CursorMover.GetCursorHandle() != cursorBefore)
                return true;
        }

        return false;
    }

    private void MoveCursorAway()
    {
        var area = _window.GetClientAreaOnScreen();
        var target = new Point(
            area.X + (int)(area.Width * (0.2 + Random.Shared.NextDouble() * 0.6)),
            area.Y + (int)(area.Height * (0.45 + Random.Shared.NextDouble() * 0.4)));

        CursorMover.MoveTo(target, targetWidth: 300);
    }

    public void Dispose() => _audio?.Dispose();
}
