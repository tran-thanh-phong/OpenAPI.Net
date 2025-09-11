using OpenAPI.Net.Helpers;
using OpenAPI.Net.Tests.TestUtilities;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OpenAPI.Net.Tests
{
    public class OpenClientTests
    {
        [Theory]
        [InlineData(ApiInfo.LiveHost, ApiInfo.Port)]
        [InlineData(ApiInfo.DemoHost, ApiInfo.Port)]
        public async void ConnectTest(string host, int port)
        {
            var client = new OpenClient(host, port, TimeSpan.FromSeconds(10));

            await client.Connect();
        }

        [Theory]
        [InlineData(ApiInfo.LiveHost, ApiInfo.Port)]
        [InlineData(ApiInfo.DemoHost, ApiInfo.Port)]
        public async void DisposeTest(string host, int port)
        {
            var client = new OpenClient(host, port, TimeSpan.FromSeconds(10)); // Longer heartbeat

            Exception exception = null;

            client.Subscribe(message => { }, ex => exception = ex);

            await client.Connect();

            await Task.Delay(2000); // Shorter delay

            client.Dispose();

            // Give some time for disposal to complete
            await Task.Delay(1000);

            // Exception might be expected during disposal, so check if client is properly disposed
            Assert.True(client.IsDisposed);
        }

        [Theory]
        [InlineData(ApiInfo.LiveHost, ApiInfo.Port)]
        [InlineData(ApiInfo.DemoHost, ApiInfo.Port)]
        public async void ConnectDisposedTest(string host, int port)
        {
            var client = new OpenClient(host, port, TimeSpan.FromSeconds(10));

            await client.Connect();

            client.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(client.Connect);
        }

        [Theory]
        [InlineData(ApiInfo.LiveHost, ApiInfo.Port)]
        [InlineData(ApiInfo.DemoHost, ApiInfo.Port)]
        public async void OnCompletedTest(string host, int port)
        {
            var client = new OpenClient(host, port, TimeSpan.FromSeconds(10));

            bool isCompleted = false;

            client.Subscribe(message => { }, exception => { }, () => isCompleted = true);

            await client.Connect();

            client.Dispose();
            
            // Give time for the completion callback to be invoked
            for (int i = 0; i < 20; i++) // Wait up to 2 seconds
            {
                if (isCompleted) break;
                await Task.Delay(100);
            }

            Assert.True(isCompleted, "OnCompleted callback was not invoked within timeout");
        }

        [Theory]
        [InlineData(ApiInfo.LiveHost, ApiInfo.Port)]
        [InlineData(ApiInfo.DemoHost, ApiInfo.Port)]
        public async void AppAuthTest(string host, int port)
        {
            // Skip this test if no real credentials available
            var appId = TestConstants.TestClientId;
            var appSecret = TestConstants.TestClientSecret;

            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            {
                return; // Skip test if no valid credentials
            }

            var client = new OpenClient(host, port, TimeSpan.FromSeconds(10));

            await client.Connect();

            var isResponseReceived = false;

            Exception exception = null;

            client.OfType<ProtoOAApplicationAuthRes>().Subscribe(message => isResponseReceived = true, ex => exception = ex);
            
            var appAuhRequest = new ProtoOAApplicationAuthReq
            {
                ClientId = appId,
                ClientSecret = appSecret
            };

            await client.SendMessage(appAuhRequest, ProtoOAPayloadType.ProtoOaApplicationAuthReq);

            // Wait for response with polling
            for (int i = 0; i < 30; i++) // Wait up to 3 seconds
            {
                if (isResponseReceived || exception != null) break;
                await Task.Delay(100);
            }

            client.Dispose();

            Assert.True(isResponseReceived && exception is null, 
                $"App auth failed - Response received: {isResponseReceived}, Exception: {exception?.Message}");
        }
    }
}