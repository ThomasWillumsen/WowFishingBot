using WindowsInput;

public static class Utility
{
    public static void ThreadSleepRandom(int minMs, int maxMs)
    {
        var random = new Random();
        var randomTime = random.Next(minMs, maxMs);
        Thread.Sleep(randomTime);
    }

    public static void SleepRandom(this IKeyboardSimulator simulator, int minMs, int maxMs)
    {
        var random = new Random();
        var randomTime = random.Next(minMs, maxMs);
        simulator.Sleep(randomTime);
    }

    public static void SleepRandom(this IMouseSimulator simulator, int minMs, int maxMs)
    {
        var random = new Random();
        var randomTime = random.Next(minMs, maxMs);
        simulator.Sleep(randomTime);
    }
}