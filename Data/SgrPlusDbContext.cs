using Microsoft.EntityFrameworkCore;

namespace RiesgoWebEmpresarial.Data;

/// <summary>
/// Conexión EF Core a la base de datos de la web SgrPlus.
///
/// Todavía sin entidades mapeadas. Dos formas de completarlo:
///
///  1) Scaffold desde la base real (requiere la connection string cargada):
///     dotnet ef dbcontext scaffold "Name=ConnectionStrings:SgrPlusWeb" \
///       Microsoft.EntityFrameworkCore.SqlServer -o Data/SgrPlus --context SgrPlusDbContext --force
///
///  2) A mano: agregar clases POCO y sus DbSet acá abajo.
///
/// Ver README (sección "Base de datos").
/// </summary>
public class SgrPlusDbContext : DbContext
{
    public SgrPlusDbContext(DbContextOptions<SgrPlusDbContext> options) : base(options)
    {
    }

    // Ejemplo de cómo quedaría una entidad una vez mapeada la base:
    // public DbSet<Empresa> Empresas => Set<Empresa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configuración fluida / mapeos van acá cuando existan entidades.
    }
}
