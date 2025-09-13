# cTrader Open API .NET Integration Functions - Technical Documentation

## Executive Summary

This document provides a comprehensive technical overview of the cTrader Open API .NET library's supported functions based on analysis of sample applications and core library components. The library provides a reactive, high-performance wrapper for cTrader's WebSocket/TCP-based trading API, enabling real-time trading operations, market data streaming, and account management.

## 1. Core Architecture

### 1.1 OpenClient (`src/OpenAPI.Net/OpenClient.cs`)
- **Purpose**: Main entry point and central client class
- **Key Features**:
  - Implements `IObservable<IMessage>` for reactive message streaming
  - Supports both TCP and WebSocket connections
  - Uses System.Threading.Channels for message queuing
  - Array pools for memory optimization
  - Rate limiting (max 40 requests/second by default)
  - Automatic heartbeat management

### 1.2 Connection Modes
- **TCP**: Direct SSL connection to cTrader servers
- **WebSocket**: WSS connection for web-based applications
- **Dual Environment**: Separate connections for Live and Demo accounts

## 2. Authentication System

### 2.1 OAuth Flow (`src/OpenAPI.Net/Auth/`)
- **App Class**: Manages application credentials (ClientId, Secret, RedirectUri)
- **Token Class**: Handles access tokens, refresh tokens, and expiration
- **TokenFactory**: Creates and validates authentication tokens
- **Scopes**: Trading (full access) or Accounts (read-only)

### 2.2 Authentication Process
1. **Application Authorization**: `ProtoOAApplicationAuthReq` → `ProtoOAApplicationAuthRes`
2. **Account Authorization**: `ProtoOAAccountAuthReq` → `ProtoOAAccountAuthRes`
3. **Token Refresh**: `ProtoOARefreshTokenReq` → `ProtoOARefreshTokenRes`

## 3. Trading Operations

### 3.1 Order Management
**Supported Order Types:**
- **Market Orders**: `ProtoOAOrderType.Market`
- **Market Range Orders**: `ProtoOAOrderType.MarketRange` (with slippage control)
- **Limit Orders**: `ProtoOAOrderType.Limit`
- **Stop Orders**: `ProtoOAOrderType.Stop`
- **Stop Limit Orders**: `ProtoOAOrderType.StopLimit`

**Core Functions:**
- **Create Order**: `ProtoOANewOrderReq` → `ProtoOAExecutionEvent`
- **Cancel Order**: `ProtoOACancelOrderReq`
- **Modify Order**: `ProtoOAAmendOrderReq`

**Advanced Features:**
- Stop Loss / Take Profit management
- Trailing Stop Loss
- Guaranteed Stop Loss
- Order expiration timestamps
- Custom labels and comments
- Client-side order IDs

### 3.2 Position Management
**Core Functions:**
- **Close Position**: `ProtoOAClosePositionReq`
- **Modify Position SL/TP**: `ProtoOAAmendPositionSLTPReq`
- **Get Position P&L**: `ProtoOAGetPositionUnrealizedPnLReq`

**Position Lifecycle:**
- Real-time position updates via `ProtoOAExecutionEvent`
- Position status tracking (`ProtoOAPositionStatus`)
- Partial closing support
- Position modification (volume, SL/TP)

### 3.3 Execution Events
**Event Types** (`ProtoOAExecutionType`):
- `OrderAccepted`: Order successfully placed
- `OrderFilled`: Order execution completed
- `OrderReplaced`: Order modification successful
- `OrderCancelled`: Order cancellation
- `OrderRejected`: Order rejection with error details
- `OrderExpired`: Order expiration

## 4. Market Data Functions

### 4.1 Real-time Price Feeds
**Spot Price Subscriptions:**
- **Subscribe**: `ProtoOASubscribeSpotsReq` → `ProtoOASpotEvent`
- **Unsubscribe**: `ProtoOAUnsubscribeSpotsReq`
- **Features**: Bid/Ask prices, real-time updates, multiple symbol support

### 4.2 Historical Data
**Trendbar Data:**
- **Request**: `ProtoOAGetTrendbarsReq` → `ProtoOAGetTrendbarsRes`
- **Live Subscription**: `ProtoOASubscribeLiveTrendbarReq`
- **Periods**: M1, M5, M15, M30, H1, H4, D1, W1, MN1
- **OHLC Data**: Open, High, Low, Close, Volume, UTC timestamps

**Tick Data:**
- **Request**: `ProtoOAGetTickDataReq` → `ProtoOAGetTickDataRes`
- **Types**: Bid/Ask tick data
- **Precision**: Millisecond timestamps
- **Filtering**: Time range queries

### 4.3 Symbol Information
**Symbol Discovery:**
- **Light Symbols**: `ProtoOASymbolsListReq` → Basic symbol info
- **Detailed Symbols**: `ProtoOASymbolByIdReq` → Complete symbol specifications
- **Asset Information**: `ProtoOAAssetListReq` → Currency and asset details
- **Conversion Symbols**: `ProtoOASymbolsForConversionReq` → Cross-currency pairs

## 5. Account Management

### 5.1 Account Operations
**Core Functions:**
- **Account List**: `ProtoOAGetAccountListByAccessTokenReq`
- **Account Details**: Account balance, equity, margin information
- **Trader Information**: `ProtoOATraderReq` → Profile and settings
- **Account Logout**: `ProtoOAAccountLogoutReq`

### 5.2 Transaction History
**Historical Trades:**
- **Deal List**: `ProtoOADealListReq` → Trade execution history
- **Features**: Commission, swap, P&L details, execution prices
- **Filtering**: Time-based queries, account-specific data

**Cash Flow History:**
- **Request**: `ProtoOACashFlowHistoryListReq`
- **Types**: Deposits, withdrawals, bonus operations
- **Balance Tracking**: Historical balance changes

### 5.3 Account Reconciliation
**Reconcile Function:**
- **Request**: `ProtoOAReconcileReq` → `ProtoOAReconcileRes`
- **Purpose**: Synchronize account state after connection issues
- **Data**: Open positions, pending orders, account balances

## 6. Event-Driven Architecture

### 6.1 Reactive Streams
**Observable Patterns:**
- All API responses implement `IObservable<IMessage>`
- Type-safe filtering with `OfType<T>()`
- Error handling through observer pattern
- Automatic reconnection capabilities

### 6.2 Message Types
**System Messages:**
- `ProtoHeartbeatEvent`: Connection keepalive
- `ProtoOAErrorRes`: Error handling and reporting
- `ProtoOAClientDisconnectEvent`: Disconnection notifications

**Data Events:**
- `ProtoOASpotEvent`: Real-time price updates
- `ProtoOAExecutionEvent`: Trade execution notifications
- `ProtoOATrendbarEvent`: Live chart data updates

## 7. Error Handling & Resilience

### 7.1 Exception Types (`src/OpenAPI.Net/Exceptions/`)
- **ConnectionException**: Network and connection failures
- **SendException**: Message transmission errors
- **ReceiveException**: Message reception issues
- **ObserverException**: Reactive stream observer errors

### 7.2 Reliability Features
- Automatic reconnection logic
- Message queuing during disconnections
- Rate limiting and throttling
- Token refresh automation
- Connection state monitoring

## 8. Performance Optimizations

### 8.1 Memory Management
- Array pooling for message buffers
- Protocol Buffers for efficient serialization
- Channel-based message queuing
- Reactive stream disposal patterns

### 8.2 Network Efficiency
- Binary Protocol Buffers messaging
- Selective symbol subscriptions
- Batched historical data requests
- Connection multiplexing (Live/Demo)

## 9. Unit Testing Requirements

### 9.1 Core Testing Areas
1. **Connection Management**
   - TCP/WebSocket connection establishment
   - Authentication flow validation
   - Reconnection logic
   - Rate limiting behavior

2. **Message Serialization**
   - Protocol Buffers encoding/decoding
   - Message factory functionality
   - Type safety validation

3. **Trading Operations**
   - Order creation and modification
   - Position management
   - Execution event processing
   - Error scenario handling

4. **Market Data**
   - Subscription management
   - Real-time data streaming
   - Historical data retrieval
   - Symbol information queries

5. **Account Management**
   - Authentication workflows
   - Account data retrieval
   - Transaction history processing

### 9.2 Mock Testing Strategy
- **API Response Mocking**: Simulate cTrader server responses
- **Network Failure Simulation**: Test resilience patterns
- **Rate Limiting Validation**: Ensure compliance with API limits
- **Error Condition Testing**: Validate error handling paths

### 9.3 Integration Testing
- **End-to-End Workflows**: Complete trading scenarios
- **Multi-Account Testing**: Live and Demo environments
- **Performance Testing**: High-frequency message processing
- **Stress Testing**: Connection stability under load

## 10. Implementation Roadmap

### Phase 1: Foundation Testing
- Core OpenClient functionality
- Authentication system validation
- Basic connection management
- Message serialization testing

### Phase 2: Trading Operations
- Order management test suite
- Position management validation
- Execution event processing
- Risk management testing

### Phase 3: Market Data
- Real-time subscription testing
- Historical data validation
- Symbol management testing
- Performance benchmarking

### Phase 4: Advanced Features
- Error handling validation
- Resilience testing
- Performance optimization
- Documentation completion

---

**Note**: This documentation is based on analysis of the OpenAPI.Net library samples and core components. For production implementation, refer to the official cTrader Open API documentation and ensure compliance with broker-specific requirements.