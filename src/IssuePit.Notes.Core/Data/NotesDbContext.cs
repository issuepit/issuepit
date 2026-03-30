using IssuePit.Notes.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Core.Data;

public class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<NoteWorkspace> NoteWorkspaces => Set<NoteWorkspace>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteLink> NoteLinks => Set<NoteLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Note → Workspace relationship
        modelBuilder.Entity<Note>()
            .HasOne(n => n.Workspace)
            .WithMany(w => w.Notes)
            .HasForeignKey(n => n.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoteLink: SourceNote → OutgoingLinks
        modelBuilder.Entity<NoteLink>()
            .HasOne(l => l.SourceNote)
            .WithMany(n => n.OutgoingLinks)
            .HasForeignKey(l => l.SourceNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoteLink: TargetNote → IncomingLinks (optional, for note-to-note links)
        modelBuilder.Entity<NoteLink>()
            .HasOne(l => l.TargetNote)
            .WithMany(n => n.IncomingLinks)
            .HasForeignKey(l => l.TargetNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for fast lookup of links by source note
        modelBuilder.Entity<NoteLink>()
            .HasIndex(l => l.SourceNoteId);

        // Index for fast lookup of backlinks (who links to this note?)
        modelBuilder.Entity<NoteLink>()
            .HasIndex(l => l.TargetNoteId);

        // Index for workspace-scoped queries
        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.TenantId, n.WorkspaceId });

        modelBuilder.Entity<NoteWorkspace>()
            .HasIndex(w => w.TenantId);
    }
}
