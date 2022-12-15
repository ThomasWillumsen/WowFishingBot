internal partial class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Fishing bot initializing...");

        try
        {
            var fishingBot = new FishingBot();
            fishingBot.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}