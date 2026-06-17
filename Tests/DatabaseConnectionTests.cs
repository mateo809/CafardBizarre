using System;
using System.Threading.Tasks;
using Xunit;
using Npgsql;

namespace CafardBizarre.Tests
{
    [Trait("Category", "DatabaseConnection")]
    public class DatabaseConnectionTests : IAsyncLifetime
    {
        private string _connectionString;
        private NpgsqlConnection _connection;

        public async Task InitializeAsync()
        {
            _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") 
                ?? throw new InvalidOperationException("CONNECTION_STRING environment variable not set");
            _connection = new NpgsqlConnection(_connectionString);
            await _connection.OpenAsync();
        }

        public async Task DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }

        [Fact]
        public async Task TestDatabaseConnection_ShouldSucceed()
        {
            // Arrange & Act
            var isConnected = _connection.State == System.Data.ConnectionState.Open;

            // Assert
            Assert.True(isConnected, "Database connection should be open");
        }

        [Fact]
        public async Task TestExecuteQuery_ShouldReturnData()
        {
            // Arrange
            var command = _connection.CreateCommand();
            command.CommandText = "SELECT NOW();";

            // Act
            var result = await command.ExecuteScalarAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DateTime>(result);
        }

        [Fact]
        public async Task TestDatabaseVersion_ShouldMatch()
        {
            // Arrange
            var command = _connection.CreateCommand();
            command.CommandText = "SELECT version();";

            // Act
            var result = await command.ExecuteScalarAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Contains("PostgreSQL", result.ToString());
        }
    }
}
