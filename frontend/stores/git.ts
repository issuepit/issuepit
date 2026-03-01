import { defineStore } from 'pinia'
import type { GitRepository, GitBranch, GitCommit, GitTreeEntry, GitBlob } from '~/types'

export const useGitStore = defineStore('git', () => {
  const repo = ref<GitRepository | null>(null)
  const branches = ref<GitBranch[]>([])
  const commits = ref<GitCommit[]>([])
  const tree = ref<GitTreeEntry[]>([])
  const blob = ref<GitBlob | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const hasMoreCommits = ref(false)

  const api = useApi()

  async function fetchRepo(projectId: string) {
    loading.value = true
    error.value = null
    try {
      repo.value = await api.get<GitRepository>(`/api/projects/${projectId}/git/repo`)
    } catch (e: unknown) {
      repo.value = null
      // 404 means no repo configured – not an error we surface
      if ((e as { statusCode?: number })?.statusCode !== 404)
        error.value = e instanceof Error ? e.message : 'Failed to fetch git repo'
    } finally {
      loading.value = false
    }
  }

  async function createRepo(projectId: string, payload: { remoteUrl: string; defaultBranch?: string; authUsername?: string; authToken?: string }) {
    loading.value = true
    error.value = null
    try {
      repo.value = await api.post<GitRepository>(`/api/projects/${projectId}/git/repo`, payload)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link git repository'
    } finally {
      loading.value = false
    }
  }

  async function updateRepo(projectId: string, payload: { remoteUrl: string; defaultBranch?: string; authUsername?: string; authToken?: string }) {
    loading.value = true
    error.value = null
    try {
      repo.value = await api.put<GitRepository>(`/api/projects/${projectId}/git/repo`, payload)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update git repository'
    } finally {
      loading.value = false
    }
  }

  async function fetchBranches(projectId: string) {
    loading.value = true
    error.value = null
    try {
      branches.value = await api.get<GitBranch[]>(`/api/projects/${projectId}/git/branches`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch branches'
    } finally {
      loading.value = false
    }
  }

  async function fetchCommits(projectId: string, branch?: string, skip = 0, take = 30) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (branch) params.set('branch', branch)
      params.set('skip', String(skip))
      params.set('take', String(take))
      const data = await api.get<GitCommit[]>(`/api/projects/${projectId}/git/commits?${params}`)
      if (skip === 0) commits.value = data
      else commits.value.push(...data)
      hasMoreCommits.value = data.length === take
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch commits'
    } finally {
      loading.value = false
    }
  }

  async function fetchTree(projectId: string, ref?: string, path?: string) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (ref) params.set('ref_', ref)
      if (path) params.set('path', path)
      tree.value = await api.get<GitTreeEntry[]>(`/api/projects/${projectId}/git/tree?${params}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch tree'
    } finally {
      loading.value = false
    }
  }

  async function fetchBlob(projectId: string, path: string, ref?: string) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ path })
      if (ref) params.set('ref_', ref)
      blob.value = await api.get<GitBlob>(`/api/projects/${projectId}/git/blob?${params}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch file'
    } finally {
      loading.value = false
    }
  }

  async function triggerFetch(projectId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/fetch`, {})
      if (repo.value) repo.value.lastFetchedAt = new Date().toISOString()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Fetch failed'
    }
  }

  async function triggerClone(projectId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/clone`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Clone failed'
    }
  }

  function reset() {
    repo.value = null
    branches.value = []
    commits.value = []
    tree.value = []
    blob.value = null
    error.value = null
    hasMoreCommits.value = false
  }

  return {
    repo,
    branches,
    commits,
    tree,
    blob,
    loading,
    error,
    hasMoreCommits,
    fetchRepo,
    createRepo,
    updateRepo,
    fetchBranches,
    fetchCommits,
    fetchTree,
    fetchBlob,
    triggerFetch,
    triggerClone,
    reset
  }
})
