using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Data;
internal static class DatabaseConflicts
{
    public static bool IsUniqueViolation(DbUpdateException error) => error.InnerException is
        Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 } or
        Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}
