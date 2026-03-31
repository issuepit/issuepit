import { defineStore } from 'pinia'
import type { Issue, IssuePriority, IssueType, IssueComment, IssueAttachment, IssueTask, IssueAssignee, Label, CodeReviewComment, IssueLink, IssueLinkType, IssueEvent, IssueRuns, IssueGitMapping } from '~/types'
import { IssueStatus } from '~/types'

const PRIORITY_ORDER: Record<string, number> = {
  urgent: 0, very_high: 1, high: 2, medium: 3, low: 4, no_priority: 5, unknown: 6
}

interface IssueFilters {
  status?: IssueStatus
  priority?: IssuePriority
  type?: IssueType
  assigneeId?: string
  labelId?: string
  milestoneId?: string
  search?: string
  sortBy?: 'lastActivity' | 'updatedAt' | 'createdAt' | 'number' | 'priority'
  sortDir?: 'asc' | 'desc'
}

export const useIssuesStore = defineStore('issues', () => {
  const issues = ref<Issue[]>([])
  const currentIssue = ref<Issue | null>(null)
  const currentComments = ref<IssueComment[]>([])
  const currentCodeReviewComments = ref<CodeReviewComment[]>([])
  const currentAttachments = ref<IssueAttachment[]>([])
  const currentTasks = ref<IssueTask[]>([])
  const currentLinks = ref<IssueLink[]>([])
  const currentHistory = ref<IssueEvent[]>([])
  const currentRuns = ref<IssueRuns | null>(null)
  const currentGitMappings = ref<IssueGitMapping[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const filters = ref<IssueFilters>({})

  const api = useApi()

  const filteredIssues = computed(() => {
    let result = issues.value
    if (filters.value.status) result = result.filter(i => i.status === filters.value.status)
    if (filters.value.priority) result = result.filter(i => i.priority === filters.value.priority)
    if (filters.value.type) result = result.filter(i => i.type === filters.value.type)
    if (filters.value.milestoneId) result = result.filter(i => i.milestoneId === filters.value.milestoneId)
    if (filters.value.search) {
      const q = filters.value.search.toLowerCase()
      result = result.filter(i => i.title.toLowerCase().includes(q))
    }
    const sortBy = filters.value.sortBy ?? 'lastActivity'
    const sortDir = filters.value.sortDir ?? 'desc'
    return [...result].sort((a, b) => {
      let cmp = 0
      if (sortBy === 'number') {
        cmp = a.number - b.number
      } else if (sortBy === 'createdAt') {
        cmp = a.createdAt.localeCompare(b.createdAt)
      } else if (sortBy === 'priority') {
        cmp = (PRIORITY_ORDER[a.priority] ?? 5) - (PRIORITY_ORDER[b.priority] ?? 5)
      } else if (sortBy === 'updatedAt') {
        cmp = a.updatedAt.localeCompare(b.updatedAt)
      } else {
        // lastActivity (default)
        cmp = a.lastActivityAt.localeCompare(b.lastActivityAt)
      }
      return sortDir === 'asc' ? cmp : -cmp
    })
  })

  const issuesByStatus = computed(() => {
    const grouped: Record<IssueStatus, Issue[]> = {
      [IssueStatus.Backlog]: [],
      [IssueStatus.Todo]: [],
      [IssueStatus.InProgress]: [],
      [IssueStatus.InReview]: [],
      [IssueStatus.ReadyToMerge]: [],
      [IssueStatus.Done]: [],
      [IssueStatus.Cancelled]: []
    }
    for (const issue of filteredIssues.value) {
      grouped[issue.status].push(issue)
    }
    for (const status of Object.keys(grouped) as unknown as IssueStatus[]) {
      grouped[status].sort((a, b) => a.kanbanRank - b.kanbanRank || a.createdAt.localeCompare(b.createdAt))
    }
    return grouped
  })

  async function fetchIssues(projectId?: string, params?: IssueFilters) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Issue[]>('/api/issues', { params: { ...(projectId ? { projectId } : {}), ...params } })
      issues.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch issues'
    } finally {
      loading.value = false
    }
  }

  async function fetchFeed(filter: 'my' | 'open' | 'unassigned' | 'waiting' = 'my') {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Issue[]>('/api/issues/feed', { params: { filter } })
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
      // Support numeric issue IDs (issue number) in addition to GUIDs
      const isNumeric = /^\d+$/.test(issueId)
      const url = isNumeric
        ? `/api/issues/by-project/${projectId}/${issueId}`
        : `/api/issues/${issueId}`
      const data = await api.get<Issue>(url)
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
      if (currentIssue.value?.id === issueId) currentIssue.value = { ...currentIssue.value, ...data }
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

  // --- Comments ---

  async function fetchComments(issueId: string) {
    try {
      const data = await api.get<IssueComment[]>(`/api/issues/${issueId}/comments`)
      currentComments.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch comments'
    }
  }

  async function addComment(issueId: string, body: string, userId?: string, branch?: string) {
    try {
      const data = await api.post<IssueComment>(`/api/issues/${issueId}/comments`, { body, userId, branch: branch || undefined })
      currentComments.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add comment'
    }
  }

  async function updateComment(issueId: string, commentId: string, body: string) {
    try {
      const data = await api.put<IssueComment>(`/api/issues/${issueId}/comments/${commentId}`, { body })
      const idx = currentComments.value.findIndex(c => c.id === commentId)
      if (idx !== -1) currentComments.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update comment'
    }
  }

  async function deleteComment(issueId: string, commentId: string) {
    try {
      await api.del(`/api/issues/${issueId}/comments/${commentId}`)
      currentComments.value = currentComments.value.filter(c => c.id !== commentId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete comment'
    }
  }

  // --- Code Review Comments ---

  async function fetchCodeReviewComments(issueId: string) {
    try {
      const data = await api.get<CodeReviewComment[]>(`/api/issues/${issueId}/code-review-comments`)
      currentCodeReviewComments.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch code review comments'
    }
  }

  async function addCodeReviewComment(issueId: string, comment: Omit<CodeReviewComment, 'id' | 'issueId' | 'createdAt'>) {
    try {
      const data = await api.post<CodeReviewComment>(`/api/issues/${issueId}/code-review-comments`, comment)
      currentCodeReviewComments.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add code review comment'
    }
  }

  async function addCodeReviewCommentsBatch(issueId: string, comments: Omit<CodeReviewComment, 'id' | 'issueId' | 'createdAt'>[]) {
    try {
      const data = await api.post<CodeReviewComment[]>(`/api/issues/${issueId}/code-review-comments/batch`, comments)
      currentCodeReviewComments.value.push(...data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add code review comments'
    }
  }

  // --- Tasks ---

  async function fetchTasks(issueId: string) {
    try {
      const data = await api.get<IssueTask[]>(`/api/issues/${issueId}/tasks`)
      currentTasks.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch tasks'
    }
  }

  async function createTask(issueId: string, title: string) {
    try {
      const data = await api.post<IssueTask>(`/api/issues/${issueId}/tasks`, { title })
      currentTasks.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create task'
    }
  }

  async function toggleTask(issueId: string, taskId: string, completed: boolean) {
    try {
      const task = currentTasks.value.find(t => t.id === taskId)
      if (!task) return
      const data = await api.put<IssueTask>(`/api/issues/${issueId}/tasks/${taskId}`, { title: task.title, completed })
      const idx = currentTasks.value.findIndex(t => t.id === taskId)
      if (idx !== -1) currentTasks.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update task'
    }
  }

  async function deleteTask(issueId: string, taskId: string) {
    try {
      await api.del(`/api/issues/${issueId}/tasks/${taskId}`)
      currentTasks.value = currentTasks.value.filter(t => t.id !== taskId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete task'
    }
  }

  // --- Assignees ---

  async function addAssignee(issueId: string, payload: { userId?: string; agentId?: string; branch?: string }) {
    try {
      const data = await api.post<IssueAssignee>(`/api/issues/${issueId}/assignees`, payload)
      if (currentIssue.value?.id === issueId) {
        currentIssue.value = { ...currentIssue.value, assignees: [...(currentIssue.value.assignees ?? []), data] }
      }
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add assignee'
    }
  }

  async function removeAssignee(issueId: string, assigneeId: string) {
    try {
      await api.del(`/api/issues/${issueId}/assignees/${assigneeId}`)
      if (currentIssue.value?.id === issueId) {
        currentIssue.value = { ...currentIssue.value, assignees: currentIssue.value.assignees.filter(a => a.id !== assigneeId) }
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove assignee'
    }
  }

  // --- Labels on Issue ---

  async function addIssueLabel(issueId: string, labelId: string) {
    try {
      const data = await api.post<Label>(`/api/issues/${issueId}/labels`, { labelId })
      if (currentIssue.value?.id === issueId) {
        currentIssue.value = { ...currentIssue.value, labels: [...(currentIssue.value.labels ?? []), data] }
      }
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add label'
    }
  }

  async function removeIssueLabel(issueId: string, labelId: string) {
    try {
      await api.del(`/api/issues/${issueId}/labels/${labelId}`)
      if (currentIssue.value?.id === issueId) {
        currentIssue.value = { ...currentIssue.value, labels: currentIssue.value.labels.filter(l => l.id !== labelId) }
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove label'
    }
  }

  // --- Issue Links ---

  async function fetchLinks(issueId: string) {
    try {
      const data = await api.get<IssueLink[]>(`/api/issues/${issueId}/links`)
      currentLinks.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch links'
    }
  }

  async function addLink(issueId: string, targetIssueId: string, linkType: IssueLinkType) {
    try {
      const data = await api.post<IssueLink>(`/api/issues/${issueId}/links`, { targetIssueId, linkType })
      currentLinks.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add link'
    }
  }

  async function removeLink(issueId: string, linkId: string) {
    try {
      await api.del(`/api/issues/${issueId}/links/${linkId}`)
      currentLinks.value = currentLinks.value.filter(l => l.id !== linkId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove link'
    }
  }

  // --- History ---

  async function fetchHistory(issueId: string) {
    try {
      const data = await api.get<IssueEvent[]>(`/api/issues/${issueId}/history`)
      currentHistory.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch history'
    }
  }

  // --- Attachments ---

  async function fetchAttachments(issueId: string) {
    try {
      const data = await api.get<IssueAttachment[]>(`/api/issues/${issueId}/attachments`)
      currentAttachments.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch attachments'
    }
  }

  async function addAttachment(issueId: string, file: File, isVoiceFile = false, isPublic = true) {
    const config = useRuntimeConfig()
    const baseURL = config.public.apiBase as string
    try {
      const body = new FormData()
      body.append('file', file)
      const params = new URLSearchParams({ isVoiceFile: String(isVoiceFile), isPublic: String(isPublic) })
      const data = await $fetch<IssueAttachment>(`/api/issues/${issueId}/attachments?${params}`, {
        baseURL,
        method: 'POST',
        body,
        credentials: 'include',
      })
      currentAttachments.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to upload attachment'
    }
  }

  async function deleteAttachment(issueId: string, attachmentId: string) {
    try {
      await api.del(`/api/issues/${issueId}/attachments/${attachmentId}`)
      currentAttachments.value = currentAttachments.value.filter(a => a.id !== attachmentId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete attachment'
    }
  }

  async function updateAttachment(issueId: string, attachmentId: string, patch: { isPublic?: boolean }) {
    try {
      const data = await api.patch<IssueAttachment>(`/api/issues/${issueId}/attachments/${attachmentId}`, patch)
      const idx = currentAttachments.value.findIndex(a => a.id === attachmentId)
      if (idx >= 0) currentAttachments.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update attachment visibility'
    }
  }

  async function retranscribeAttachment(issueId: string, attachmentId: string) {
    try {
      const data = await api.post<IssueComment>(`/api/issues/${issueId}/attachments/${attachmentId}/retranscribe`, {})
      currentComments.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to retranscribe attachment'
    }
  }

  async function fetchIssueRuns(issueId: string) {
    try {
      currentRuns.value = await api.get<IssueRuns>(`/api/issues/${issueId}/runs`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch issue runs'
    }
  }

  async function fetchGitMappings(issueId: string) {
    try {
      currentGitMappings.value = await api.get<IssueGitMapping[]>(`/api/issues/${issueId}/git-mappings`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch git mappings'
    }
  }

  // --- Milestone on Issue ---

  async function clearIssueMilestone(projectId: string, issueId: string) {
    try {
      const data = await api.put<Issue>(`/api/issues/${issueId}`, { clearMilestoneId: true })
      const idx = issues.value.findIndex(i => i.id === issueId)
      if (idx !== -1) issues.value[idx] = data
      if (currentIssue.value?.id === issueId) currentIssue.value = { ...currentIssue.value, ...data }
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to clear milestone'
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
    currentComments,
    currentCodeReviewComments,
    currentAttachments,
    currentTasks,
    currentLinks,
    currentHistory,
    currentRuns,
    currentGitMappings,
    loading,
    error,
    filters,
    filteredIssues,
    issuesByStatus,
    fetchIssues,
    fetchFeed,
    fetchIssue,
    createIssue,
    updateIssue,
    updateIssueStatus,
    deleteIssue,
    fetchComments,
    addComment,
    updateComment,
    deleteComment,
    fetchCodeReviewComments,
    addCodeReviewComment,
    addCodeReviewCommentsBatch,
    fetchAttachments,
    addAttachment,
    deleteAttachment,
    updateAttachment,
    retranscribeAttachment,
    fetchTasks,
    createTask,
    toggleTask,
    deleteTask,
    addAssignee,
    removeAssignee,
    addIssueLabel,
    removeIssueLabel,
    fetchLinks,
    addLink,
    removeLink,
    fetchHistory,
    fetchIssueRuns,
    fetchGitMappings,
    clearIssueMilestone,
    setFilters,
    clearFilters
  }
})
