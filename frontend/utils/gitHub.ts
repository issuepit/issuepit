/**
 * Builds an external GitHub commit URL from a repository identifier and a commit SHA.
 * @param gitHubRepo - The project's GitHub repo (e.g. "owner/repo" or full URL)
 * @param sha - The commit SHA
 * @returns The full GitHub commit URL, or null when the repo is not configured
 */
export function buildCommitUrl(gitHubRepo: string | undefined | null, sha: string | undefined | null): string | null {
  if (!gitHubRepo || !sha) return null
  const normalized = gitHubRepo.replace(/^https?:\/\/github\.com\//, '').replace(/\.git$/, '')
  return `https://github.com/${normalized}/commit/${sha}`
}
