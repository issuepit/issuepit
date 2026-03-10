import type { Project } from '~/types'

/**
 * Returns the display ID for an issue number, taking into account the project's
 * IssueKey and IssueNumberOffset settings.
 *
 * Examples:
 *  - issueKey="IP", offset=0, number=5  → "IP-5"
 *  - issueKey="IP", offset=10000, number=5  → "IP-10005"
 *  - issueKey=null, offset=0, number=5  → "#5"
 *  - issueKey=null, offset=10000, number=5  → "#10005"
 */
export function formatIssueId(number: number, project: Pick<Project, 'issueKey' | 'issueNumberOffset'> | null | undefined): string {
  const offset = project?.issueNumberOffset ?? 0
  const displayed = number + offset
  const key = project?.issueKey
  return key ? `${key}-${displayed}` : `#${displayed}`
}
