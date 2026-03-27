/**
 * Utilities for mapping act log job IDs to workflow graph node IDs.
 *
 * act emits job identifiers using display names (from the `name:` YAML field),
 * not YAML keys. For reusable-workflow calls it prefixes with the caller workflow
 * or job name (e.g. "Backend/Backend CI/Build"). For matrix jobs it appends "-N"
 * or "(value)" (e.g. "Build (ubuntu-latest)").
 *
 * Our graph node IDs use the file-stem / yaml-key format (e.g. "backend/build").
 * This module builds fuzzy lookup indexes and resolves log IDs to graph node IDs.
 */

import type { WorkflowJobNode } from '~/types'

export interface GraphJobIndexes {
  byId: Map<string, string>   // lowercase_id → graph_id
  byName: Map<string, string> // lowercase_name/alias → graph_id
  /** workflowFileStem or callerFileStem → graph_id, for jobs whose last name segment is a pure ${{...}} template. */
  byTemplatePrefix: Map<string, string>
}

/**
 * Builds lookup indexes from a list of graph job nodes.
 *
 * Indexed aliases (in addition to exact ID and name):
 *  - Name with spaces around `/` removed ("Backend / Build" → "backend/build")
 *  - workflowFileStem/lastJobNamePart ("backend/build" for backend.yml's Build job)
 *  - Unambiguous last-name-segment ("build" only when exactly ONE compound-name node has it)
 */
export function buildGraphJobIndexes(graphJobs: WorkflowJobNode[]): GraphJobIndexes {
  const byId = new Map<string, string>()
  const byName = new Map<string, string>()
  const byTemplatePrefix = new Map<string, string>()

  // Collect last-segment candidates to detect ambiguous names (same last segment across multiple nodes).
  const lastSegCandidates = new Map<string, string[]>()

  for (const j of graphJobs) {
    byId.set(j.id.toLowerCase(), j.id)
    const nameKey = j.name.trim().toLowerCase()
    byName.set(nameKey, j.id)
    // Also index name without spaces around slashes (act may omit spaces: "Backend/Build" vs "Backend / Build")
    const nameNoSpaces = nameKey.replace(/\s*\/\s*/g, '/')
    if (nameNoSpaces !== nameKey && !byName.has(nameNoSpaces)) byName.set(nameNoSpaces, j.id)
    // Compute the last segment of the job display name once (reused for file-stem indexing below).
    const lastJobNamePart = (j.name.split(/\s*\/\s*/).pop() || j.name).trim().toLowerCase()
    // Also compute a stripped version of lastJobNamePart for matrix jobs that use template expressions
    // like "${{ matrix.version }}" in their name. Stripping the parenthetical makes "ci/build" match
    // log IDs like "CI/Build (version 1)-1" (where the resolved value replaces the template).
    const lastJobNamePartBase = lastJobNamePart.replace(/\s*\([^)]*\)\s*$/, '').trim()
    const hasTemplateSuffix = lastJobNamePartBase !== lastJobNamePart && lastJobNamePart.includes('${{')
    // Detect when the ENTIRE last name segment is a template expression (e.g. "${{ matrix.suite.name }}").
    // act resolves this at runtime so the log ID will contain the resolved value, not the template text.
    // We can't index by the last segment in this case — only by the workflow/caller file stem.
    const isPureTemplateName = /^\$\{\{[^}]*\}\}$/.test(lastJobNamePart.trim())

    // Index by workflowFileStem/lastJobNamePart so act's "stem/callee" format can resolve correctly
    if (j.workflowFile) {
      const stem = j.workflowFile.replace(/\.(yml|yaml)$/i, '').toLowerCase()
      const workflowKey = `${stem}/${lastJobNamePart}`
      if (!byName.has(workflowKey)) byName.set(workflowKey, j.id)
      // Add stripped template key so "ci/build" matches "CI/Build (version 1)-1" style log IDs
      if (hasTemplateSuffix) {
        const workflowKeyBase = `${stem}/${lastJobNamePartBase}`
        if (!byName.has(workflowKeyBase)) byName.set(workflowKeyBase, j.id)
      }
      // For pure-template last segments, index by stem alone so the resolver can fall back to it
      // when the last log-ID segment is the resolved value (e.g. "E2E Tests-4" for "${{ matrix.suite.name }}").
      if (isPureTemplateName && !byTemplatePrefix.has(stem)) byTemplatePrefix.set(stem, j.id)
    }
    // Index by callerWorkflowFileStem/lastJobNamePart so act's "<callerWorkflow>/.../job" format resolves.
    // e.g. for "CI/Backend CI/Build" → segments[0]="ci" matches ci.yml stem → "ci/build" → backend/build
    if (j.callerWorkflowFile) {
      const callerStem = j.callerWorkflowFile.replace(/\.(yml|yaml)$/i, '').toLowerCase()
      const callerKey = `${callerStem}/${lastJobNamePart}`
      if (!byName.has(callerKey)) byName.set(callerKey, j.id)
      // Add stripped template key for matrix template names (e.g. "ci/build" for "Build (${{ matrix.version }})")
      if (hasTemplateSuffix) {
        const callerKeyBase = `${callerStem}/${lastJobNamePartBase}`
        if (!byName.has(callerKeyBase)) byName.set(callerKeyBase, j.id)
      }
      // For pure-template last segments, also index by callerStem for caller-prefixed log IDs
      if (isPureTemplateName && !byTemplatePrefix.has(callerStem)) byTemplatePrefix.set(callerStem, j.id)
    }
    // Collect last-segment candidates (used below to prevent ambiguous single-segment matching)
    const nameParts = j.name.split(/\s*\/\s*/)
    if (nameParts.length > 1) {
      const lastPart = nameParts[nameParts.length - 1].trim().toLowerCase()
      if (!lastSegCandidates.has(lastPart)) lastSegCandidates.set(lastPart, [])
      lastSegCandidates.get(lastPart)!.push(j.id)
    }
  }

  // Add last-segment entries only when there is exactly ONE candidate.
  // Multiple candidates (e.g. "build" in both "backend/build" and "pages/build") are ambiguous:
  // using last-segment for them would map logs from one workflow to the wrong graph node.
  for (const [lastSeg, candidates] of lastSegCandidates) {
    if (candidates.length === 1 && !byName.has(lastSeg))
      byName.set(lastSeg, candidates[0])
  }

  return { byId, byName, byTemplatePrefix }
}

/**
 * Strips trailing matrix suffixes from a log job ID segment.
 * Handles both "-N" (e.g. "Build-2") and "(value)" (e.g. "Build (ubuntu-latest)") forms.
 */
function stripMatrixSuffix(s: string): string {
  return s.replace(/-\d+$/, '').replace(/\s*\([^)]*\)\s*$/, '').trim()
}

/**
 * Maps an act log job ID to the matching graph node ID.
 *
 * act uses display names (e.g. "Build & Push") rather than YAML keys ("build").
 * For matrix jobs it appends "-N" (e.g. "Build-2") or "(value)" (e.g. "Build (ubuntu-latest)").
 * Reusable workflow calls prefix with the caller name (e.g. "Backend/Backend CI/Build").
 *
 * Matching order:
 *  1. Exact ID match (case-insensitive).
 *  2. Strip trailing matrix index, retry exact ID.
 *  3. Display-name match (full or stripped).
 *  4. Compound "/" path — iterate each left-side segment paired with the last segment:
 *       e.g. "Backend/Backend CI/Build" → try "backend ci/build", then "backend/build".
 *       This lets "backend" (first segment, == workflow file stem) find "backend/build".
 *  5. Bare last segment (only unambiguous or direct-name entries).
 *  6. No match → return original log ID (shown as standalone box).
 */
export function resolveLogJobId(logId: string, indexes: GraphJobIndexes): string {
  const { byId, byName, byTemplatePrefix } = indexes
  // Normalise backslashes (Windows paths emitted by act on Windows hosts) so matching works.
  const norm = logId.trim().toLowerCase().replace(/\\/g, '/')

  // 1. Exact ID match
  if (byId.has(norm)) return byId.get(norm)!

  // 2. Strip trailing matrix index "-N" or "(value)" and retry
  const stripped = stripMatrixSuffix(norm)
  if (stripped !== norm && byId.has(stripped)) return byId.get(stripped)!

  // 3. Display-name match (full then stripped)
  if (byName.has(norm)) return byName.get(norm)!
  if (stripped !== norm && byName.has(stripped)) return byName.get(stripped)!

  // 4. Compound "/" path — iterate each left-side segment paired with the last segment.
  //    For "A/B/C": try "b/c", "a/c" (right-to-left through left segments).
  //    This matches "backend" (stem) + "build" (job) even when act inserts "Backend CI" in between.
  const segments = norm.split('/')
  if (segments.length > 1) {
    const lastSeg = stripMatrixSuffix(segments[segments.length - 1])
    for (let i = segments.length - 2; i >= 0; i--) {
      const qualified = `${segments[i]}/${lastSeg}`
      if (byName.has(qualified)) return byName.get(qualified)!
      if (byId.has(qualified)) return byId.get(qualified)!
    }
    // Also try bare last segment (covers unambiguous direct-name matches like pages/build → "build")
    if (byName.has(lastSeg)) return byName.get(lastSeg)!
    if (byId.has(lastSeg)) return byId.get(lastSeg)!
    // Last resort: match prefix segments against jobs whose last name part is a pure ${{...}} template.
    // act resolves the template at runtime so the last log-ID segment is the resolved value (e.g. "E2E Tests-4").
    // The job can only be identified by its workflow/caller file stem, which appears as an earlier segment.
    for (let i = segments.length - 2; i >= 0; i--) {
      if (byTemplatePrefix.has(segments[i])) return byTemplatePrefix.get(segments[i])!
    }
  }

  return logId // No match — use as-is
}

/**
 * Extracts a short discriminator label from a matrix-instance act job ID.
 *
 * act prefixes reusable-workflow job IDs with the caller workflow/job name, so
 * a matrix job "Build" in backend.yml gets IDs like:
 *   "Deploy GitHub Pages/Build"  → discriminator: "Deploy GitHub Pages"
 *   "Backend/Backend CI/Build-2" → discriminator: "Backend CI-2" (last prefix segment)
 *
 * For simple matrix jobs ("Build-3", "Build (ubuntu-latest)") the discriminator
 * is the numeric index or the parenthetical value.
 */
export function matrixLabel(rawId: string, jobDisplayName: string): string {
  const leafName = (jobDisplayName.split(/\s*\/\s*/).pop() || jobDisplayName).trim()
  const escapedLeaf = leafName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

  // Try to strip the job's leaf name (+ optional matrix suffix) from the END of the rawId.
  // e.g. "Deploy GitHub Pages/Build" → prefix="Deploy GitHub Pages", matrixSuffix=""
  //      "Backend/Backend CI/Build-2" → prefix="Backend/Backend CI", matrixSuffix="-2"
  const leafPattern = new RegExp(`/${escapedLeaf}(-\\d+)?(\\s*\\([^)]*\\))?\\s*$`, 'i')
  const prefixMatch = rawId.match(leafPattern)
  if (prefixMatch) {
    const prefix = rawId.slice(0, rawId.length - prefixMatch[0].length)
    if (prefix) {
      // Show just the last segment of the prefix (e.g. "Backend CI" from "Backend/Backend CI")
      const prefixLastSeg = prefix.split('/').pop()!.trim()
      const matrixSuffix = prefixMatch[1] ?? ''
      return matrixSuffix ? `${prefixLastSeg}${matrixSuffix}` : prefixLastSeg
    }
    // Prefix is empty — rawId was just "<leafName><suffix>".
    const matrixSuffix = (prefixMatch[1] ?? '').concat(prefixMatch[2] ?? '').trim()
    if (matrixSuffix) return matrixSuffix
  }

  // Fallback: traditional last-segment approach — strip leaf name, return remainder.
  const slashIdx = rawId.lastIndexOf('/')
  const seg = slashIdx !== -1 ? rawId.slice(slashIdx + 1) : rawId
  const stripped = seg.replace(new RegExp(`^${escapedLeaf}[-\\s(]*`, 'i'), '').replace(/\)\s*$/, '').trim()
  return stripped || seg
}
