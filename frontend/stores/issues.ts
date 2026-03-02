import { defineStore } from 'pinia'
import type { Issue, IssuePriority, IssueType, IssueComment, IssueTask, IssueAssignee, Label, CodeReviewComment } from '~/types'
import { IssueStatus } from '~/types'

interface IssueFilters {
  status?: IssueStatus
  priority?: IssuePriority
  type?: IssueType
  assigneeId?: string
  labelId?: string
  milestoneId?: string
  search?: string
}

export const useIssuesStore = defineStore('issues', () => {
  const issues = ref<Issue[]>([])
  const currentIssue = ref<Issue | null>(null)
  const currentComments = ref<IssueComment[]>([])
  const currentCodeReviewComments = ref<CodeReviewComment[]>([])
  const currentTasks = ref<IssueTask[]>([])
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

  async function addComment(issueId: string, body: string, userId?: string) {
    try {
      const data = await api.post<IssueComment>(`/api/issues/${issueId}/comments`, { body, userId })
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

  async function addAssignee(issueId: string, payload: { userId?: string; agentId?: string }) {
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
    currentTasks,
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
    fetchTasks,
    createTask,
    toggleTask,
    deleteTask,
    addAssignee,
    removeAssignee,
    addIssueLabel,
    removeIssueLabel,
    clearIssueMilestone,
    setFilters,
    clearFilters
  }
})
