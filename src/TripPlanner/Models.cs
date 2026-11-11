namespace TripPlanner;

/// <summary>A single trip: destination, date range and overall budget.</summary>
public sealed class Trip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "USD";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whole days, inclusive of both travel days.</summary>
    public int DayCount => EndDate.DayNumber - StartDate.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    /// <summary>Days from <paramref name="today"/> until departure; negative once the trip has started.</summary>
    public int DaysUntil(DateOnly today) => StartDate.DayNumber - today.DayNumber;
}

/// <summary>One timed activity on a day of a trip.</summary>
public sealed class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public string Category { get; set; } = "General";
    public decimal Cost { get; set; }
    public string Notes { get; set; } = "";

    public bool Overlaps(Activity other) =>
        Date == other.Date && StartTime < other.EndTime && other.StartTime < EndTime;
}

/// <summary>Money spent on a trip, grouped by category.</summary>
public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public DateOnly Date { get; set; }
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>One item on a trip's packing list.</summary>
public sealed class PackingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "General";
    public int Quantity { get; set; } = 1;
    public bool Packed { get; set; }
}

/// <summary>The whole data file: every trip and everything attached to it.</summary>
public sealed class TripData
{
    public List<Trip> Trips { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();
    public List<PackingItem> PackingItems { get; set; } = new();
}
