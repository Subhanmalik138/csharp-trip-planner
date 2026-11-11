using System.Text;

namespace TripPlanner;

/// <summary>Read-only reporting: trip summaries and upcoming-trip countdowns.</summary>
public sealed class ReportService
{
    private readonly IStore _store;
    public ReportService(IStore store) => _store = store;

    /// <summary>Upcoming trips (start date today or later), soonest first.</summary>
    public List<(Trip Trip, int DaysUntil)> UpcomingTrips(DateOnly today) =>
        _store.Load().Trips
            .Where(t => t.StartDate >= today)
            .OrderBy(t => t.StartDate)
            .Select(t => (t, t.DaysUntil(today)))
            .ToList();

    /// <summary>One printable summary: dates, itinerary, spending vs budget, packing progress.</summary>
    public string TripSummary(Guid tripId)
    {
        var data = _store.Load();
        var trip = data.Trips.FirstOrDefault(t => t.Id == tripId)
            ?? throw new InvalidOperationException($"No trip with id {tripId}.");

        var activities = data.Activities.Where(a => a.TripId == tripId).ToList();
        var expenses = data.Expenses.Where(e => e.TripId == tripId).ToList();
        var items = data.PackingItems.Where(p => p.TripId == tripId).ToList();

        var activityCost = activities.Sum(a => a.Cost);
        var expenseTotal = expenses.Sum(e => e.Amount);
        var spent = activityCost + expenseTotal;
        var remaining = trip.Budget - spent;
        var packed = items.Count(p => p.Packed);

        var sb = new StringBuilder();
        sb.AppendLine($"{trip.Name} — {trip.Destination}");
        sb.AppendLine($"  Dates:      {trip.StartDate:yyyy-MM-dd} → {trip.EndDate:yyyy-MM-dd} ({trip.DayCount} days)");
        sb.AppendLine($"  Itinerary:  {activities.Count} activities across {activities.Select(a => a.Date).Distinct().Count()} days");
        sb.AppendLine($"  Spending:   {spent} {trip.Currency} (expenses {expenseTotal}, activities {activityCost})");
        sb.AppendLine($"  Budget:     {trip.Budget} {trip.Currency} — {(remaining >= 0 ? $"{remaining} remaining" : $"{-remaining} OVER BUDGET")}");
        sb.AppendLine($"  Packing:    {packed}/{items.Count} packed");

        if (expenses.Count > 0)
        {
            sb.AppendLine("  Spend by category:");
            foreach (var (category, amount) in expenses
                         .GroupBy(e => e.Category)
                         .OrderByDescending(g => g.Sum(e => e.Amount))
                         .Select(g => (g.Key, g.Sum(e => e.Amount))))
                sb.AppendLine($"    {category}: {amount} {trip.Currency}");
        }

        return sb.ToString().TrimEnd();
    }
}
