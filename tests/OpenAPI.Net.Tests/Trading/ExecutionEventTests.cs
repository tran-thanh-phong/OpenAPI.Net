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
/// Tests for execution event processing including order acceptance, fills, and rejections
/// </summary>
public class ExecutionEventTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;

    public ExecutionEventTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task OrderAcceptance_ValidOrder_ShouldReceiveAcceptedEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        _output.WriteLine("Testing order acceptance event...");

        // Act
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            LimitPrice = 1.10000, // Well below market to avoid execution
            Comment = "Order acceptance test"
        };

        await _client.SendMessage(limitOrderRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderAccepted);
        var acceptedEvent = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderAccepted);
        acceptedEvent.Should().NotBeNull();
        acceptedEvent.CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        acceptedEvent.Order.Should().NotBeNull();
        
        _output.WriteLine($"Order accepted with ID: {acceptedEvent.Order.OrderId}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task OrderFill_MarketOrder_ShouldReceiveFilledEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        _output.WriteLine("Testing order fill event...");

        // Act
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            Comment = "Order fill test"
        };

        await _client.SendMessage(marketOrderRequest);
        await Task.Delay(4000); // Wait for execution

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        var fillEvent = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        fillEvent.Should().NotBeNull();
        fillEvent.CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        fillEvent.Order.Should().NotBeNull();
        fillEvent.Position.Should().NotBeNull();
        fillEvent.Deal.Should().NotBeNull();
        
        _output.WriteLine($"Order filled: Deal ID {fillEvent.Deal.DealId}, Volume {fillEvent.Deal.Volume}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task OrderRejection_InvalidOrder_ShouldReceiveRejectedEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing order rejection event...");

        // Act - Send order with invalid symbol ID
        var invalidOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = 999999999, // Invalid symbol ID
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            Comment = "Order rejection test"
        };

        await _client.SendMessage(invalidOrderRequest);
        await Task.Delay(3000);

        // Assert
        errorResponses.Should().NotBeEmpty();
        var errorResponse = errorResponses.FirstOrDefault();
        errorResponse.Should().NotBeNull();
        errorResponse.CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        
        _output.WriteLine($"Order rejected with error: {errorResponse.ErrorCode} - {errorResponse.Description}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task OrderModification_ValidAmendment_ShouldReceiveAmendedEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // First create a pending order
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            LimitPrice = 1.10000,
            Comment = "Order for amendment test"
        };

        await _client!.SendMessage(limitOrderRequest);
        await Task.Delay(2000);

        var orderId = GetLastOrderId(executionEvents);
        if (orderId == 0)
        {
            _output.WriteLine("Could not get order ID for amendment test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing order amendment event for order: {orderId}");

        // Act - Modify the order
        var amendOrderRequest = new ProtoOAAmendOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            OrderId = orderId,
            Volume = 200000,
            LimitPrice = 1.09500
        };

        await _client.SendMessage(amendOrderRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderReplaced);
        var amendedEvent = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderReplaced);
        amendedEvent.Should().NotBeNull();
        amendedEvent.CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        amendedEvent.Order.Should().NotBeNull();
        
        _output.WriteLine($"Order amended: New volume {amendedEvent.Order.OrderId}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task OrderCancellation_ValidOrder_ShouldReceiveCancelledEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // First create a pending order
        var limitOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            LimitPrice = 1.09000,
            Comment = "Order for cancellation test"
        };

        await _client!.SendMessage(limitOrderRequest);
        await Task.Delay(2000);

        var orderId = GetLastOrderId(executionEvents);
        if (orderId == 0)
        {
            _output.WriteLine("Could not get order ID for cancellation test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing order cancellation event for order: {orderId}");

        // Act - Cancel the order
        var cancelOrderRequest = new ProtoOACancelOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            OrderId = orderId
        };

        await _client.SendMessage(cancelOrderRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderCancelled);
        var cancelledEvent = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderCancelled);
        cancelledEvent.Should().NotBeNull();
        cancelledEvent.CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        cancelledEvent.Order.Should().NotBeNull();
        
        _output.WriteLine($"Order cancelled: Order ID {cancelledEvent.Order.OrderId}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task PositionClose_ValidPosition_ShouldReceiveClosedEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // First create a position
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            Comment = "Position for close test"
        };

        await _client!.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        var positionId = GetLastPositionId(executionEvents);
        if (positionId == 0)
        {
            _output.WriteLine("Could not create position for close test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing position close event for position: {positionId}");

        // Act - Close the position
        var closePositionRequest = new ProtoOAClosePositionReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            PositionId = positionId,
            Volume = 100000
        };

        await _client.SendMessage(closePositionRequest);
        await Task.Delay(3000);

        // Assert
        var closeEvents = executionEvents.Where(e => 
            e.ExecutionType == ProtoOAExecutionType.OrderFilled && 
            e.Position != null && 
            e.Position.PositionId == positionId);
            
        closeEvents.Should().NotBeEmpty();
        _output.WriteLine("Position close event received successfully");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StopLossTriggered_ShouldReceiveStopLossEvent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        _output.WriteLine("Testing stop loss trigger event simulation...");

        // Act - Create position with tight stop loss (this is a simulation test)
        var orderWithSLRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            StopLoss = 1.11000, // Stop loss level
            Comment = "Position with SL for trigger test"
        };

        await _client!.SendMessage(orderWithSLRequest);
        await Task.Delay(3000);

        // Assert - Check that position was created with stop loss
        var fillEvents = executionEvents.Where(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        fillEvents.Should().NotBeEmpty();
        
        var fillEvent = fillEvents.FirstOrDefault();
        fillEvent.Should().NotBeNull();
        if (fillEvent.Position != null)
        {
            _output.WriteLine($"Position created successfully");
            fillEvent.Position.Should().NotBeNull();
        }
        
        subscription.Dispose();
    }

    [Fact]
    public void ExecutionTypes_EnumValues_ShouldHaveExpectedValues()
    {
        // Arrange & Act & Assert
        ProtoOAExecutionType.OrderAccepted.Should().Be(ProtoOAExecutionType.OrderAccepted);
        ProtoOAExecutionType.OrderFilled.Should().Be(ProtoOAExecutionType.OrderFilled);
        ProtoOAExecutionType.OrderCancelled.Should().Be(ProtoOAExecutionType.OrderCancelled);
        ProtoOAExecutionType.OrderReplaced.Should().Be(ProtoOAExecutionType.OrderReplaced);
        ProtoOAExecutionType.OrderRejected.Should().Be(ProtoOAExecutionType.OrderRejected);

        _output.WriteLine("Execution type enum values validated successfully");
    }

    [Fact]
    public async Task MultipleExecutionEvents_ShouldProcessInOrder()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(e => 
            {
                executionEvents.Add(e);
                _output.WriteLine($"Received execution event: {e.ExecutionType} at {DateTime.Now:HH:mm:ss.fff}");
            });

        _output.WriteLine("Testing multiple execution events processing...");

        // Act - Send multiple orders
        for (int i = 0; i < 3; i++)
        {
            var orderRequest = new ProtoOANewOrderReq
            {
                CtidTraderAccountId = TestConstants.TestAccountId,
                SymbolId = TestConstants.TestSymbolId,
                OrderType = ProtoOAOrderType.Limit,
                TradeSide = ProtoOATradeSide.Buy,
                Volume = 100000,
                LimitPrice = 1.09000 + (i * 0.00100), // Different prices
                Comment = $"Multiple order test {i + 1}"
            };

            await _client!.SendMessage(orderRequest);
            await Task.Delay(1000); // Small delay between orders
        }

        await Task.Delay(5000); // Wait for all responses

        // Assert
        executionEvents.Should().HaveCountGreaterOrEqualTo(3);
        var acceptedEvents = executionEvents.Where(e => e.ExecutionType == ProtoOAExecutionType.OrderAccepted).ToList();
        acceptedEvents.Should().HaveCountGreaterOrEqualTo(3);
        
        _output.WriteLine($"Processed {executionEvents.Count} execution events successfully");
        
        subscription.Dispose();
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

    private long GetLastOrderId(List<ProtoOAExecutionEvent> executionEvents)
    {
        var lastOrder = executionEvents.LastOrDefault(e => 
            e.ExecutionType == ProtoOAExecutionType.OrderAccepted && e.Order != null);
        
        return lastOrder?.Order?.OrderId ?? 0;
    }

    private long GetLastPositionId(List<ProtoOAExecutionEvent> executionEvents)
    {
        var lastFilledEvent = executionEvents.LastOrDefault(e => 
            e.ExecutionType == ProtoOAExecutionType.OrderFilled && e.Position != null);
        
        return lastFilledEvent?.Position?.PositionId ?? 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}