using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("issue_attachments")]
public class IssueAttachment
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    /// <summary>Optional: attachment is associated with a specific comment.</summary>
    public Guid? CommentId { get; set; }

    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileUrl { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>True if this attachment is a voice/audio recording.</summary>
    public bool IsVoiceFile { get; set; }

    /// <summary>When false, only the uploader can access this attachment. Defaults to false for voice files.</summary>
    public bool IsPublic { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
