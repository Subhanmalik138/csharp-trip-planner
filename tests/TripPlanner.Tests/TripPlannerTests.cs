using TripPlanner;
using Xunit;

namespace TripPlanner.Tests;

public sealed class TripServiceTests
{
    private static (TripService Service, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        return (new TripService(store), store);
    }

    private static Trip MakeTrip(TripService s, string name = "Istanbul", DateOnly? start = null, DateOnly? end = null, decimal budget = 1000m) =>
        s.CreateTrip(name, "Türkiye", start ?? new DateOnly(2026, 12, 1), end ?? new DateOnly(2026, 12, 5), budget);

    [Fact]
    public void CreateTrip_SetsFieldsAndDayCount()
    {
        var (s, _) = Setup();
        var trip = MakeTrip(s);
        Assert.Equal(5, trip.DayCount);
        Assert.Equal("USD", trip.Currency);
        Assert.NotEqual(Guid.Empty, trip.Id);
    }

    [Fact]
    public void CreateTrip_EndBeforeStart_Throws()
    {
        var (s, _) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.CreateTrip("Bad", "Nowhere", new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void CreateTrip_NegativeBudget_Throws()
    {
        var (s, _) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.CreateTrip("Bad", "Nowhere", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2), -5m));
    }

    [Fact]
    public void CreateTrip_EmptyName_Throws()
    {
        var (s, _) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.CreateTrip("  ", "Nowhere", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 2)));
    }

    [Fact]
    public void ListTrips_OrderedByStartDate()
    {
        var (s, _) = Setup();
        MakeTrip(s, "Later", new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 12));
        MakeTrip(s, "Earlier", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3));
        var trips = s.ListTrips();
        Assert.Equal("Earlier", trips[0].Name);
        Assert.Equal("Later", trips[1].Name);
    }

    [Fact]
    public void GetTrip_UnknownId_Throws()
    {
        var (s, _) = Setup();
        Assert.Throws<InvalidOperationException>(() => s.GetTrip(Guid.NewGuid()));
    }

    [Fact]
    public void DeleteTrip_RemovesTripAndAttachedData()
    {
        var store = new InMemoryStore();
        var trips = new TripService(store);
        var trip = MakeTrip(trips);
        new ItineraryService(store).AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(9, 0), new TimeOnly(10, 0), "Museum");
        new ExpenseService(store).AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Lunch", 20m);
        new PackingService(store).AddItem(trip.Id, "Passport");

        trips.DeleteTrip(trip.Id);

        Assert.Empty(trips.ListTrips());
        Assert.Empty(new ItineraryService(store).ListActivities(trip.Id));
        Assert.Empty(new ExpenseService(store).ListExpenses(trip.Id));
        Assert.Empty(new PackingService(store).ListItems(trip.Id));
    }

    [Fact]
    public void Trip_DaysUntil_CountsDownToDeparture()
    {
        var trip = new Trip { StartDate = new DateOnly(2026, 12, 10), EndDate = new DateOnly(2026, 12, 12) };
        Assert.Equal(9, trip.DaysUntil(new DateOnly(2026, 12, 1)));
        Assert.Equal(0, trip.DaysUntil(new DateOnly(2026, 12, 10)));
        Assert.Equal(-1, trip.DaysUntil(new DateOnly(2026, 12, 11)));
    }
}

public sealed class ItineraryServiceTests
{
    private static (ItineraryService Service, Trip Trip) Setup()
    {
        var store = new InMemoryStore();
        var trip = new TripService(store).CreateTrip("Istanbul", "Türkiye",
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 1000m);
        return (new ItineraryService(store), trip);
    }

    [Fact]
    public void AddActivity_ValidActivity_Stored()
    {
        var (s, trip) = Setup();
        var a = s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(9, 0), new TimeOnly(11, 0), "Hagia Sophia", "Sultanahmet", "Sightseeing", 25m);
        Assert.Single(s.ListActivities(trip.Id));
        Assert.Equal("Hagia Sophia", a.Title);
    }

    [Fact]
    public void AddActivity_OutsideTripDates_Throws()
    {
        var (s, trip) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.AddActivity(trip.Id, new DateOnly(2026, 12, 20),
                new TimeOnly(9, 0), new TimeOnly(10, 0), "Too late"));
    }

    [Fact]
    public void AddActivity_OverlappingActivity_Throws()
    {
        var (s, trip) = Setup();
        s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(9, 0), new TimeOnly(11, 0), "First");
        Assert.Throws<InvalidOperationException>(() =>
            s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
                new TimeOnly(10, 30), new TimeOnly(12, 0), "Overlapping"));
    }

    [Fact]
    public void AddActivity_BackToBack_IsAllowed()
    {
        var (s, trip) = Setup();
        s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(9, 0), new TimeOnly(11, 0), "First");
        // Ends exactly when the next starts: not an overlap.
        var second = s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(11, 0), new TimeOnly(12, 0), "Second");
        Assert.Equal(2, s.ListActivities(trip.Id).Count);
        Assert.Equal("Second", second.Title);
    }

    [Fact]
    public void AddActivity_EndBeforeStart_Throws()
    {
        var (s, trip) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.AddActivity(trip.Id, new DateOnly(2026, 12, 2),
                new TimeOnly(11, 0), new TimeOnly(9, 0), "Backwards"));
    }

    [Fact]
    public void ListActivities_FiltersByDate_AndOrdersByTime()
    {
        var (s, trip) = Setup();
        s.AddActivity(trip.Id, new DateOnly(2026, 12, 3), new TimeOnly(14, 0), new TimeOnly(15, 0), "Afternoon");
        s.AddActivity(trip.Id, new DateOnly(2026, 12, 2), new TimeOnly(14, 0), new TimeOnly(15, 0), "Late");
        s.AddActivity(trip.Id, new DateOnly(2026, 12, 2), new TimeOnly(9, 0), new TimeOnly(10, 0), "Early");
        var day2 = s.ListActivities(trip.Id, new DateOnly(2026, 12, 2));
        Assert.Equal(2, day2.Count);
        Assert.Equal("Early", day2[0].Title);
        Assert.Equal("Late", day2[1].Title);
    }
}

public sealed class ExpenseServiceTests
{
    private static (ExpenseService Service, Trip Trip, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        var trip = new TripService(store).CreateTrip("Istanbul", "Türkiye",
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 500m);
        return (new ExpenseService(store), trip, store);
    }

    [Fact]
    public void AddExpense_NonPositiveAmount_Throws()
    {
        var (s, trip, _) = Setup();
        Assert.Throws<ArgumentException>(() =>
            s.AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Nothing", 0m));
    }

    [Fact]
    public void TotalSpent_AndSpendByCategory_SumCorrectly()
    {
        var (s, trip, _) = Setup();
        s.AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Breakfast", 10m);
        s.AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Dinner", 25m);
        s.AddExpense(trip.Id, new DateOnly(2026, 12, 3), "Transport", "Metro", 5m);
        Assert.Equal(40m, s.TotalSpent(trip.Id));
        var byCat = s.SpendByCategory(trip.Id);
        Assert.Equal(35m, byCat["Food"]);
        Assert.Equal(5m, byCat["Transport"]);
    }

    [Fact]
    public void RemainingBudget_IncludesActivityCosts()
    {
        var (s, trip, store) = Setup();
        s.AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Lunch", 100m);
        new ItineraryService(store).AddActivity(trip.Id, new DateOnly(2026, 12, 2),
            new TimeOnly(9, 0), new TimeOnly(10, 0), "Tour", cost: 50m);
        Assert.Equal(350m, s.RemainingBudget(trip.Id));
    }

    [Fact]
    public void RemainingBudget_CanGoNegative_WhenOverBudget()
    {
        var (s, trip, _) = Setup();
        s.AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Hotel", "Suite", 800m);
        Assert.Equal(-300m, s.RemainingBudget(trip.Id));
    }
}

public sealed class PackingServiceTests
{
    private static (PackingService Service, Trip Trip) Setup()
    {
        var store = new InMemoryStore();
        var trip = new TripService(store).CreateTrip("Istanbul", "Türkiye",
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5));
        return (new PackingService(store), trip);
    }

    [Fact]
    public void TogglePacked_FlipsState()
    {
        var (s, trip) = Setup();
        var item = s.AddItem(trip.Id, "Passport");
        Assert.False(item.Packed);
        Assert.True(s.TogglePacked(trip.Id, item.Id).Packed);
        Assert.False(s.TogglePacked(trip.Id, item.Id).Packed);
    }

    [Fact]
    public void Progress_CountsPackedVsTotal()
    {
        var (s, trip) = Setup();
        var a = s.AddItem(trip.Id, "Passport");
        s.AddItem(trip.Id, "Charger");
        s.AddItem(trip.Id, "Sunglasses");
        s.TogglePacked(trip.Id, a.Id);
        Assert.Equal((1, 3), s.Progress(trip.Id));
    }

    [Fact]
    public void AddItem_ZeroQuantity_Throws()
    {
        var (s, trip) = Setup();
        Assert.Throws<ArgumentException>(() => s.AddItem(trip.Id, "Nothing", quantity: 0));
    }
}

public sealed class ReportServiceTests
{
    private static (ReportService Service, Trip Trip, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        var trip = new TripService(store).CreateTrip("Istanbul", "Türkiye",
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 100m);
        return (new ReportService(store), trip, store);
    }

    [Fact]
    public void TripSummary_ReportsOverBudget()
    {
        var (s, trip, store) = Setup();
        new ExpenseService(store).AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Hotel", "Suite", 150m);
        var summary = s.TripSummary(trip.Id);
        Assert.Contains("OVER BUDGET", summary);
        Assert.Contains("50", summary);
    }

    [Fact]
    public void TripSummary_ReportsPackingProgress()
    {
        var (s, trip, store) = Setup();
        var packing = new PackingService(store);
        var item = packing.AddItem(trip.Id, "Passport");
        packing.AddItem(trip.Id, "Charger");
        packing.TogglePacked(trip.Id, item.Id);
        Assert.Contains("1/2 packed", s.TripSummary(trip.Id));
    }

    [Fact]
    public void UpcomingTrips_OnlyFuture_OrderedSoonestFirst()
    {
        var store = new InMemoryStore();
        var trips = new TripService(store);
        trips.CreateTrip("Past", "Nowhere", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));
        trips.CreateTrip("Far", "Nowhere", new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 5));
        trips.CreateTrip("Near", "Nowhere", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 3));
        var upcoming = new ReportService(store).UpcomingTrips(new DateOnly(2026, 11, 11));
        Assert.Equal(2, upcoming.Count);
        Assert.Equal("Near", upcoming[0].Trip.Name);
        Assert.Equal(20, upcoming[0].DaysUntil);
        Assert.Equal("Far", upcoming[1].Trip.Name);
    }
}

public sealed class JsonFileStoreTests
{
    [Fact]
    public void RoundTrip_PreservesTripsAndChildren()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new JsonFileStore(path);
            var trip = new TripService(store).CreateTrip("Istanbul", "Türkiye",
                new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 1000m);
            new ExpenseService(store).AddExpense(trip.Id, new DateOnly(2026, 12, 2), "Food", "Lunch", 15m);

            var reloaded = new JsonFileStore(path).Load();
            Assert.Single(reloaded.Trips);
            Assert.Equal("Istanbul", reloaded.Trips[0].Name);
            Assert.Single(reloaded.Expenses);
            Assert.Equal(15m, reloaded.Expenses[0].Amount);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyData()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.Empty(new JsonFileStore(path).Load().Trips);
    }
}
