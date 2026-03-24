import type { Project } from '~/types'

/**
 * Returns the display ID for an issue, taking into account the project's IssueKey and
 * IssueNumberOffset settings as well as any external tracker ID.
 *
 * When an external ID is available (e.g. imported from GitHub or Jira) it is shown as
 * the primary identifier. The native IssuePit number is used as a fallback.
 *
 * GitHub has no project slugs, so GitHub issues are shown as bare numbers:
 *  - externalId=69, externalSource="github"       → "#69"
 *
 * For other trackers (e.g. Jira), the externalSource stores the project key/slug and is
 * used as a prefix:
 *  - externalId=42, externalSource="PROJ"         → "#PROJ-42"
 *
 * Native IssuePit issues:
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
    // GitHub has no project slugs — show bare number (e.g. "#69")
    if (externalSource === 'github') {
      return `#${externalId}`
    }
    // For other trackers (e.g. Jira), externalSource holds the project key/slug (e.g. "PROJ")
    return `#${externalSource}-${externalId}`
  }
  const offset = project?.issueNumberOffset ?? 0
  const displayed = number + offset
  const key = project?.issueKey
  return key ? `#${key}-${displayed}` : `#${displayed}`
}
