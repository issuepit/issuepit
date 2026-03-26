import { defineStore } from 'pinia'
import type {
  JiraSyncConfig,
  GitHubSyncRun,
  GitHubSyncRunDetail,
} from '~/types'
import { JiraSyncTriggerMode } from '~/types'

export const useJiraSyncStore = defineStore('jiraSync', () => {
  const config = ref<JiraSyncConfig | null>(null)
  const runs = ref<GitHubSyncRun[]>([])
  const currentRun = ref<GitHubSyncRunDetail | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchConfig(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<JiraSyncConfig>(`/api/projects/${projectId}/jira-sync/config`)
      config.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch Jira sync configuration'
    } finally {
      loading.value = false
    }
  }

  async function saveConfig(projectId: string, req: {
    jiraBaseUrl?: string | null
    jiraProjectKey?: string | null
    jiraEmail?: string | null
    apiKeyId?: string | null
    triggerMode: number
    onlyImportWithParent: boolean
    importComments: boolean
  }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<JiraSyncConfig>(`/api/projects/${projectId}/jira-sync/config`, req)
      config.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to save Jira sync configuration'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchRuns(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubSyncRun[]>(`/api/projects/${projectId}/jira-sync/runs`)
      runs.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch Jira sync runs'
    } finally {
      loading.value = false
    }
  }

  async function fetchRun(projectId: string, runId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubSyncRunDetail>(`/api/projects/${projectId}/jira-sync/runs/${runId}`)
      currentRun.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch Jira sync run'
    } finally {
      loading.value = false
    }
  }

  async function triggerSync(projectId: string, dryRun = false) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/jira-sync/trigger${dryRun ? '?dryRun=true' : ''}`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to trigger Jira import'
      throw e
    } finally {
      loading.value = false
    }
  }

  function $reset() {
    config.value = null
    runs.value = []
    currentRun.value = null
    loading.value = false
    error.value = null
  }

  return {
    config,
    runs,
    currentRun,
    loading,
    error,
    fetchConfig,
    saveConfig,
    fetchRuns,
    fetchRun,
    triggerSync,
    $reset,
  }
})

export { JiraSyncTriggerMode }
