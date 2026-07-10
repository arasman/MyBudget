using System.Data;
using Dapper;

namespace MyBudget.Features.SharedKernel.Persistence;

/// <summary>
/// Dapper type handlers for types not natively supported by Dapper.
/// Must be registered at application startup via <see cref="RegisterAll"/>.
/// </summary>
public static class DapperTypeHandlers
{
    public static void RegisterAll()
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    /// <summary>Maps PostgreSQL date (returned as DateOnly by Npgsql 10) to C# DateOnly.</summary>
    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value  = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value)
        {
            return value switch
            {
                DateOnly d   => d,
                DateTime dt  => DateOnly.FromDateTime(dt),
                _            => DateOnly.Parse(value.ToString()!)
            };
        }
    }
}
