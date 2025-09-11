# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build & Test
- **Build solution**: `dotnet build OpenAPI.Net.sln`
- **Run tests**: `dotnet test tests/OpenAPI.Net.Tests/OpenAPI.Net.Tests.csproj`
- **Build and pack NuGet package**: `dotnet pack src/OpenAPI.Net/OpenAPI.Net.csproj`

### Running Samples
- **Console sample**: `dotnet run --project samples/Console.Sample/Console.Sample.csproj`
- **WPF sample**: `dotnet run --project samples/WPF.Sample/WPF.Sample.csproj`
- **WinForms sample**: `dotnet run --project samples/WinForms.Sample/WinForms.Sample.csproj`
- **ASP.NET sample**: `dotnet run --project samples/ASP.NET.Sample/ASP.NET.Sample.csproj`
- **Blazor WebSocket sample**: `dotnet run --project samples/Blazor.WebSocket.Sample/Blazor.WebSocket.Sample.csproj`

### Documentation
- **Build docs**: `mkdocs build` (requires Python and mkdocs-material)
- **Serve docs locally**: `mkdocs serve`

## Architecture Overview

This is a .NET 6 library that provides a reactive (RX) wrapper for the cTrader Open API, enabling WebSocket-based real-time trading operations.

### Core Components

**OpenClient** (`src/OpenAPI.Net/OpenClient.cs`)
- Main entry point and central client class
- Implements `IObservable<IMessage>` for reactive message streaming
- Manages both TCP and WebSocket connections
- Handles message queuing using System.Threading.Channels
- Uses array pools to minimize memory allocations

**Authentication System** (`src/OpenAPI.Net/Auth/`)
- `App.cs`: Application credentials management
- `Token.cs`: Authentication token handling  
- `TokenFactory.cs`: Token creation and validation

**Message Handling** (`src/OpenAPI.Net/Messages/`)
- `OpenApiMessages.cs` & `OpenApiModelMessages.cs`: Core API message types
- `OpenApiCommonMessages.cs` & `OpenApiCommonModelMessages.cs`: Common message definitions
- Generated from Protocol Buffers definitions

**Exception Types** (`src/OpenAPI.Net/Exceptions/`)
- `ConnectionException`: Connection-related failures
- `SendException` & `ReceiveException`: Message transmission errors
- `ObserverException`: Reactive stream observer errors

### Key Dependencies
- **Google.Protobuf** (3.32.0): Protocol buffer serialization
- **System.Reactive** (6.0.2): Reactive Extensions for async streams
- **Websocket.Client** (5.2.0): WebSocket connectivity

### Sample Applications
The `samples/` directory contains comprehensive working examples demonstrating various integration patterns:

**Console.Sample** - Interactive command-line demo
- **Use case**: Learning the API through interactive commands
- **Features**: Account authentication, symbol data retrieval, market data subscriptions, historical data (trendbars/ticks), trading operations
- **Commands**: `help`, `accountlist`, `reconcile`, `symbolslist`, `subscribe spot/trendbar`, `trendbar`, `tickdata`, `profile`, `disconnect`
- **Authentication flow**: Supports both OAuth flow and direct access token usage

**WPF.Sample** - Desktop trading application
- **Use case**: Full-featured desktop trading platform
- **Features**: Uses CefSharp for embedded browser authentication and charting, real-time price feeds, order management
- **Requirements**: Visual C++ 2019 x86 runtime, must run on x86/x64 (not AnyCPU)
- **Architecture**: MVVM pattern with reactive data binding

**WinForms.Sample** - Enterprise Windows Forms trading application
- **Primary Business Use Cases**:
  - **Proprietary Trading Firms**: Desktop trading workstation for professional traders requiring direct market access
  - **Fund Management**: Portfolio management tool for fund managers handling multiple accounts
  - **Trading Education**: Training platform for teaching trading concepts with live market data
  - **Broker Integration**: White-label trading platform that brokers can customize for their clients
  - **Algorithmic Trading Frontend**: GUI for monitoring and controlling automated trading strategies

- **Core Business Features**:
  - **Multi-Account Management**: ComboBox for switching between demo/live accounts with trader login display
  - **Real-time Market Data**: Subscribe/unsubscribe to spot price feeds for immediate market updates
  - **Order Management System**: Complete order lifecycle management (Market, Limit, Stop, Stop-Limit orders)
  - **Position Management**: Close positions, modify stop-loss/take-profit levels with risk management
  - **Historical Data Analysis**: Access trendbars (OHLC) and tick data for backtesting and analysis
  - **Transaction Reporting**: Comprehensive deal history, cash flow tracking for compliance and reporting
  - **Token Management**: OAuth refresh token handling for session management
  - **Message Monitoring**: Real-time API message log for debugging and audit trails

- **Technical Architecture**: 
  - **Reactive Streams**: Uses System.Reactive for handling real-time market data updates
  - **Thread-Safe Operations**: Queue-based message handling with timer-driven UI updates
  - **Async Operations**: Non-blocking API calls maintaining responsive UI during data requests
  - **Message Serialization**: Protocol Buffers for efficient binary message exchange
  - **Error Handling**: Comprehensive exception handling with user-friendly error dialogs

- **Business Value Propositions**:
  - **Low Latency**: Direct TCP connection for minimal market data delays
  - **Professional UI**: Native Windows Forms for familiar desktop application experience
  - **Compliance Ready**: Built-in message logging and transaction history for regulatory requirements
  - **Scalable**: Can handle multiple accounts and high-frequency market data feeds
  - **Cost Effective**: No licensing fees for web browsers or complex UI frameworks

**ASP.NET.Sample** - Web-based trading platform
- **Use case**: Multi-user web application for trading operations
- **Features**: SignalR Hub (`TradingAccountHub`) for real-time updates, account management, symbol data streaming
- **Key components**: `ConnectApiHostedService` for background API connections
- **Real-time features**: Live symbol prices, account data updates via SignalR

**Blazor.WebSocket.Sample** - WebAssembly trading client
- **Use case**: Client-side web application with WebSocket connectivity
- **Features**: Runs entirely in browser, WebSocket-only API connections, dependency injection setup
- **Architecture**: Uses shared services (`OpenApiService`, `TradingAccountsService`)

**Notebook.Sample** - Jupyter notebook tutorial
- **Use case**: Interactive learning and data analysis
- **Features**: Step-by-step API usage tutorial, account authorization, symbol data retrieval, historical data analysis, order creation examples
- **Format**: .NET Interactive notebook with markdown explanations and executable C# cells

**Samples.Shared** - Common utilities library
- **Components**: `OpenApiService` (main API wrapper), `TradingAccountsService` (account management)
- **Models**: `ApiCredentials`, `OrderModel`, `MarketOrderModel`, `PendingOrderModel`, `SymbolModel`
- **Use case**: Reusable components across all sample applications

### Testing
- Uses xUnit testing framework
- Test project: `tests/OpenAPI.Net.Tests/`
- Includes Visual Studio Test SDK and coverlet for coverage

### Build System
- .NET 6 SDK-style projects
- Solution file: `OpenAPI.Net.sln`
- NuGet package generation enabled for main library
- Targets AnyCPU platform
- Published as `cTrader.OpenAPI.Net` on NuGet

### Documentation
- MkDocs-based documentation in `docs/` directory
- Material theme with search and navigation features
- Published to GitHub Pages at https://spotware.github.io/OpenAPI.Net/