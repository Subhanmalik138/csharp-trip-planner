namespace TripPlanner;

/// <summary>Expenses: logging, totals and per-category breakdowns.</summary>
public sealed class ExpenseService
{
    private readonly IStore _store;
    public ExpenseService(IStore store) => _store = store;

    public Expense AddExpense(Guid tripId, DateOnly date, string category, string description, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Expense amount must be positive.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Expense description is required.");

        var data = _store.Load();
        if (!data.Trips.Any(t => t.Id == tripId))
            throw new InvalidOperationException($"No trip with id {tripId}.");

        var expense = new Expense
        {
            TripId = tripId,
            Date = date,
            Category = category.Trim(),
            Description = description.Trim(),
            Amount = amount,
        };
        data.Expenses.Add(expense);
        _store.Save(data);
        return expense;
    }

    public List<Expense> ListExpenses(Guid tripId) =>
        _store.Load().Expenses
            .Where(e => e.TripId == tripId)
            .OrderBy(e => e.Date)
            .ToList();

    public decimal TotalSpent(Guid tripId) =>
        _store.Load().Expenses.Where(e => e.TripId == tripId).Sum(e => e.Amount);

    public Dictionary<string, decimal> SpendByCategory(Guid tripId) =>
        _store.Load().Expenses
            .Where(e => e.TripId == tripId)
            .GroupBy(e => e.Category)
            .OrderByDescending(g => g.Sum(e => e.Amount))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

    /// <summary>Budget minus spending (activity costs included); negative means over budget.</summary>
    public decimal RemainingBudget(Guid tripId)
    {
        var data = _store.Load();
        var trip = data.Trips.FirstOrDefault(t => t.Id == tripId)
            ?? throw new InvalidOperationException($"No trip with id {tripId}.");
        var spent = data.Expenses.Where(e => e.TripId == tripId).Sum(e => e.Amount)
                  + data.Activities.Where(a => a.TripId == tripId).Sum(a => a.Cost);
        return trip.Budget - spent;
    }
}
