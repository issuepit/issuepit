import { defineStore } from 'pinia'
import type { GitServerRepo, GitServerPermission, GitServerBranchProtection, GitServerAccessLevel } from '~/types'

export const useGitServerStore = defineStore('gitServer', () => {
  const repos = ref<GitServerRepo[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchRepos(orgId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitServerRepo[]>(`/api/orgs/${orgId}/git-server/repos`)
      repos.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch git repos'
    } finally {
      loading.value = false
    }
  }

  async function createRepo(orgId: string, payload: {
    slug: string
    description?: string
    defaultBranch?: string
    projectId?: string
    isTemporary?: boolean
    defaultAccessLevel?: GitServerAccessLevel
  }) {
    const repo = await api.post<GitServerRepo>(`/api/orgs/${orgId}/git-server/repos`, payload)
    repos.value.push(repo)
    return repo
  }

  async function deleteRepo(orgId: string, repoId: string) {
    await api.del(`/api/orgs/${orgId}/git-server/repos/${repoId}`)
    repos.value = repos.value.filter(r => r.id !== repoId)
  }

  async function fetchPermissions(orgId: string, repoId: string) {
    return api.get<GitServerPermission[]>(`/api/orgs/${orgId}/git-server/repos/${repoId}/permissions`)
  }

  async function grantPermission(orgId: string, repoId: string, payload: {
    userId?: string
    apiKeyId?: string
    accessLevel: GitServerAccessLevel
  }) {
    return api.post<GitServerPermission>(`/api/orgs/${orgId}/git-server/repos/${repoId}/permissions`, payload)
  }

  async function revokePermission(orgId: string, repoId: string, permId: string) {
    await api.del(`/api/orgs/${orgId}/git-server/repos/${repoId}/permissions/${permId}`)
  }

  async function fetchBranchProtections(orgId: string, repoId: string) {
    return api.get<GitServerBranchProtection[]>(`/api/orgs/${orgId}/git-server/repos/${repoId}/branch-protections`)
  }

  async function createBranchProtection(orgId: string, repoId: string, payload: {
    pattern: string
    disallowForcePush: boolean
    requirePullRequest: boolean
    allowAdminBypass: boolean
  }) {
    return api.post<GitServerBranchProtection>(`/api/orgs/${orgId}/git-server/repos/${repoId}/branch-protections`, payload)
  }

  async function deleteBranchProtection(orgId: string, repoId: string, ruleId: string) {
    await api.del(`/api/orgs/${orgId}/git-server/repos/${repoId}/branch-protections/${ruleId}`)
  }

  return {
    repos,
    loading,
    error,
    fetchRepos,
    createRepo,
    deleteRepo,
    fetchPermissions,
    grantPermission,
    revokePermission,
    fetchBranchProtections,
    createBranchProtection,
    deleteBranchProtection,
  }
})
