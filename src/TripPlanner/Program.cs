namespace TripPlanner;

/// <summary>Entry point. Commands are implemented in Cli.cs.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var storePath = Environment.GetEnvironmentVariable("TRIPPLANNER_DATA")
                        ?? Path.Combine(Directory.GetCurrentDirectory(), "trips.json");
        var store = new JsonFileStore(storePath);
        return new Cli(store).Run(args);
    }
}
