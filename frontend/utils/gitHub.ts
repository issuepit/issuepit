/**
 * Returns the internal code-viewer URL for a specific commit SHA.
 * Links to /projects/{projectId}/code?sha={sha} so the code browser opens at that commit.
 */
export function buildCommitUrl(projectId: string, sha: string | undefined | null): string | null {
  if (!sha) return null
  return `/projects/${projectId}/code?sha=${sha}`
}

/**
 * Returns the internal code-viewer URL for a specific branch.
 * Links to /projects/{projectId}/code?tab=branches&branch={branch} so the code browser opens
 * on the Branches tab with that branch selected.
 */
export function buildBranchUrl(projectId: string, branch: string | undefined | null): string | null {
  if (!branch) return null
  return `/projects/${projectId}/code?tab=branches&branch=${encodeURIComponent(branch)}`
}
