using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Google.Protobuf;

namespace OpenAPI.Net.Tests.Mocks;

/// <summary>
/// Simplified mock implementation of OpenClient for basic testing
/// </summary>
public class MockOpenClient : IDisposable
{
    private readonly Subject<IMessage> _messageSubject;
    private readonly List<IMessage> _sentMessages;
    private bool _isConnected;
    private bool _isDisposed;

    public MockOpenClient()
    {
        _messageSubject = new Subject<IMessage>();
        _sentMessages = new List<IMessage>();
        _isConnected = false;
        _isDisposed = false;
        
        // Mock properties
        Host = "mock.demo.ctrader.com";
        Port = 5035;
        MaxRequestPerSecond = 40;
        HeartbeatInterval = TimeSpan.FromSeconds(25);
        IsUsingWebSocket = false;
        LastSentMessageTime = DateTimeOffset.MinValue;
    }

    // Properties that mimic OpenClient
    public string Host { get; }
    public int Port { get; }
    public int MaxRequestPerSecond { get; }
    public TimeSpan HeartbeatInterval { get; }
    public bool IsUsingWebSocket { get; }
    public bool IsDisposed => _isDisposed;
    public bool IsCompleted => false;
    public bool IsTerminated => false;
    public DateTimeOffset LastSentMessageTime { get; private set; }
    public int MessagesQueueCount => 0;

    // Events and observables
    public IObservable<IMessage> Messages => _messageSubject;
    public IList<IMessage> SentMessages => _sentMessages.AsReadOnly();

    public Task Connect()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockOpenClient));
            
        _isConnected = true;
        return Task.CompletedTask;
    }

    public Task SendMessage<T>(T message) where T : IMessage
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockOpenClient));
            
        if (!_isConnected)
            throw new InvalidOperationException("Client not connected");

        _sentMessages.Add(message);
        LastSentMessageTime = DateTimeOffset.UtcNow;
        
        // Simulate basic responses
        Task.Run(async () =>
        {
            await Task.Delay(100);
            SimulateBasicResponse(message);
        });
        
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(IObserver<IMessage> observer)
    {
        return _messageSubject.Subscribe(observer);
    }

    public void SimulateMessage(IMessage message)
    {
        if (!_isDisposed)
        {
            _messageSubject.OnNext(message);
        }
    }

    private void SimulateBasicResponse<T>(T message) where T : IMessage
    {
        // Simulate simple responses based on request type
        switch (message)
        {
            case ProtoOAApplicationAuthReq:
                _messageSubject.OnNext(new ProtoOAApplicationAuthRes());
                break;
                
            case ProtoOAAccountAuthReq accountReq:
                _messageSubject.OnNext(new ProtoOAAccountAuthRes
                {
                    CtidTraderAccountId = accountReq.CtidTraderAccountId
                });
                break;
                
            case ProtoOANewOrderReq orderReq:
                var executionEvent = new ProtoOAExecutionEvent
                {
                    CtidTraderAccountId = orderReq.CtidTraderAccountId,
                    ExecutionType = ProtoOAExecutionType.OrderAccepted
                };
                _messageSubject.OnNext(executionEvent);
                break;
                
            case ProtoOAReconcileReq reconcileReq:
                _messageSubject.OnNext(new ProtoOAReconcileRes
                {
                    CtidTraderAccountId = reconcileReq.CtidTraderAccountId
                });
                break;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _messageSubject?.OnCompleted();
            _messageSubject?.Dispose();
        }
    }
}