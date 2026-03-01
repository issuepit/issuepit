import { defineStore } from 'pinia'
import type { ProjectMember, ProjectPermission } from '~/types'

export const useProjectMembersStore = defineStore('projectMembers', () => {
  const members = ref<ProjectMember[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchMembers(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<ProjectMember[]>(`/api/projects/${projectId}/members`)
      members.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch project members'
    } finally {
      loading.value = false
    }
  }

  async function addMember(projectId: string, payload: { userId?: string; teamId?: string; permissions: ProjectPermission }) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/members`, payload)
      await fetchMembers(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add project member'
    } finally {
      loading.value = false
    }
  }

  async function updateMember(projectId: string, payload: { userId?: string; teamId?: string; permissions: ProjectPermission }) {
    loading.value = true
    error.value = null
    try {
      await api.put(`/api/projects/${projectId}/members`, payload)
      await fetchMembers(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update project member'
    } finally {
      loading.value = false
    }
  }

  async function removeMember(projectId: string, payload: { userId?: string; teamId?: string }) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/projects/${projectId}/members`, { body: payload })
      await fetchMembers(projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove project member'
    } finally {
      loading.value = false
    }
  }

  return {
    members,
    loading,
    error,
    fetchMembers,
    addMember,
    updateMember,
    removeMember,
  }
})
