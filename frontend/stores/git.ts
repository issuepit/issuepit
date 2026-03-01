import { defineStore } from 'pinia'
import type { GitBranch, GitCommit, GitTreeEntry, GitBlob } from '~/types'

export const useGitStore = defineStore('git', () => {
  const branches = ref<GitBranch[]>([])
  const commits = ref<GitCommit[]>([])
  const treeEntries = ref<GitTreeEntry[]>([])
  const blob = ref<GitBlob | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchBranches(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitBranch[]>(`/api/projects/${projectId}/git/branches`)
      branches.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch branches'
    } finally {
      loading.value = false
    }
  }

  async function fetchCommits(projectId: string, gitRef?: string, page = 1) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ page: String(page) })
      if (gitRef) params.set('gitRef', gitRef)
      const data = await api.get<GitCommit[]>(`/api/projects/${projectId}/git/commits?${params}`)
      commits.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch commits'
    } finally {
      loading.value = false
    }
  }

  async function fetchTree(projectId: string, gitRef?: string, path?: string) {
    loading.value = true
    error.value = null
    blob.value = null
    try {
      const params = new URLSearchParams()
      if (gitRef) params.set('gitRef', gitRef)
      if (path) params.set('path', path)
      const data = await api.get<GitTreeEntry[]>(`/api/projects/${projectId}/git/tree?${params}`)
      treeEntries.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch tree'
    } finally {
      loading.value = false
    }
  }

  async function fetchBlob(projectId: string, path: string, gitRef?: string) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ path })
      if (gitRef) params.set('gitRef', gitRef)
      const data = await api.get<GitBlob>(`/api/projects/${projectId}/git/blob?${params}`)
      blob.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch file'
    } finally {
      loading.value = false
    }
  }

  function reset() {
    branches.value = []
    commits.value = []
    treeEntries.value = []
    blob.value = null
    error.value = null
  }

  return {
    branches,
    commits,
    treeEntries,
    blob,
    loading,
    error,
    fetchBranches,
    fetchCommits,
    fetchTree,
    fetchBlob,
    reset,
  }
})
