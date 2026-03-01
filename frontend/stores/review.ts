import { defineStore } from 'pinia'
import type { DiffResult, DiffLine, IssueComment } from '~/types'

export const useReviewStore = defineStore('review', () => {
  const diff = ref<DiffResult | null>(null)
  const comments = ref<IssueComment[]>([])
  const loading = ref(false)
  const diffLoading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchDiff(issueId: string) {
    diffLoading.value = true
    error.value = null
    try {
      const data = await api.get<DiffResult>(`/api/issues/${issueId}/review/diff`)
      diff.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch diff'
      diff.value = null
    } finally {
      diffLoading.value = false
    }
  }

  async function fetchComments(issueId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<IssueComment[]>(`/api/issues/${issueId}/review/comments`)
      comments.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch comments'
    } finally {
      loading.value = false
    }
  }

  async function addComment(issueId: string, payload: Partial<IssueComment>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<IssueComment>(`/api/issues/${issueId}/review/comments`, payload)
      comments.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add comment'
    } finally {
      loading.value = false
    }
  }

  async function deleteComment(issueId: string, commentId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/issues/${issueId}/review/comments/${commentId}`)
      comments.value = comments.value.filter(c => c.id !== commentId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete comment'
    } finally {
      loading.value = false
    }
  }

  function reset() {
    diff.value = null
    comments.value = []
    error.value = null
  }

  return {
    diff,
    comments,
    loading,
    diffLoading,
    error,
    fetchDiff,
    fetchComments,
    addComment,
    deleteComment,
    reset
  }
})

/**
 * Parses a unified diff patch string into an array of DiffLine objects.
 * Each line is classified as 'hunk', 'add', 'del', or 'context'.
 */
export function parsePatch(patch: string | undefined): DiffLine[] {
  if (!patch) return []

  const lines: DiffLine[] = []
  let oldLine = 0
  let newLine = 0

  for (const raw of patch.split('\n')) {
    if (raw.startsWith('@@')) {
      // Hunk header: @@ -l,s +l,s @@
      const match = raw.match(/@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/)
      if (match) {
        oldLine = parseInt(match[1], 10)
        newLine = parseInt(match[2], 10)
      }
      lines.push({ content: raw, type: 'hunk', lineNumber: null, oldLineNumber: null, newLineNumber: null })
    } else if (raw.startsWith('+')) {
      lines.push({ content: raw.slice(1), type: 'add', lineNumber: newLine, oldLineNumber: null, newLineNumber: newLine })
      newLine++
    } else if (raw.startsWith('-')) {
      lines.push({ content: raw.slice(1), type: 'del', lineNumber: oldLine, oldLineNumber: oldLine, newLineNumber: null })
      oldLine++
    } else if (raw.startsWith('\\')) {
      // "\ No newline at end of file" - skip
    } else {
      lines.push({ content: raw.startsWith(' ') ? raw.slice(1) : raw, type: 'context', lineNumber: newLine, oldLineNumber: oldLine, newLineNumber: newLine })
      oldLine++
      newLine++
    }
  }

  return lines
}
