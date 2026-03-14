import { defineStore } from 'pinia'
import type {
  GitHubSyncConfig,
  GitHubSyncRun,
  GitHubSyncRunDetail,
  GitHubConflict,
} from '~/types'
import { GitHubSyncTriggerMode, GitHubSyncMode } from '~/types'

export const useGitHubSyncStore = defineStore('githubSync', () => {
  const config = ref<GitHubSyncConfig | null>(null)
  const runs = ref<GitHubSyncRun[]>([])
  const currentRun = ref<GitHubSyncRunDetail | null>(null)
  const conflicts = ref<GitHubConflict[]>([])
  const loading = ref(false)
  const conflictsLoading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchConfig(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubSyncConfig>(`/api/projects/${projectId}/github-sync/config`)
      config.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch sync configuration'
    } finally {
      loading.value = false
    }
  }

  async function saveConfig(projectId: string, req: {
    gitHubIdentityId?: string | null
    gitHubRepo?: string | null
    triggerMode: number
    syncMode: number
  }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<GitHubSyncConfig>(`/api/projects/${projectId}/github-sync/config`, req)
      config.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to save sync configuration'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchRuns(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubSyncRun[]>(`/api/projects/${projectId}/github-sync/runs`)
      runs.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch sync runs'
    } finally {
      loading.value = false
    }
  }

  async function fetchRun(projectId: string, runId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubSyncRunDetail>(`/api/projects/${projectId}/github-sync/runs/${runId}`)
      currentRun.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch sync run'
    } finally {
      loading.value = false
    }
  }

  async function triggerSync(projectId: string) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/github-sync/trigger`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to trigger sync'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchConflicts(projectId: string) {
    conflictsLoading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubConflict[]>(`/api/projects/${projectId}/github-sync/conflicts`)
      conflicts.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch conflicts'
    } finally {
      conflictsLoading.value = false
    }
  }

  function $reset() {
    config.value = null
    runs.value = []
    currentRun.value = null
    conflicts.value = []
    loading.value = false
    conflictsLoading.value = false
    error.value = null
  }

  return {
    config,
    runs,
    currentRun,
    conflicts,
    loading,
    conflictsLoading,
    error,
    fetchConfig,
    saveConfig,
    fetchRuns,
    fetchRun,
    triggerSync,
    fetchConflicts,
    $reset,
  }
})

export { GitHubSyncTriggerMode, GitHubSyncMode }
