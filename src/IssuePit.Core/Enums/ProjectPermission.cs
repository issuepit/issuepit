namespace IssuePit.Core.Enums;

[Flags]
public enum ProjectPermission
{
    None = 0,
    Read = 1,
    Write = 2,
    CommentPrs = 4,
    MoveKanban = 8,
    Milestones = 16,
    Labels = 32,
    ProjectAdmin = 64
}
