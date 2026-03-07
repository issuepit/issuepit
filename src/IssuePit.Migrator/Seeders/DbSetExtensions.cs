using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Migrator.Seeders;

/// <summary>Extension methods that make EF Core <see cref="DbSet{T}"/> seeders idempotent.</summary>
public static class DbSetExtensions
{
    /// <summary>
    /// Adds <paramref name="entity"/> to the set when no row matching <paramref name="predicate"/> exists.
    /// Returns the existing or newly queued entity together with a flag indicating whether the entity is new.
    /// Call <c>SaveChangesAsync</c> afterwards to persist newly added entities.
    /// </summary>
    public static async Task<(T Entity, bool IsNew)> AddIfNotExistsAsync<T>(
        this DbSet<T> dbSet,
        Expression<Func<T, bool>> predicate,
        T entity) where T : class
    {
        var existing = await dbSet.FirstOrDefaultAsync(predicate);
        if (existing is not null)
            return (existing, false);

        dbSet.Add(entity);
        return (entity, true);
    }
}
