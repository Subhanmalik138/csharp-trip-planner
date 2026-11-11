namespace TripPlanner;

/// <summary>Packing list: add items, tick them off, track progress.</summary>
public sealed class PackingService
{
    private readonly IStore _store;
    public PackingService(IStore store) => _store = store;

    public PackingItem AddItem(Guid tripId, string name, string category = "General", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.");
        if (quantity < 1) throw new ArgumentException("Quantity must be at least 1.");

        var data = _store.Load();
        if (!data.Trips.Any(t => t.Id == tripId))
            throw new InvalidOperationException($"No trip with id {tripId}.");

        var item = new PackingItem
        {
            TripId = tripId,
            Name = name.Trim(),
            Category = category.Trim(),
            Quantity = quantity,
        };
        data.PackingItems.Add(item);
        _store.Save(data);
        return item;
    }

    public PackingItem TogglePacked(Guid tripId, Guid itemId)
    {
        var data = _store.Load();
        var item = data.PackingItems.FirstOrDefault(p => p.TripId == tripId && p.Id == itemId)
            ?? throw new InvalidOperationException($"No packing item {itemId} on trip {tripId}.");
        item.Packed = !item.Packed;
        _store.Save(data);
        return item;
    }

    public List<PackingItem> ListItems(Guid tripId) =>
        _store.Load().PackingItems
            .Where(p => p.TripId == tripId)
            .OrderBy(p => p.Packed).ThenBy(p => p.Category).ThenBy(p => p.Name)
            .ToList();

    /// <summary>(packed count, total count) for the trip.</summary>
    public (int Packed, int Total) Progress(Guid tripId)
    {
        var items = _store.Load().PackingItems.Where(p => p.TripId == tripId).ToList();
        return (items.Count(p => p.Packed), items.Count);
    }
}
