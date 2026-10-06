# J. Simulator

J. Simulator is a .NET 8 WPF desktop application for analysing a single-server queue with M/M/1, M/G/1, and G/G/1 models.

## Features

- Analytical calculation of utilization, queue length, wait time, and empty-system probability.
- Discrete-event simulation with configurable arrival and service distributions.
- Comparison chart for M/M/1, M/G/1, and G/G/1.
- Import of J. Data `.xls` or `.xlsx` files using `Date`, `Arrival_Time`, `Service_In`, and `Service_Out`.
- PDF report export.

## Project structure

- `JSimulator.App` — WPF user interface, view models, Excel import, styling, and charts.
- `JSimulator.Core` — queueing formulas, simulation engine, domain models, and reporting logic.

## Run locally

```powershell
dotnet restore JSimulator.sln
dotnet run --project .\JSimulator.App\JSimulator.App.csproj
```

## Requirements

- Windows 10 or later
- .NET 8 SDK
