# TradeSoft

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**TradeSoft** is a high-performance, modular trading system simulation and backtesting engine built with .NET 8.0 and WPF. Designed for scalability and speed, it leverages a pipelined architecture to process market data efficiently.

---

## Features

- **High-Performance Engine**: Utilizes a `Pipeline` pattern with efficient data handling using `Disruptor` concepts for high-throughput processing.
- **Modular Architecture**: Clean separation between Domain, Infrastructure, Engine, and UI layers ( Modular Monolith Architecture).
- **Advanced Backtesting**: Supports multi-file tick providers and complex strategy execution.
- **Robust Persistence**: Uses **SQLite** for efficient data storage and retrieval.
- **Extensible**: easy to add new strategies, data providers, and pipeline handlers.

## Architecture

The solution follows a **Layered Architecture** ensuring separation of concerns:

| Layer | Project | Description |
|-------|---------|-------------|
| **Presentation** | `TradeSoft.UI` | WPF application for user interaction and visualization. |
| **Application** | `TradeSoft.Engine` | Orchestrates backtests and processing logic (Pipelines, Strategies). |
| **Domain** | `TradeSoft.Core` | Core entities (Ticks, Orders), DTOs, and Interfaces. No external dependencies. |
| **Infrastructure** | `TradeSoft.Infrastructure` | Database access , File I/O, and external service integrations. |

## Technology Stack

- **Framework**: .NET 8.0
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Database**: SQLite 
- **Testing**: xUnit 

## Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or JetBrains Rider or VS Code

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/TradeSoft.git
   cd TradeSoft
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the Solution**
   ```bash
   dotnet build --configuration Release
   ```

4. **Run the Application**
   ```bash
   dotnet run --project TradeSoft.UI/TradeSoft.UI.csproj
   ```


## Contributing
- Martin MÉNOT
- Louis GESLIN
- Inès MAKINDE
- Alejandro CHARLAND
- Pierre-Ugo PELLE
- Adam GUEMOUNE


## Disclaimer
This tool is for educational purposes or experimentation only and is not intended for production use. We are not responsible for any financial loss you may incur by using this software. Use it at your own risk.


## License

Distributed under the MIT License. See `LICENSE` for more information.
