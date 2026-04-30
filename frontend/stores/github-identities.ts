import { defineStore } from 'pinia'
import type { GitHubIdentity } from '~/types'

export const useGitHubIdentitiesStore = defineStore('githubIdentities', () => {
  const identities = ref<GitHubIdentity[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchIdentities() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<GitHubIdentity[]>('/api/github-identities')
      identities.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch GitHub identities'
    } finally {
      loading.value = false
    }
  }

  async function createIdentity(token: string, name?: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<GitHubIdentity>('/api/github-identities', { token, name })
      await fetchIdentities()
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create GitHub identity'
    } finally {
      loading.value = false
    }
  }

  async function updateIdentity(id: string, name?: string) {
    loading.value = true
    error.value = null
    try {
      await api.put(`/api/github-identities/${id}`, { name })
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update GitHub identity'
    } finally {
      loading.value = false
    }
  }

  async function deleteIdentity(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/github-identities/${id}`)
      identities.value = identities.value.filter(i => i.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete GitHub identity'
    } finally {
      loading.value = false
    }
  }

  async function mapToAgent(id: string, agentId: string) {
    loading.value = true
    error.value = null
    try {
      await api.put(`/api/github-identities/${id}/agent/${agentId}`, {})
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to map identity to agent'
    } finally {
      loading.value = false
    }
  }

  async function unmapFromAgent(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/github-identities/${id}/agent`)
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unmap identity from agent'
    } finally {
      loading.value = false
    }
  }

  async function mapToProject(id: string, projectId: string) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/github-identities/${id}/projects/${projectId}`, {})
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to map identity to project'
    } finally {
      loading.value = false
    }
  }

  async function unmapFromProject(id: string, projectId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/github-identities/${id}/projects/${projectId}`)
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unmap identity from project'
    } finally {
      loading.value = false
    }
  }

  async function mapToOrg(id: string, orgId: string) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/github-identities/${id}/orgs/${orgId}`, {})
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to map identity to organization'
    } finally {
      loading.value = false
    }
  }

  async function unmapFromOrg(id: string, orgId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/github-identities/${id}/orgs/${orgId}`)
      await fetchIdentities()
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unmap identity from organization'
    } finally {
      loading.value = false
    }
  }

  /** Whether the GitHub OAuth flow for creating identities is configured server-side. */
  const oauthEnabled = ref(false)
  const oauthChecked = ref(false)

  async function fetchOAuthConfig() {
    try {
      const data = await api.get<{ enabled: boolean }>('/api/github-identities/oauth/config')
      oauthEnabled.value = !!data?.enabled
    } catch {
      oauthEnabled.value = false
    } finally {
      oauthChecked.value = true
    }
  }

  /**
   * Redirects the browser to the backend OAuth start endpoint, which itself redirects
   * to GitHub. After the user authorises, GitHub calls back to the backend which
   * upserts the identity and redirects back to `returnUrl` with `?oauth=success|error|refreshed`.
   */
  function startOAuth(returnUrl?: string) {
    if (!import.meta.client) return
    const config = useRuntimeConfig()
    const apiBase = (config.public.apiBase as string) || ''
    const fallback = window.location.pathname + window.location.search
    const target = returnUrl || fallback
    window.location.href = `${apiBase}/api/github-identities/oauth/start?returnUrl=${encodeURIComponent(target)}`
  }

  return {
    identities,
    loading,
    error,
    oauthEnabled,
    oauthChecked,
    fetchIdentities,
    fetchOAuthConfig,
    startOAuth,
    createIdentity,
    updateIdentity,
    deleteIdentity,
    mapToAgent,
    unmapFromAgent,
    mapToProject,
    unmapFromProject,
    mapToOrg,
    unmapFromOrg,
  }
})
