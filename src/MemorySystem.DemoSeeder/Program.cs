namespace MemorySystem.DemoSeeder;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await PrivateAlphaSeedCli.RunAsync(args, Console.Out, Console.Error);
    }
}
