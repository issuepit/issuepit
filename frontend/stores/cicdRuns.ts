import { defineStore } from 'pinia'
import type { CiCdRun, AgentSession } from '~/types'

export const useCiCdRunsStore = defineStore('cicdRuns', () => {
  const runs = ref<CiCdRun[]>([])
  const agentSessions = ref<AgentSession[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchRuns(projectId: string) {
    loading.value = true
    error.value = null
    try {
      runs.value = await api.get<CiCdRun[]>(`/api/cicd-runs?projectId=${projectId}`)
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

  return {
    runs,
    agentSessions,
    loading,
    error,
    fetchRuns,
    fetchAgentSessions,
  }
})
