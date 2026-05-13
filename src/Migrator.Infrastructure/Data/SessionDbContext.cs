using Microsoft.EntityFrameworkCore;

namespace Migrator.Infrastructure.Data;

/// <summary>
/// Contexto EF sin entidades: solo sirve para abrir la conexión SQL Server y participar en el mismo stack que el resto de EF.
/// El motor de datos dinámico usa <see cref="Database"/> → <see cref="Microsoft.Data.SqlClient.SqlConnection"/>.
/// </summary>
public sealed class SessionDbContext : DbContext
{
    public SessionDbContext(DbContextOptions<SessionDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sin modelo: migración dirigida por JSON.
    }
}

public static class SessionDbContextFactory
{
    public static DbContextOptions<SessionDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<SessionDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(0))
            .Options;
}
