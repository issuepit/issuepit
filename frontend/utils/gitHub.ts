/**
 * Returns the internal code-viewer URL for a specific commit SHA.
 * Links to /projects/{projectId}/code?sha={sha} so the code browser opens at that commit.
 */
export function buildCommitUrl(projectId: string, sha: string | undefined | null): string | null {
  if (!sha) return null
  return `/projects/${projectId}/code?sha=${sha}`
}
