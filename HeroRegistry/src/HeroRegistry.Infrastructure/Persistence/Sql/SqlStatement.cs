using System.Data;
using System.Data.Common;

namespace HeroRegistry.Infrastructure.Persistence.Sql;

/// <summary>SQL text plus its parameters. The text never contains values, only parameter names.</summary>
public sealed record SqlStatement(string Sql, IReadOnlyList<SqlParameterValue> Parameters);

/// <summary>
/// A parameter with an explicit type and size, so SQL Server sees exactly the column's type
/// (for example NVARCHAR(100) or CHAR(2)) and reuses one cached plan per statement.
/// </summary>
/// <param name="Value">The value. <see langword="null"/> is sent as SQL NULL.</param>
/// <param name="Size">String size: the maximum length, -1 for MAX, 0 for non-string types.</param>
public sealed record SqlParameterValue(string Name, object? Value, DbType DbType, int Size);

internal static class DbCommandExtensions
{
    public static void AddParameters(this DbCommand command, IEnumerable<SqlParameterValue> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.DbType = parameter.DbType;
            if (parameter.Size != 0)
            {
                dbParameter.Size = parameter.Size;
            }

            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }
}
