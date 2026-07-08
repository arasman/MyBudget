using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MyBudget.Features.SharedKernel.Persistence;

/// <summary>
/// Keyed DI wrapper for raw Npgsql connections (used by Dapper query handlers).
/// Key: "postgres"
/// </summary>
public sealed class ConnectionFactory
{
    private readonly string _connectionString;

    public ConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
    }

    public IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
