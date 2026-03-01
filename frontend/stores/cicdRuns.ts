import { defineStore } from 'pinia'
import type { CiCdRun, AgentSession, DashboardAgentSession } from '~/types'

export const useCiCdRunsStore = defineStore('cicdRuns', () => {
  const runs = ref<CiCdRun[]>([])
  const agentSessions = ref<AgentSession[]>([])
  const dashboardSessions = ref<DashboardAgentSession[]>([])
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
    loading,
    error,
    fetchRuns,
    fetchAgentSessions,
    fetchDashboardSessions,
  }
})
