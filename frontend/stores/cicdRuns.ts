import { defineStore } from 'pinia'
import type { CiCdRun, CiCdRunLog, AgentSession, AgentSessionDetail, DashboardAgentSession } from '~/types'

export const useCiCdRunsStore = defineStore('cicdRuns', () => {
  const runs = ref<CiCdRun[]>([])
  const agentSessions = ref<AgentSession[]>([])
  const dashboardSessions = ref<DashboardAgentSession[]>([])
  const currentRun = ref<CiCdRun | null>(null)
  const currentRunLogs = ref<CiCdRunLog[]>([])
  const currentSession = ref<AgentSessionDetail | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchRuns(projectId?: string) {
    loading.value = true
    error.value = null
    try {
      const url = projectId ? `/api/cicd-runs?projectId=${projectId}` : '/api/cicd-runs'
      runs.value = await api.get<CiCdRun[]>(url)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CI/CD runs'
    } finally {
      loading.value = false
    }
  }

  async function fetchRun(runId: string) {
    loading.value = true
    error.value = null
    try {
      currentRun.value = await api.get<CiCdRun>(`/api/cicd-runs/${runId}`)
      currentRunLogs.value = await api.get<CiCdRunLog[]>(`/api/cicd-runs/${runId}/logs`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CI/CD run'
    } finally {
      loading.value = false
    }
  }

  async function fetchAgentSessions(projectId: string) {
    loading.value = true
    error.value = null
    try {
      agentSessions.value = await api.get<AgentSession[]>(`/api/projects/${projectId}/agent-sessions`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent sessions'
    } finally {
      loading.value = false
    }
  }

  async function fetchAgentSession(sessionId: string) {
    loading.value = true
    error.value = null
    try {
      currentSession.value = await api.get<AgentSessionDetail>(`/api/agent-sessions/${sessionId}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent session'
    } finally {
      loading.value = false
    }
  }

  async function retryRun(runId: string, options?: { keepContainerOnFailure?: boolean; forceRetry?: boolean }) {
    await api.post(`/api/cicd-runs/${runId}/retry`, options ?? {})
  }

  async function cancelRun(runId: string) {
    try {
      const updated = await api.post<{ id: string; status: number; statusName: string }>(`/api/cicd-runs/${runId}/cancel`, {})
      const run = runs.value.find(r => r.id === runId)
      if (run) {
        run.status = updated.status
        run.statusName = updated.statusName
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to cancel CI/CD run'
    }
  }
  
  async function fetchDashboardSessions() {
    loading.value = true
    error.value = null
    try {
      dashboardSessions.value = await api.get<DashboardAgentSession[]>('/api/dashboard/agent-sessions')
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent sessions'
    } finally {
      loading.value = false
    }
  }

  return {
    runs,
    agentSessions,
    dashboardSessions,
    currentRun,
    currentRunLogs,
    currentSession,
    loading,
    error,
    fetchRuns,
    fetchRun,
    fetchAgentSessions,
    fetchAgentSession,
    retryRun,
    cancelRun,
    fetchDashboardSessions,
  }
})
