import { defineStore } from 'pinia'
import type { CiCdRun, CiCdRunStatus, CiCdRunLog, CiCdTestSuite, CiCdArtifact, AgentSession, AgentSessionDetail, AgentSessionLog, DashboardAgentSession, WorkflowGraph, WorkflowInfo, LinkedCiCdRun } from '~/types'

export const useCiCdRunsStore = defineStore('cicdRuns', () => {
  const runs = ref<CiCdRun[]>([])
  const agentSessions = ref<AgentSession[]>([])
  const dashboardSessions = ref<DashboardAgentSession[]>([])
  const currentRun = ref<CiCdRun | null>(null)
  const currentRunLogs = ref<CiCdRunLog[]>([])
  const currentRunGraph = ref<WorkflowGraph | null>(null)
  const currentRunGraphError = ref<string | null>(null)
  const currentRunTestSuites = ref<CiCdTestSuite[]>([])
  const currentRunArtifacts = ref<CiCdArtifact[]>([])
  const currentRunLinkedRuns = ref<LinkedCiCdRun[]>([])
  const currentSession = ref<AgentSessionDetail | null>(null)
  const currentSessionLogs = ref<AgentSessionLog[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchRuns(projectId?: string, silent = false) {
    if (!silent) loading.value = true
    error.value = null
    try {
      const url = projectId ? `/api/cicd-runs?projectId=${projectId}` : '/api/cicd-runs'
      runs.value = await api.get<CiCdRun[]>(url)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CI/CD runs'
    } finally {
      if (!silent) loading.value = false
    }
  }

  async function fetchRun(runId: string) {
    loading.value = true
    error.value = null
    try {
      currentRun.value = await api.get<CiCdRun>(`/api/cicd-runs/${runId}`)
      currentRunLogs.value = await api.get<CiCdRunLog[]>(`/api/cicd-runs/${runId}/logs`)
      currentRunGraph.value = null
      currentRunGraphError.value = null
      try {
        currentRunGraph.value = await api.get<WorkflowGraph>(`/api/cicd-runs/${runId}/graph`)
      } catch (e: unknown) {
        currentRunGraph.value = null
        // Preserve error message so the Jobs tab can show a meaningful message.
        // 404 means no workspace is available; other errors are unexpected.
        interface FetchError { status?: number; data?: { error?: string } }
        const fe = e as FetchError
        if (fe?.status === 404) {
          currentRunGraphError.value = fe.data?.error ?? 'Graph not available (no local workspace for this run).'
        } else {
          currentRunGraphError.value = e instanceof Error ? e.message : 'Failed to load workflow graph.'
        }
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch CI/CD run'
    } finally {
      loading.value = false
    }
  }

  /** Refreshes only the run metadata (status, endedAt) without replacing logs or causing a loading flash. */
  async function fetchRunOnly(runId: string) {
    try {
      currentRun.value = await api.get<CiCdRun>(`/api/cicd-runs/${runId}`)
    } catch {
      // best-effort
    }
  }

  /** Fetches test results (parsed TRX suites and cases) for the given run. */
  async function fetchTestResults(runId: string) {
    try {
      currentRunTestSuites.value = await api.get<CiCdTestSuite[]>(`/api/cicd-runs/${runId}/test-results`)
    } catch {
      currentRunTestSuites.value = []
    }
  }

  /** Fetches artifacts produced by the given run. */
  async function fetchArtifacts(runId: string) {
    try {
      currentRunArtifacts.value = await api.get<CiCdArtifact[]>(`/api/cicd-runs/${runId}/artifacts`)
    } catch {
      currentRunArtifacts.value = []
    }
  }

  /** Fetches linked runs (retries, agent session, same commit SHA) for the given run. */
  async function fetchLinkedRuns(runId: string) {
    try {
      currentRunLinkedRuns.value = await api.get<LinkedCiCdRun[]>(`/api/cicd-runs/${runId}/linked`)
    } catch {
      currentRunLinkedRuns.value = []
    }
  }

  async function fetchAgentSessions(projectId: string, silent = false) {
    if (!silent) loading.value = true
    error.value = null
    try {
      agentSessions.value = await api.get<AgentSession[]>(`/api/projects/${projectId}/agent-sessions`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent sessions'
    } finally {
      if (!silent) loading.value = false
    }
  }

  async function fetchAgentSession(sessionId: string) {
    loading.value = true
    error.value = null
    try {
      currentSession.value = await api.get<AgentSessionDetail>(`/api/agent-sessions/${sessionId}`)
      currentSessionLogs.value = await api.get<AgentSessionLog[]>(`/api/agent-sessions/${sessionId}/logs`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent session'
    } finally {
      loading.value = false
    }
  }

  /** Refreshes only the session metadata (status, endedAt) without replacing logs or causing a loading flash. */
  async function fetchAgentSessionOnly(sessionId: string) {
    try {
      currentSession.value = await api.get<AgentSessionDetail>(`/api/agent-sessions/${sessionId}`)
    } catch {
      // best-effort
    }
  }

  async function retrySession(sessionId: string, options?: {
    dockerImageOverride?: string
    keepContainer?: boolean
    agentIdOverride?: string
    modelOverride?: string
    runnerTypeOverride?: number
    useHttpServerOverride?: boolean
    runtimeTypeOverride?: number
  }) {
    return await api.post<{ retriedSessionId: string }>(`/api/agent-sessions/${sessionId}/retry`, options ?? {})
  }

  async function cancelSession(sessionId: string) {
    await api.post(`/api/agent-sessions/${sessionId}/cancel`, {})
  }

  async function retryRun(runId: string, options?: {
    keepContainerOnFailure?: boolean
    forceRetry?: boolean
    noDind?: boolean
    noVolumeMounts?: boolean
    customImage?: string
    customEntrypoint?: string
    customArgs?: string
    actRunnerImage?: string
    eventName?: string
    branch?: string
    commitSha?: string
  }) {
    await api.post(`/api/cicd-runs/${runId}/retry`, options ?? {})
  }

  async function approveRun(runId: string) {
    try {
      const updated = await api.post<{ id: string; status: CiCdRunStatus; statusName: string }>(`/api/cicd-runs/${runId}/approve`, {})
      const run = runs.value.find(r => r.id === runId)
      if (run) {
        run.status = updated.status
        run.statusName = updated.statusName
      }
      if (currentRun.value?.id === runId) {
        currentRun.value.status = updated.status
        currentRun.value.statusName = updated.statusName
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to approve CI/CD run'
    }
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
  
  async function fetchDashboardSessions(silent = false) {
    if (!silent) loading.value = true
    error.value = null
    try {
      dashboardSessions.value = await api.get<DashboardAgentSession[]>('/api/dashboard/agent-sessions?limit=100')
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent sessions'
    } finally {
      if (!silent) loading.value = false
    }
  }

  async function fetchWorkflows(projectId: string): Promise<WorkflowInfo[]> {
    try {
      return await api.get<WorkflowInfo[]>(`/api/projects/${projectId}/git/workflows`)
    } catch {
      return []
    }
  }

  async function triggerRun(request: {
    projectId: string
    commitSha?: string
    eventName: string
    branch?: string
    workflow?: string
    inputs?: Record<string, string>
    customImage?: string
  }) {
    await api.post('/api/cicd-runs/trigger', request)
  }

  return {
    runs,
    agentSessions,
    dashboardSessions,
    currentRun,
    currentRunLogs,
    currentRunGraph,
    currentRunGraphError,
    currentRunTestSuites,
    currentRunArtifacts,
    currentRunLinkedRuns,
    currentSession,
    currentSessionLogs,
    loading,
    error,
    fetchRuns,
    fetchRun,
    fetchRunOnly,
    fetchTestResults,
    fetchArtifacts,
    fetchLinkedRuns,
    fetchAgentSessions,
    fetchAgentSession,
    fetchAgentSessionOnly,
    retrySession,
    cancelSession,
    retryRun,
    cancelRun,
    approveRun,
    fetchDashboardSessions,
    fetchWorkflows,
    triggerRun,
  }
})
