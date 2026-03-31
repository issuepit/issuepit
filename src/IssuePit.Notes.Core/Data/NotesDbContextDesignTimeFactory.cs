using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IssuePit.Notes.Core.Data;

/// <summary>
/// Design-time factory for EF Core tooling (migrations, scaffolding).
/// </summary>
public class NotesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    public NotesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotesDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=issuepit_notes;Username=postgres;Password=postgres");
        return new NotesDbContext(optionsBuilder.Options);
    }
}
