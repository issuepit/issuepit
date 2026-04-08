import { defineStore } from 'pinia'
import type { Organization, OrganizationMember, OrgRole, Project } from '~/types'

export const useOrgsStore = defineStore('orgs', () => {
  const orgs = ref<Organization[]>([])
  const currentOrg = ref<Organization | null>(null)
  const members = ref<OrganizationMember[]>([])
  const orgProjects = ref<Project[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchOrgs() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Organization[]>('/api/orgs')
      orgs.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch organizations'
    } finally {
      loading.value = false
    }
  }

  async function fetchOrg(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Organization>(`/api/orgs/${id}`)
      currentOrg.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch organization'
    } finally {
      loading.value = false
    }
  }

  async function createOrg(payload: { name: string; slug: string; description?: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Organization>('/api/orgs', payload)
      orgs.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create organization'
    } finally {
      loading.value = false
    }
  }

  async function updateOrg(id: string, payload: { name: string; slug: string; maxConcurrentRunners?: number; concurrentJobs?: number | null; actRunnerImage?: string | null; actEnv?: string | null; actSecrets?: string | null; actionCachePath?: string | null; useNewActionCache?: boolean; actionOfflineMode?: boolean; localRepositories?: string | null; skipSteps?: string | null; maxCiCdLoopCount?: number | null; addGitTrailers?: boolean }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Organization>(`/api/orgs/${id}`, payload)
      const idx = orgs.value.findIndex(o => o.id === id)
      if (idx !== -1) orgs.value[idx] = data
      if (currentOrg.value?.id === id) currentOrg.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update organization'
    } finally {
      loading.value = false
    }
  }

  async function deleteOrg(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/orgs/${id}`)
      orgs.value = orgs.value.filter(o => o.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete organization'
    } finally {
      loading.value = false
    }
  }

  async function fetchMembers(orgId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<OrganizationMember[]>(`/api/orgs/${orgId}/members`)
      members.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch members'
    } finally {
      loading.value = false
    }
  }

  async function addMember(orgId: string, userId: string, role: OrgRole) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/orgs/${orgId}/members/${userId}`, { role })
      await fetchMembers(orgId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add member'
    } finally {
      loading.value = false
    }
  }

  async function updateMember(orgId: string, userId: string, role: OrgRole) {
    loading.value = true
    error.value = null
    try {
      await api.put(`/api/orgs/${orgId}/members/${userId}`, { role })
      const idx = members.value.findIndex(m => m.userId === userId)
      if (idx !== -1) members.value[idx].role = role
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update member'
    } finally {
      loading.value = false
    }
  }

  async function removeMember(orgId: string, userId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/orgs/${orgId}/members/${userId}`)
      members.value = members.value.filter(m => m.userId !== userId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove member'
    } finally {
      loading.value = false
    }
  }

  async function fetchOrgProjects(orgId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Project[]>(`/api/orgs/${orgId}/projects`)
      orgProjects.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch org projects'
    } finally {
      loading.value = false
    }
  }

  return {
    orgs,
    currentOrg,
    members,
    orgProjects,
    loading,
    error,
    fetchOrgs,
    fetchOrg,
    createOrg,
    updateOrg,
    deleteOrg,
    fetchMembers,
    addMember,
    updateMember,
    removeMember,
    fetchOrgProjects,
  }
})
