import type { Project } from '~/types'

/**
 * Returns the display prefix for an external source identifier.
 *
 * Examples:
 *  - "github" → "GH"
 *  - "jira"   → "J"
 */
function externalSourcePrefix(source: string): string {
  if (source === 'github') return 'GH'
  if (source === 'jira') return 'J'
  return source.toUpperCase().slice(0, 5)
}

/**
 * Returns the display ID for an issue, taking into account the project's IssueKey and
 * IssueNumberOffset settings as well as any external tracker ID.
 *
 * When an external ID is available (e.g. imported from GitHub or Jira) it is shown as
 * the primary identifier. The native IssuePit number is used as a fallback.
 *
 * Examples:
 *  - externalId=69, externalSource="github"       → "#GH-69"
 *  - externalId=42, externalSource="jira"         → "#J-42"
 *  - issueKey="IP", offset=0, number=5            → "#IP-5"
 *  - issueKey="IP", offset=10000, number=5        → "#IP-10005"
 *  - issueKey=null, offset=0, number=5            → "#5"
 *  - issueKey=null, offset=10000, number=5        → "#10005"
 */
export function formatIssueId(
  number: number,
  project: Pick<Project, 'issueKey' | 'issueNumberOffset'> | null | undefined,
  externalId?: number | null,
  externalSource?: string | null,
): string {
  if (externalId != null && externalSource) {
    return `#${externalSourcePrefix(externalSource)}-${externalId}`
  }
  const offset = project?.issueNumberOffset ?? 0
  const displayed = number + offset
  const key = project?.issueKey
  return key ? `#${key}-${displayed}` : `#${displayed}`
}
