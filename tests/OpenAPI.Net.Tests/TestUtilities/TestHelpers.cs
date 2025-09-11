using FluentAssertions;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenAPI.Net.Tests.TestUtilities;

/// <summary>
/// Common test helper methods and utilities
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a test ProtoMessage with the specified payload type and optional client message ID
    /// </summary>
    public static ProtoMessage CreateTestMessage<T>(T payload, ProtoOAPayloadType payloadType, string? clientMsgId = null)
        where T : IMessage
    {
        var message = new ProtoMessage
        {
            Payload = payload.ToByteString(),
            PayloadType = (uint)payloadType
        };
        
        if (!string.IsNullOrEmpty(clientMsgId))
        {
            message.ClientMsgId = clientMsgId;
        }
        
        return message;
    }

    /// <summary>
    /// Creates a test ProtoMessage with the specified payload type and optional client message ID
    /// </summary>
    public static ProtoMessage CreateTestMessage<T>(T payload, ProtoPayloadType payloadType, string? clientMsgId = null)
        where T : IMessage
    {
        var message = new ProtoMessage
        {
            Payload = payload.ToByteString(),
            PayloadType = (uint)payloadType
        };
        
        if (!string.IsNullOrEmpty(clientMsgId))
        {
            message.ClientMsgId = clientMsgId;
        }
        
        return message;
    }

    /// <summary>
    /// Asserts that an action completes within the specified timeout
    /// </summary>
    public static async Task ShouldCompleteWithin(this Task task, TimeSpan timeout)
    {
        using var cts = new System.Threading.CancellationTokenSource(timeout);
        
        try
        {
            await task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Task did not complete within {timeout}");
        }
    }

    /// <summary>
    /// Asserts that a function throws an exception of the specified type
    /// </summary>
    public static async Task<TException> ShouldThrowAsync<TException>(this Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            throw new InvalidOperationException($"Expected {typeof(TException).Name} but no exception was thrown");
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a test execution event for order operations
    /// </summary>
    public static ProtoOAExecutionEvent CreateTestExecutionEvent(
        ProtoOAExecutionType executionType,
        long accountId = TestConstants.TestAccountId,
        long orderId = TestConstants.TestOrderId,
        long positionId = TestConstants.TestPositionId)
    {
        return new ProtoOAExecutionEvent
        {
            CtidTraderAccountId = accountId,
            ExecutionType = executionType
        };
    }

    /// <summary>
    /// Creates a test spot event for market data
    /// </summary>
    public static ProtoOASpotEvent CreateTestSpotEvent(
        long accountId = TestConstants.TestAccountId,
        long symbolId = TestConstants.TestSymbolId,
        double bid = 1.12340,
        double ask = 1.12350)
    {
        return new ProtoOASpotEvent
        {
            CtidTraderAccountId = accountId,
            SymbolId = symbolId,
            Bid = (ulong)(bid * 100000),
            Ask = (ulong)(ask * 100000),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Creates a simple test observer for integration tests
    /// </summary>
    public static TestObserver<T> CreateTestObserver<T>(Action<T> onNext)
    {
        return new TestObserver<T>(onNext);
    }
}

/// <summary>
/// Simple test observer implementation for testing reactive streams
/// </summary>
public class TestObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception>? _onError;
    private readonly Action? _onCompleted;

    public TestObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
    {
        _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onError = onError;
        _onCompleted = onCompleted;
    }

    public void OnNext(T value) => _onNext(value);
    public void OnError(Exception error) => _onError?.Invoke(error);
    public void OnCompleted() => _onCompleted?.Invoke();
}