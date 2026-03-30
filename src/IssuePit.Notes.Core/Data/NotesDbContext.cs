using IssuePit.Notes.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Core.Data;

public class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Notebook> Notebooks => Set<Notebook>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteLink> NoteLinks => Set<NoteLink>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();
    public DbSet<NoteTagMapping> NoteTagMappings => Set<NoteTagMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // NoteTagMapping composite key
        modelBuilder.Entity<NoteTagMapping>()
            .HasKey(x => new { x.NoteId, x.TagId });

        modelBuilder.Entity<NoteTagMapping>()
            .HasOne(x => x.Note)
            .WithMany(n => n.TagMappings)
            .HasForeignKey(x => x.NoteId);

        modelBuilder.Entity<NoteTagMapping>()
            .HasOne(x => x.Tag)
            .WithMany(t => t.NoteMappings)
            .HasForeignKey(x => x.TagId);

        // NoteLink relationships
        modelBuilder.Entity<NoteLink>()
            .HasOne(l => l.SourceNote)
            .WithMany(n => n.OutgoingLinks)
            .HasForeignKey(l => l.SourceNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NoteLink>()
            .HasOne(l => l.TargetNote)
            .WithMany(n => n.IncomingLinks)
            .HasForeignKey(l => l.TargetNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique slug per notebook for wiki-style linking
        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.NotebookId, n.Slug })
            .IsUnique();
    }
}
