namespace IssuePit.Core.Enums;

public enum GitOriginMode
{
    /// <summary>Fetch/read only – never pushed to by agents or releases.</summary>
    ReadOnly = 0,

    /// <summary>Working remote – agents push feature branches and open PRs here.</summary>
    Working = 1,

    /// <summary>Release remote – only the main/default branch is pushed here after an agent PR is merged.</summary>
    Release = 2,
}
