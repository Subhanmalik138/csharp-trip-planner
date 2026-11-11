namespace TripPlanner;

/// <summary>Day-by-day itinerary: activities must fall inside the trip and
/// must not overlap another activity on the same day.</summary>
public sealed class ItineraryService
{
    private readonly IStore _store;
    public ItineraryService(IStore store) => _store = store;

    public Activity AddActivity(Guid tripId, DateOnly date, TimeOnly start, TimeOnly end,
        string title, string location = "", string category = "General", decimal cost = 0m)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Activity title is required.");
        if (end <= start) throw new ArgumentException("Activity end time must be after start time.");
        if (cost < 0) throw new ArgumentException("Activity cost cannot be negative.");

        var data = _store.Load();
        var trip = data.Trips.FirstOrDefault(t => t.Id == tripId)
            ?? throw new InvalidOperationException($"No trip with id {tripId}.");
        if (!trip.Contains(date))
            throw new ArgumentException($"Date {date:yyyy-MM-dd} is outside the trip ({trip.StartDate:yyyy-MM-dd} – {trip.EndDate:yyyy-MM-dd}).");

        var activity = new Activity
        {
            TripId = tripId,
            Date = date,
            StartTime = start,
            EndTime = end,
            Title = title.Trim(),
            Location = location.Trim(),
            Category = category.Trim(),
            Cost = cost,
        };
        if (data.Activities.Any(a => a.TripId == tripId && a.Overlaps(activity)))
            throw new InvalidOperationException("Activity overlaps an existing activity on that day.");

        data.Activities.Add(activity);
        _store.Save(data);
        return activity;
    }

    public List<Activity> ListActivities(Guid tripId, DateOnly? date = null) =>
        _store.Load().Activities
            .Where(a => a.TripId == tripId && (date is null || a.Date == date))
            .OrderBy(a => a.Date).ThenBy(a => a.StartTime)
            .ToList();

    public void RemoveActivity(Guid tripId, Guid activityId)
    {
        var data = _store.Load();
        if (data.Activities.RemoveAll(a => a.TripId == tripId && a.Id == activityId) == 0)
            throw new InvalidOperationException($"No activity {activityId} on trip {tripId}.");
        _store.Save(data);
    }
}
