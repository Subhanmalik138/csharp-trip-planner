namespace TripPlanner;

/// <summary>Command-line interface. Parsing is intentionally hand-rolled:
/// one small parser, no external dependency, easy to test.</summary>
public sealed class Cli
{
    private readonly IStore _store;

    public Cli(IStore store) => _store = store;

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        return args[0] switch
        {
            "trip" => RunTrip(args[1..]),
            "activity" => RunActivity(args[1..]),
            "expense" => RunExpense(args[1..]),
            "pack" => RunPack(args[1..]),
            "report" => RunReport(args[1..]),
            "upcoming" => RunUpcoming(),
            "--help" or "-h" or "help" => Help(),
            _ => Unknown(args[0]),
        };
    }

    private int Help() { PrintHelp(); return 0; }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            TripPlanner — plan trips, itineraries, expenses and packing lists.

            Usage:
              tripplanner trip add --name <name> --destination <place> --start <yyyy-MM-dd> --end <yyyy-MM-dd> [--budget <amount>] [--currency <code>]
              tripplanner trip list
              tripplanner trip show <id>
              tripplanner trip delete <id>
              tripplanner activity add --trip <id> --date <yyyy-MM-dd> --start <HH:mm> --end <HH:mm> --title <title> [--location <place>] [--category <cat>] [--cost <amount>]
              tripplanner activity list --trip <id> [--date <yyyy-MM-dd>]
              tripplanner expense add --trip <id> --amount <amount> --category <cat> --description <text> [--date <yyyy-MM-dd>]
              tripplanner expense list --trip <id>
              tripplanner pack add --trip <id> --name <item> [--category <cat>] [--qty <n>]
              tripplanner pack check --trip <id> --item <id>
              tripplanner pack list --trip <id>
              tripplanner report --trip <id>
              tripplanner upcoming

            Data is stored in trips.json (override with TRIPPLANNER_DATA).
            """);
    }

    // --- option parsing -------------------------------------------------

    /// <summary>Parses "--key value" pairs; flags without a value map to "true".</summary>
    internal static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;
            var key = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            options[key] = value;
        }
        return options;
    }

    private static string Require(Dictionary<string, string> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || value == "true")
            throw new ArgumentException($"Missing required option --{key}.");
        return value;
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine("Error: " + message);
        return 1;
    }

    // --- trip commands --------------------------------------------------

    private int RunTrip(string[] args)
    {
        if (args.Length == 0) return Error("Usage: tripplanner trip <add|list|show|delete>");
        try
        {
            return args[0] switch
            {
                "add" => TripAdd(ParseOptions(args[1..])),
                "list" => TripList(),
                "show" => args.Length > 1 ? TripShow(args[1]) : Error("Usage: tripplanner trip show <id>"),
                "delete" => args.Length > 1 ? TripDelete(args[1]) : Error("Usage: tripplanner trip delete <id>"),
                _ => Error($"Unknown trip subcommand: {args[0]}"),
            };
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
    }

    private int TripAdd(Dictionary<string, string> o)
    {
        var service = new TripService(_store);
        var trip = service.CreateTrip(
            Require(o, "name"),
            Require(o, "destination"),
            DateOnly.Parse(Require(o, "start")),
            DateOnly.Parse(Require(o, "end")),
            o.TryGetValue("budget", out var b) ? decimal.Parse(b) : 0m,
            o.TryGetValue("currency", out var c) ? c : "USD");
        Console.WriteLine($"Created trip {trip.Id} — {trip.Name} ({trip.Destination}), {trip.DayCount} days.");
        return 0;
    }

    private int TripList()
    {
        var trips = new TripService(_store).ListTrips();
        if (trips.Count == 0) { Console.WriteLine("No trips yet. Add one with: tripplanner trip add ..."); return 0; }
        foreach (var t in trips)
            Console.WriteLine($"{t.Id}  {t.Name} — {t.Destination}  {t.StartDate:yyyy-MM-dd} → {t.EndDate:yyyy-MM-dd}  {t.Budget} {t.Currency}");
        return 0;
    }

    private int TripShow(string id)
    {
        var service = new TripService(_store);
        var trip = service.GetTrip(Guid.Parse(id));
        Console.WriteLine($"{trip.Name} — {trip.Destination}");
        Console.WriteLine($"  Dates:    {trip.StartDate:yyyy-MM-dd} → {trip.EndDate:yyyy-MM-dd} ({trip.DayCount} days)");
        Console.WriteLine($"  Budget:   {trip.Budget} {trip.Currency}");
        Console.WriteLine($"  Countdown: {trip.DaysUntil(DateOnly.FromDateTime(DateTime.Today))} days to go");
        if (trip.Notes.Length > 0) Console.WriteLine($"  Notes:    {trip.Notes}");
        return 0;
    }

    private int TripDelete(string id)
    {
        new TripService(_store).DeleteTrip(Guid.Parse(id));
        Console.WriteLine("Trip deleted (activities, expenses and packing items removed too).");
        return 0;
    }

    // --- activity commands ----------------------------------------------

    private int RunActivity(string[] args)
    {
        if (args.Length == 0) return Error("Usage: tripplanner activity <add|list>");
        try
        {
            return args[0] switch
            {
                "add" => ActivityAdd(ParseOptions(args[1..])),
                "list" => ActivityList(ParseOptions(args[1..])),
                _ => Error($"Unknown activity subcommand: {args[0]}"),
            };
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
    }

    private int ActivityAdd(Dictionary<string, string> o)
    {
        var service = new ItineraryService(_store);
        var activity = service.AddActivity(
            Guid.Parse(Require(o, "trip")),
            DateOnly.Parse(Require(o, "date")),
            TimeOnly.Parse(Require(o, "start")),
            TimeOnly.Parse(Require(o, "end")),
            Require(o, "title"),
            o.TryGetValue("location", out var l) ? l : "",
            o.TryGetValue("category", out var c) ? c : "General",
            o.TryGetValue("cost", out var cost) ? decimal.Parse(cost) : 0m);
        Console.WriteLine($"Added activity {activity.Id} — {activity.Title} on {activity.Date:yyyy-MM-dd} {activity.StartTime:HH:mm}–{activity.EndTime:HH:mm}.");
        return 0;
    }

    private int ActivityList(Dictionary<string, string> o)
    {
        var tripId = Guid.Parse(Require(o, "trip"));
        var date = o.TryGetValue("date", out var d) ? DateOnly.Parse(d) : (DateOnly?)null;
        var activities = new ItineraryService(_store).ListActivities(tripId, date);
        if (activities.Count == 0) { Console.WriteLine("No activities found."); return 0; }
        foreach (var a in activities)
            Console.WriteLine($"{a.Date:yyyy-MM-dd} {a.StartTime:HH:mm}–{a.EndTime:HH:mm}  {a.Title}  [{a.Category}]  {(a.Location.Length > 0 ? "@ " + a.Location : "")}  {a.Cost}");
        return 0;
    }

    // --- expense commands -----------------------------------------------

    private int RunExpense(string[] args)
    {
        if (args.Length == 0) return Error("Usage: tripplanner expense <add|list>");
        try
        {
            return args[0] switch
            {
                "add" => ExpenseAdd(ParseOptions(args[1..])),
                "list" => ExpenseList(ParseOptions(args[1..])),
                _ => Error($"Unknown expense subcommand: {args[0]}"),
            };
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
    }

    private int ExpenseAdd(Dictionary<string, string> o)
    {
        var service = new ExpenseService(_store);
        var expense = service.AddExpense(
            Guid.Parse(Require(o, "trip")),
            o.TryGetValue("date", out var d) ? DateOnly.Parse(d) : DateOnly.FromDateTime(DateTime.Today),
            Require(o, "category"),
            Require(o, "description"),
            decimal.Parse(Require(o, "amount")));
        Console.WriteLine($"Logged expense {expense.Id} — {expense.Amount} ({expense.Category}: {expense.Description}).");
        return 0;
    }

    private int ExpenseList(Dictionary<string, string> o)
    {
        var expenses = new ExpenseService(_store).ListExpenses(Guid.Parse(Require(o, "trip")));
        if (expenses.Count == 0) { Console.WriteLine("No expenses yet."); return 0; }
        foreach (var e in expenses)
            Console.WriteLine($"{e.Date:yyyy-MM-dd}  {e.Amount,10}  [{e.Category}]  {e.Description}");
        Console.WriteLine($"Total: {expenses.Sum(e => e.Amount)}");
        return 0;
    }

    // --- packing commands -----------------------------------------------

    private int RunPack(string[] args)
    {
        if (args.Length == 0) return Error("Usage: tripplanner pack <add|check|list>");
        try
        {
            return args[0] switch
            {
                "add" => PackAdd(ParseOptions(args[1..])),
                "check" => PackCheck(ParseOptions(args[1..])),
                "list" => PackList(ParseOptions(args[1..])),
                _ => Error($"Unknown pack subcommand: {args[0]}"),
            };
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
    }

    private int PackAdd(Dictionary<string, string> o)
    {
        var service = new PackingService(_store);
        var item = service.AddItem(
            Guid.Parse(Require(o, "trip")),
            Require(o, "name"),
            o.TryGetValue("category", out var c) ? c : "General",
            o.TryGetValue("qty", out var q) ? int.Parse(q) : 1);
        Console.WriteLine($"Added to packing list: {item.Name} x{item.Quantity} ({item.Id}).");
        return 0;
    }

    private int PackCheck(Dictionary<string, string> o)
    {
        var service = new PackingService(_store);
        var item = service.TogglePacked(Guid.Parse(Require(o, "trip")), Guid.Parse(Require(o, "item")));
        Console.WriteLine($"{(item.Packed ? "[x]" : "[ ]")} {item.Name}");
        return 0;
    }

    private int PackList(Dictionary<string, string> o)
    {
        var service = new PackingService(_store);
        var tripId = Guid.Parse(Require(o, "trip"));
        var items = service.ListItems(tripId);
        if (items.Count == 0) { Console.WriteLine("Packing list is empty."); return 0; }
        foreach (var item in items)
            Console.WriteLine($"{(item.Packed ? "[x]" : "[ ]")} {item.Name} x{item.Quantity}  [{item.Category}]");
        var (packed, total) = service.Progress(tripId);
        Console.WriteLine($"Packed {packed}/{total}.");
        return 0;
    }

    // --- reports ---------------------------------------------------------

    private int RunReport(string[] args)
    {
        if (args.Length == 0) return Error("Usage: tripplanner report --trip <id>");
        try
        {
            var o = ParseOptions(args);
            var summary = new ReportService(_store).TripSummary(Guid.Parse(Require(o, "trip")));
            Console.WriteLine(summary);
            return 0;
        }
        catch (ArgumentException ex) { return Error(ex.Message); }
        catch (InvalidOperationException ex) { return Error(ex.Message); }
    }

    private int RunUpcoming()
    {
        var trips = new ReportService(_store).UpcomingTrips(DateOnly.FromDateTime(DateTime.Today));
        if (trips.Count == 0) { Console.WriteLine("No upcoming trips."); return 0; }
        foreach (var (trip, days) in trips)
            Console.WriteLine($"{trip.StartDate:yyyy-MM-dd}  in {days} days  {trip.Name} — {trip.Destination}");
        return 0;
    }
}
