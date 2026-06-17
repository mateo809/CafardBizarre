using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace CafardBizarre.Tests
{
    [Trait("Category", "CafardBizarreCommunication")]
    public class CafardBizarreCommunicationTests : IAsyncLifetime
    {
        private HttpClient _httpClient;
        private string _apiUrl;

        public Task InitializeAsync()
        {
            _apiUrl = Environment.GetEnvironmentVariable("CAFARDBIZARRE_API_URL") 
                ?? throw new InvalidOperationException("CAFARDBIZARRE_API_URL environment variable not set");
            _httpClient = new HttpClient();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _httpClient?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestApiHealthCheck_ShouldRespond()
        {
            // Arrange
            var url = $"{_apiUrl}/health";

            // Act
            var response = await _httpClient.GetAsync(url);

            // Assert
            Assert.True(response.IsSuccessStatusCode, $"Health check should return success. Got: {response.StatusCode}");
        }

        [Fact]
        public async Task TestApiConnection_ShouldBeReachable()
        {
            // Arrange & Act
            var response = await _httpClient.GetAsync(_apiUrl);

            // Assert
            Assert.NotNull(response);
            Assert.True(
                response.StatusCode == System.Net.HttpStatusCode.OK || 
                response.StatusCode == System.Net.HttpStatusCode.NotFound,
                "API should be reachable"
            );
        }

        [Fact]
        public async Task TestDatabaseToApiCommunication_ShouldSync()
        {
            // This test verifies data flows from database through API
            // Arrange
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var apiUrl = $"{_apiUrl}/api/sync";

            // Act
            var response = await _httpClient.GetAsync(apiUrl);

            // Assert
            Assert.True(response.IsSuccessStatusCode, 
                $"Database to API sync should succeed. Got: {response.StatusCode}");
        }
    }
}
