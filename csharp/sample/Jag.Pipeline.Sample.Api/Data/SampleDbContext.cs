using Jag.Pipeline.Sample.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jag.Pipeline.Sample.Api.Data;

/// <summary>
/// The sample's EF Core InMemory database context.
/// </summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => this.Set<Student>();

    public DbSet<AcademicProgram> Programs => this.Set<AcademicProgram>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>().HasIndex(s => s.IdentificationNumber);
        modelBuilder.Entity<AcademicProgram>().HasIndex(p => p.Code).IsUnique();
    }
}
