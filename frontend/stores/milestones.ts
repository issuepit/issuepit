import { defineStore } from 'pinia'
import type { Milestone } from '~/types'

export const useMilestonesStore = defineStore('milestones', () => {
  const milestones = ref<Milestone[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchMilestones(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Milestone[]>(`/api/projects/${projectId}/milestones`)
      milestones.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch milestones'
    } finally {
      loading.value = false
    }
  }

  async function createMilestone(projectId: string, payload: { title: string; description?: string; startDate?: string; dueDate?: string }) {
    try {
      const data = await api.post<Milestone>(`/api/projects/${projectId}/milestones`, payload)
      milestones.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create milestone'
    }
  }

  async function updateMilestone(projectId: string, milestoneId: string, payload: Partial<Milestone>) {
    try {
      const data = await api.put<Milestone>(`/api/projects/${projectId}/milestones/${milestoneId}`, payload)
      const idx = milestones.value.findIndex(m => m.id === milestoneId)
      if (idx !== -1) milestones.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update milestone'
    }
  }

  async function deleteMilestone(projectId: string, milestoneId: string) {
    try {
      await api.del(`/api/projects/${projectId}/milestones/${milestoneId}`)
      milestones.value = milestones.value.filter(m => m.id !== milestoneId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete milestone'
    }
  }

  return {
    milestones,
    loading,
    error,
    fetchMilestones,
    createMilestone,
    updateMilestone,
    deleteMilestone,
  }
})
