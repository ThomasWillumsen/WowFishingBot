public static class Utility
{
    public static void ThreadSleepRandom(int minMs, int maxMs)
    {
        var random = new Random();
        var randomTime = random.Next(minMs, maxMs);
        Thread.Sleep(randomTime);
    }
}