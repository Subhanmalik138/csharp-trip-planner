# TripPlanner — CLI Travel Itinerary Planner

A small, dependency-free .NET 8 command-line app for planning trips: create trips,
build a day-by-day itinerary, track spending against a budget, and keep a packing
list — all stored in one local JSON file.

## What it does

- **Trips** — name, destination, date range, budget and currency, with a countdown
  to departure. Deleting a trip removes its activities, expenses and packing items too.
- **Itinerary** — timed activities per day. Activities must fall inside the trip's
  dates and cannot overlap another activity on the same day (back-to-back is fine).
- **Expenses** — log spending by category, see totals and per-category breakdowns,
  and check the remaining budget (activity costs count toward spending).
- **Packing list** — items with quantities, tick them off, see packed/total progress.
- **Reports** — a one-page trip summary (dates, itinerary, spending vs budget,
  packing progress) and an upcoming-trips countdown.

## Design decisions

1. **One JSON file, atomic writes.** All data lives in `trips.json` (override with
   `TRIPPLANNER_DATA`). Saves go to a temp file that is then moved over the old
   one, so a crash mid-save can never corrupt the database.
2. **Services behind an `IStore` interface.** `TripService`, `ItineraryService`,
   `ExpenseService`, `PackingService` and `ReportService` all depend on `IStore`,
   not on the file system — the test suite runs them against an in-memory store.
3. **Validation lives in the services, not the CLI.** Date ranges, activity
   overlaps and negative amounts are rejected by the service layer, so the rules
   hold no matter which front end calls them.
4. **No external dependencies.** Argument parsing is hand-rolled and the only
   packages are the xUnit test SDK — the app itself is plain .NET 8.

## Run it

```bash
dotnet run --project src/TripPlanner -- trip add \
  --name "Istanbul Week" --destination "Istanbul, Türkiye" \
  --start 2026-12-01 --end 2026-12-05 --budget 1000

dotnet run --project src/TripPlanner -- activity add \
  --trip <id> --date 2026-12-02 --start 09:00 --end 11:00 \
  --title "Hagia Sophia" --location Sultanahmet --category Sightseeing --cost 25

dotnet run --project src/TripPlanner -- expense add \
  --trip <id> --amount 18.5 --category Food --description "Kebab lunch"

dotnet run --project src/TripPlanner -- pack add --trip <id> --name Passport
dotnet run --project src/TripPlanner -- pack check --trip <id> --item <item-id>
dotnet run --project src/TripPlanner -- report --trip <id>
dotnet run --project src/TripPlanner -- upcoming
```

Or build once and run the binary directly:

```bash
dotnet build src/TripPlanner -c Release
./src/TripPlanner/bin/Release/net8.0/tripplanner trip list
```

Run `tripplanner` with no arguments (or `--help`) to see every command.

## Run the tests

```bash
dotnet test
```

26 xUnit tests cover trip validation and cascade delete, activity overlap rules,
expense totals and remaining-budget math, packing progress, report output, and
JSON store round-trips.

## Tech stack

- .NET 8 / C# 12, no runtime dependencies
- xUnit for tests
- System.Text.Json for storage
