import { defineStore } from 'pinia'
import type { GitRepository, GitOriginMode, GitBranch, GitCommit, GitTreeEntry, GitBlob, GitDiffFile } from '~/types'

export const useGitStore = defineStore('git', () => {
  const repos = ref<GitRepository[]>([])
  /** The primary repo used for browsing (first Working, otherwise first). */
  const repo = computed(() => repos.value.find(r => r.mode === 'Working') ?? repos.value[0] ?? null)
  const branches = ref<GitBranch[]>([])
  const commits = ref<GitCommit[]>([])
  const tree = ref<GitTreeEntry[]>([])
  const blob = ref<GitBlob | null>(null)
  const diff = ref<GitDiffFile[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const hasMoreCommits = ref(false)

  const api = useApi()

  async function fetchRepos(projectId: string) {
    loading.value = true
    error.value = null
    try {
      repos.value = await api.get<GitRepository[]>(`/api/projects/${projectId}/git/repos`)
    } catch (e: unknown) {
      repos.value = []
      if ((e as { statusCode?: number })?.statusCode !== 404)
        error.value = e instanceof Error ? e.message : 'Failed to fetch git repositories'
    } finally {
      loading.value = false
    }
  }

  /** @deprecated Use fetchRepos instead. Kept for backward compatibility. */
  async function fetchRepo(projectId: string) {
    return fetchRepos(projectId)
  }

  async function addRepo(projectId: string, payload: { remoteUrl: string; defaultBranch?: string; authUsername?: string; authToken?: string; mode?: GitOriginMode }) {
    loading.value = true
    error.value = null
    try {
      const created = await api.post<GitRepository>(`/api/projects/${projectId}/git/repos`, payload)
      repos.value = [...repos.value, created]
      return created
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add git repository'
    } finally {
      loading.value = false
    }
  }

  async function updateRepo(projectId: string, repoId: string, payload: { remoteUrl: string; defaultBranch?: string; authUsername?: string; authToken?: string; mode?: GitOriginMode }) {
    loading.value = true
    error.value = null
    try {
      const updated = await api.put<GitRepository>(`/api/projects/${projectId}/git/repos/${repoId}`, payload)
      repos.value = repos.value.map(r => r.id === repoId ? updated : r)
      return updated
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update git repository'
    } finally {
      loading.value = false
    }
  }

  async function deleteRepo(projectId: string, repoId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/projects/${projectId}/git/repos/${repoId}`)
      repos.value = repos.value.filter(r => r.id !== repoId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete git repository'
    } finally {
      loading.value = false
    }
  }

  /** @deprecated Use addRepo instead. Kept for backward compatibility. */
  async function createRepo(projectId: string, payload: { remoteUrl: string; defaultBranch?: string; authUsername?: string; authToken?: string; mode?: GitOriginMode }) {
    return addRepo(projectId, payload)
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

  async function fetchDiff(projectId: string, baseBranch: string, compareBranch: string, context = 3, noLimit = false) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ base_: baseBranch, compare: compareBranch, context: String(context) })
      if (noLimit) params.set('noLimit', 'true')
      diff.value = await api.get<GitDiffFile[]>(`/api/projects/${projectId}/git/diff?${params}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch diff'
    } finally {
      loading.value = false
    }
  }

  async function triggerFetch(projectId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/fetch`, {})
      // Refresh the repos list so lastFetchedAt timestamps are up to date.
      await fetchRepos(projectId)
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

  async function enableRepo(projectId: string, repoId: string) {
    error.value = null
    try {
      const updated = await api.post<GitRepository>(`/api/projects/${projectId}/git/repos/${repoId}/enable`, {})
      repos.value = repos.value.map(r => r.id === repoId ? updated : r)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to enable repository'
    }
  }

  async function fetchRemote(projectId: string, repoId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/repos/${repoId}/fetch`, {})
      await fetchRepos(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Fetch failed'
      throw e
    }
  }

  async function pullRemote(projectId: string, repoId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/repos/${repoId}/pull`, {})
      await fetchRepos(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Pull failed'
      throw e
    }
  }

  async function pushRemote(projectId: string, repoId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/repos/${repoId}/push`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Push failed'
      throw e
    }
  }

  async function syncRemote(projectId: string, repoId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/git/repos/${repoId}/sync`, {})
      await fetchRepos(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Sync failed'
      throw e
    }
  }

  function reset() {
    repos.value = []
    branches.value = []
    commits.value = []
    tree.value = []
    blob.value = null
    diff.value = []
    error.value = null
    hasMoreCommits.value = false
  }

  return {
    repos,
    repo,
    branches,
    commits,
    tree,
    blob,
    diff,
    loading,
    error,
    hasMoreCommits,
    fetchRepo,
    fetchRepos,
    createRepo,
    addRepo,
    updateRepo,
    deleteRepo,
    fetchBranches,
    fetchCommits,
    fetchTree,
    fetchBlob,
    fetchDiff,
    triggerFetch,
    triggerClone,
    enableRepo,
    fetchRemote,
    pullRemote,
    pushRemote,
    syncRemote,
    reset
  }
})
