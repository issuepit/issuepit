using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IssuePit.Core.Data;

public class IssuePitDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IssuePitDbContext>
{
    public IssuePitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__issuepit-db")
            ?? "Host=localhost;Database=issuepit;Username=issuepit;Password=issuepit";
        var optionsBuilder = new DbContextOptionsBuilder<IssuePitDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new IssuePitDbContext(optionsBuilder.Options);
    }
}
