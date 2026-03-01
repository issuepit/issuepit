import { defineStore } from 'pinia'
import type { Team, TeamMember } from '~/types'

export const useTeamsStore = defineStore('teams', () => {
  const teams = ref<Team[]>([])
  const currentTeam = ref<Team | null>(null)
  const members = ref<TeamMember[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchTeams(orgId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Team[]>(`/api/orgs/${orgId}/teams`)
      teams.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch teams'
    } finally {
      loading.value = false
    }
  }

  async function createTeam(orgId: string, payload: { name: string; slug: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Team>(`/api/orgs/${orgId}/teams`, payload)
      teams.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create team'
    } finally {
      loading.value = false
    }
  }

  async function updateTeam(orgId: string, id: string, payload: { name: string; slug: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Team>(`/api/orgs/${orgId}/teams/${id}`, payload)
      const idx = teams.value.findIndex(t => t.id === id)
      if (idx !== -1) teams.value[idx] = data
      if (currentTeam.value?.id === id) currentTeam.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update team'
    } finally {
      loading.value = false
    }
  }

  async function deleteTeam(orgId: string, id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/orgs/${orgId}/teams/${id}`)
      teams.value = teams.value.filter(t => t.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete team'
    } finally {
      loading.value = false
    }
  }

  async function fetchMembers(orgId: string, teamId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<TeamMember[]>(`/api/orgs/${orgId}/teams/${teamId}/members`)
      members.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch team members'
    } finally {
      loading.value = false
    }
  }

  async function addMember(orgId: string, teamId: string, userId: string) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/orgs/${orgId}/teams/${teamId}/members/${userId}`, {})
      await fetchMembers(orgId, teamId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add team member'
    } finally {
      loading.value = false
    }
  }

  async function removeMember(orgId: string, teamId: string, userId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/orgs/${orgId}/teams/${teamId}/members/${userId}`)
      members.value = members.value.filter(m => m.userId !== userId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to remove team member'
    } finally {
      loading.value = false
    }
  }

  return {
    teams,
    currentTeam,
    members,
    loading,
    error,
    fetchTeams,
    createTeam,
    updateTeam,
    deleteTeam,
    fetchMembers,
    addMember,
    removeMember,
  }
})
