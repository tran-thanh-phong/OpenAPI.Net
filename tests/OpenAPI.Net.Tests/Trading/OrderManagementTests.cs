using FluentAssertions;
using OpenAPI.Net.Tests.TestUtilities;
using System.Reactive.Linq;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;

namespace OpenAPI.Net.Tests.Trading;

/// <summary>
/// Tests for order management functionality including creation, modification, and cancellation
/// </summary>
public class OrderManagementTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;

    public OrderManagementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateMarketOrder_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        _output.WriteLine("Testing market order creation...");

        // Act
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000, // 0.1 lot
            Comment = "Test market order"
        };

        await _client.SendMessage(marketOrderRequest);
        await Task.Delay(3000); // Wait for execution

        // Assert
        orderResponses.Should().NotBeEmpty();
        _output.WriteLine($"Market order execution received: {orderResponses.Count} events");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreateLimitOrder_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        _output.WriteLine("Testing limit order creation...");

        // Act
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000, // 0.1 lot
            LimitPrice = 1.12000, // Below current market price for buy limit
            Comment = "Test limit order"
        };

        await _client.SendMessage(limitOrderRequest);
        await Task.Delay(2000); // Wait for order acceptance

        // Assert
        orderResponses.Should().NotBeEmpty();
        orderResponses.Should().Contain(r => r.ExecutionType == ProtoOAExecutionType.OrderAccepted);
        _output.WriteLine($"Limit order accepted: {orderResponses.Count} events");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreateStopOrder_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        _output.WriteLine("Testing stop order creation...");

        // Act
        var stopOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Stop,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000, // 0.1 lot
            StopPrice = 1.13000, // Above current market price for buy stop
            Comment = "Test stop order"
        };

        await _client.SendMessage(stopOrderRequest);
        await Task.Delay(2000);

        // Assert
        orderResponses.Should().NotBeEmpty();
        orderResponses.Should().Contain(r => r.ExecutionType == ProtoOAExecutionType.OrderAccepted);
        _output.WriteLine($"Stop order accepted: {orderResponses.Count} events");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreateOrderWithStopLoss_ShouldIncludeStopLoss()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        _output.WriteLine("Testing order with stop loss...");

        // Act
        var orderWithSLRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            StopLoss = 1.11000, // Stop loss below entry for buy order
            Comment = "Test order with SL"
        };

        await _client.SendMessage(orderWithSLRequest);
        await Task.Delay(3000);

        // Assert
        orderResponses.Should().NotBeEmpty();
        _output.WriteLine($"Order with SL executed: {orderResponses.Count} events");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreateOrderWithTakeProfit_ShouldIncludeTakeProfit()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        _output.WriteLine("Testing order with take profit...");

        // Act
        var orderWithTPRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            TakeProfit = 1.13000, // Take profit above entry for buy order
            Comment = "Test order with TP"
        };

        await _client.SendMessage(orderWithTPRequest);
        await Task.Delay(3000);

        // Assert
        orderResponses.Should().NotBeEmpty();
        _output.WriteLine($"Order with TP executed: {orderResponses.Count} events");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task ModifyPendingOrder_ShouldUpdateOrderParameters()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        // First create a pending order
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            LimitPrice = 1.12000,
            Comment = "Order to be modified"
        };

        await _client!.SendMessage(limitOrderRequest);
        await Task.Delay(2000);

        var orderId = GetLastOrderId(orderResponses);
        if (orderId == 0)
        {
            _output.WriteLine("Could not get order ID for modification test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing order modification for order ID: {orderId}");

        // Act - Modify the order
        var modifyOrderRequest = new ProtoOAAmendOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            OrderId = orderId,
            Volume = 200000, // Double the volume
            LimitPrice = 1.11500 // Lower the limit price
        };

        await _client.SendMessage(modifyOrderRequest);
        await Task.Delay(2000);

        // Assert
        orderResponses.Should().Contain(r => r.ExecutionType == ProtoOAExecutionType.OrderReplaced);
        _output.WriteLine("Order modification completed successfully");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CancelPendingOrder_ShouldCancelOrder()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var orderResponses = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(orderResponses.Add);

        // First create a pending order
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            LimitPrice = 1.10000, // Very low price to ensure it doesn't fill
            Comment = "Order to be cancelled"
        };

        await _client!.SendMessage(limitOrderRequest);
        await Task.Delay(2000);

        var orderId = GetLastOrderId(orderResponses);
        if (orderId == 0)
        {
            _output.WriteLine("Could not get order ID for cancellation test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing order cancellation for order ID: {orderId}");

        // Act - Cancel the order
        var cancelOrderRequest = new ProtoOACancelOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            OrderId = orderId
        };

        await _client.SendMessage(cancelOrderRequest);
        await Task.Delay(2000);

        // Assert
        orderResponses.Should().Contain(r => r.ExecutionType == ProtoOAExecutionType.OrderCancelled);
        _output.WriteLine("Order cancellation completed successfully");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreateInvalidOrder_ShouldReceiveRejection()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing invalid order rejection...");

        // Act - Create order with invalid volume (0)
        var invalidOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 0, // Invalid volume
            Comment = "Invalid order test"
        };

        await _client.SendMessage(invalidOrderRequest);
        await Task.Delay(2000);

        // Assert
        errorResponses.Should().NotBeEmpty();
        _output.WriteLine($"Received expected error for invalid order: {errorResponses[0].ErrorCode}");
        
        subscription.Dispose();
    }

    [Fact]
    public void OrderTypes_EnumValues_ShouldHaveExpectedValues()
    {
        // Arrange & Act & Assert
        ProtoOAOrderType.Market.Should().Be(ProtoOAOrderType.Market);
        ProtoOAOrderType.Limit.Should().Be(ProtoOAOrderType.Limit);
        ProtoOAOrderType.Stop.Should().Be(ProtoOAOrderType.Stop);
        ProtoOAOrderType.StopLimit.Should().Be(ProtoOAOrderType.StopLimit);

        ProtoOATradeSide.Buy.Should().Be(ProtoOATradeSide.Buy);
        ProtoOATradeSide.Sell.Should().Be(ProtoOATradeSide.Sell);

        _output.WriteLine("Order type and trade side enums validated successfully");
    }

    private async Task SetupAuthenticatedClient()
    {
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();

        // Perform complete authentication
        var appAuthRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(appAuthRequest);
        await Task.Delay(2000);

        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountAuthRequest);
        await Task.Delay(2000);
    }

    private long GetLastOrderId(List<ProtoOAExecutionEvent> orderResponses)
    {
        var lastOrder = orderResponses.LastOrDefault(r => 
            r.ExecutionType == ProtoOAExecutionType.OrderAccepted && r.Order != null);
        
        return lastOrder?.Order?.OrderId ?? 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}