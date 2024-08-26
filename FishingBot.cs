using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using CSCore.CoreAudioAPI;
using WindowsInput.Native;


public class FishingBot
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    private Process _wowProcess;
    private WindowManager _windowManager;
    private AudioSessionControl _wowAudioSession;

    public FishingBot()
    {
        _wowProcess = FindWowProcess();
        _wowAudioSession = GetWowAudioSession();
        _windowManager = new WindowManager(_wowProcess);
    }

    public void Start()
    {
        Console.WriteLine("Fishing bot started");
        Console.WriteLine("====================================");
        Console.WriteLine("Cast fishing rod");

        using (_windowManager.WowWindowFocused())
        {
            Utility.ThreadSleepRandom(250, 750);
            CastFishingRod();
            Utility.ThreadSleepRandom(200, 400);
        }

        while (true)
        {
            using (var audioMeterInformation = _wowAudioSession.QueryInterface<AudioMeterInformation>())
            {
                var peakVolume = audioMeterInformation.GetPeakValue() * 100;
                if (peakVolume < 2.7)
                    continue;

                Console.WriteLine("Possible fish detected");
                Utility.ThreadSleepRandom(250, 750);

                using (_windowManager.WowWindowFocused())
                {
                    Utility.ThreadSleepRandom(250, 750);
                    try
                    {
                        ReelInFish();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    Utility.ThreadSleepRandom(500, 1500);
                    Console.WriteLine("====================================");
                    Console.WriteLine("Recast fishing rod");
                    CastFishingRod();
                    Utility.ThreadSleepRandom(200, 400);
                }
            }

            // wait 100ms before checking volume again
            Thread.Sleep(100);
        }
    }

    private void CastFishingRod()
    {
        var simulator = new WindowsInput.InputSimulator();
        simulator.Keyboard.KeyDown(VirtualKeyCode.F3);
        simulator.Keyboard.SleepRandom(50, 90);
        simulator.Keyboard.KeyUp(VirtualKeyCode.F3);
    }

    private void ReelInFish()
    {
        Console.WriteLine("Locating fishing bobber");
        var bobberCoordinates = _windowManager.LocateFishingBobber();

        // move mouse to red pixel
        var bobberX = bobberCoordinates.Item1;
        var bobberY = bobberCoordinates.Item2;

        Console.WriteLine("Reeling in fish");

        GetCursorPos(out Point currentMousePosition);
        Console.WriteLine($"Current mouse position: ({currentMousePosition.X}, {currentMousePosition.Y})");
        Console.WriteLine($"Bobber position: ({bobberX}, {bobberY})");

        var steps = 30;
        var mouseMovementTimeInMs = 200;

        var xPixelsToMove = bobberX - currentMousePosition.X;

        // Define control points for the Bezier curve
        Point startPoint = currentMousePosition;
        Point endPoint = new Point { X = bobberX, Y = bobberY };
        var controlPointModifierXPercentage = Math.Abs((decimal)xPixelsToMove / 10 / 100); // 1000 pixels = 100%
        var controlPointModifierY = new Random().Next(-100, 100) * controlPointModifierXPercentage;
        Point controlPoint = new Point { X = (startPoint.X + endPoint.X) / 2, Y = startPoint.Y + (int)controlPointModifierY }; // Adjust control point as needed

        for (var i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;

            int x = (int)(uu * startPoint.X + 2 * u * t * controlPoint.X + tt * endPoint.X);
            int y = (int)(uu * startPoint.Y + 2 * u * t * controlPoint.Y + tt * endPoint.Y);

            SetCursorPos(x, y);
            Thread.Sleep(mouseMovementTimeInMs / steps);
        }

        Utility.ThreadSleepRandom(220, 570);

        var simulator = new WindowsInput.InputSimulator();
        simulator.Mouse.RightButtonDown();
        simulator.Mouse.SleepRandom(55, 135);
        simulator.Mouse.RightButtonUp();

        Console.WriteLine("Fish reeled in");
    }

    private AudioSessionControl GetWowAudioSession()
    {
        using (var sessionManager = GetDefaultAudioSessionManager2(DataFlow.Render))
        using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
        {
            foreach (var session in sessionEnumerator)
            {
                using (var session2 = session.QueryInterface<AudioSessionControl2>())
                {
                    if (session2.Process.ProcessName != "Wow")
                        continue;
                }

                Console.WriteLine("World of Warcraft audio session was found");
                return session;
            }

            throw new Exception("No World of Warcraft audio session was found");
        }
    }

    private AudioSessionManager2 GetDefaultAudioSessionManager2(DataFlow dataFlow)
    {
        using (var enumerator = new MMDeviceEnumerator())
        {
            using (var device = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia))
            {
                Console.WriteLine("Default Audio device found: " + device.FriendlyName);
                var sessionManager = AudioSessionManager2.FromMMDevice(device);
                return sessionManager;
            }
        }
    }

    private Process FindWowProcess()
    {
        Process[] wowProcesses = Process.GetProcessesByName("Wow");
        if (wowProcesses.Length < 0)
            throw new Exception("No World of Warcraft process was found on the machine");

        if (wowProcesses.Length > 1)
            Console.WriteLine("Multiple World of Warcraft processes are running. Choosing the first one");

        var wowProcess = wowProcesses[0];
        Console.WriteLine($"A World of Warcraft process ({wowProcess.ProcessName}) was found");
        return wowProcess;
    }
}