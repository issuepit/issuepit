import { defineStore } from 'pinia'
import type { Issue, IssuePriority, IssueType } from '~/types'
import { IssueStatus } from '~/types'

interface IssueFilters {
  status?: IssueStatus
  priority?: IssuePriority
  type?: IssueType
  assigneeId?: string
  labelId?: string
  search?: string
}

export const useIssuesStore = defineStore('issues', () => {
  const issues = ref<Issue[]>([])
  const currentIssue = ref<Issue | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const filters = ref<IssueFilters>({})

  const api = useApi()

  const filteredIssues = computed(() => {
    let result = issues.value
    if (filters.value.status) result = result.filter(i => i.status === filters.value.status)
    if (filters.value.priority) result = result.filter(i => i.priority === filters.value.priority)
    if (filters.value.type) result = result.filter(i => i.type === filters.value.type)
    if (filters.value.search) {
      const q = filters.value.search.toLowerCase()
      result = result.filter(i => i.title.toLowerCase().includes(q))
    }
    return result
  })

  const issuesByStatus = computed(() => {
    const grouped: Record<IssueStatus, Issue[]> = {
      [IssueStatus.Backlog]: [],
      [IssueStatus.Todo]: [],
      [IssueStatus.InProgress]: [],
      [IssueStatus.InReview]: [],
      [IssueStatus.Done]: [],
      [IssueStatus.Cancelled]: []
    }
    for (const issue of filteredIssues.value) {
      grouped[issue.status].push(issue)
    }
    return grouped
  })

  async function fetchIssues(projectId: string, params?: IssueFilters) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Issue[]>('/api/issues', { params: { projectId, ...params } })
      issues.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch issues'
    } finally {
      loading.value = false
    }
  }

  async function fetchIssue(projectId: string, issueId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Issue>(`/api/issues/${issueId}`)
      currentIssue.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch issue'
    } finally {
      loading.value = false
    }
  }

  async function createIssue(projectId: string, payload: Partial<Issue>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Issue>('/api/issues', { ...payload, projectId })
      issues.value.unshift(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create issue'
    } finally {
      loading.value = false
    }
  }

  async function updateIssue(projectId: string, issueId: string, payload: Partial<Issue>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Issue>(`/api/issues/${issueId}`, payload)
      const idx = issues.value.findIndex(i => i.id === issueId)
      if (idx !== -1) issues.value[idx] = data
      if (currentIssue.value?.id === issueId) currentIssue.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update issue'
    } finally {
      loading.value = false
    }
  }

  async function updateIssueStatus(projectId: string, issueId: string, status: IssueStatus) {
    return updateIssue(projectId, issueId, { status })
  }

  async function deleteIssue(projectId: string, issueId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/issues/${issueId}`)
      issues.value = issues.value.filter(i => i.id !== issueId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete issue'
    } finally {
      loading.value = false
    }
  }

  function setFilters(f: IssueFilters) {
    filters.value = f
  }

  function clearFilters() {
    filters.value = {}
  }

  return {
    issues,
    currentIssue,
    loading,
    error,
    filters,
    filteredIssues,
    issuesByStatus,
    fetchIssues,
    fetchIssue,
    createIssue,
    updateIssue,
    updateIssueStatus,
    deleteIssue,
    setFilters,
    clearFilters
  }
})
