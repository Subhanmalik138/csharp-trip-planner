namespace TripPlanner;

/// <summary>Trips: creation with date validation, listing, lookup and cascade delete.</summary>
public sealed class TripService
{
    private readonly IStore _store;
    public TripService(IStore store) => _store = store;

    public Trip CreateTrip(string name, string destination, DateOnly start, DateOnly end,
        decimal budget = 0m, string currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Trip name is required.");
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Destination is required.");
        if (end < start) throw new ArgumentException("End date cannot be before start date.");
        if (budget < 0) throw new ArgumentException("Budget cannot be negative.");

        var data = _store.Load();
        var trip = new Trip
        {
            Name = name.Trim(),
            Destination = destination.Trim(),
            StartDate = start,
            EndDate = end,
            Budget = budget,
            Currency = currency.Trim().ToUpperInvariant(),
        };
        data.Trips.Add(trip);
        _store.Save(data);
        return trip;
    }

    public List<Trip> ListTrips() =>
        _store.Load().Trips.OrderBy(t => t.StartDate).ToList();

    public Trip GetTrip(Guid id)
    {
        var trip = _store.Load().Trips.FirstOrDefault(t => t.Id == id);
        return trip ?? throw new InvalidOperationException($"No trip with id {id}.");
    }

    /// <summary>Deletes the trip and everything attached to it.</summary>
    public void DeleteTrip(Guid id)
    {
        var data = _store.Load();
        if (data.Trips.RemoveAll(t => t.Id == id) == 0)
            throw new InvalidOperationException($"No trip with id {id}.");
        data.Activities.RemoveAll(a => a.TripId == id);
        data.Expenses.RemoveAll(e => e.TripId == id);
        data.PackingItems.RemoveAll(p => p.TripId == id);
        _store.Save(data);
    }
}