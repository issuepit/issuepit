import { defineStore } from 'pinia'
import type { Label } from '~/types'

export const useLabelsStore = defineStore('labels', () => {
  const labels = ref<Label[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchLabels(projectId: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Label[]>(`/api/projects/${projectId}/labels`)
      labels.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch labels'
    } finally {
      loading.value = false
    }
  }

  async function createLabel(projectId: string, name: string, color: string) {
    try {
      const data = await api.post<Label>(`/api/projects/${projectId}/labels`, { name, color })
      labels.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create label'
    }
  }

  async function deleteLabel(projectId: string, labelId: string) {
    try {
      await api.del(`/api/projects/${projectId}/labels/${labelId}`)
      labels.value = labels.value.filter(l => l.id !== labelId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete label'
    }
  }

  return {
    labels,
    loading,
    error,
    fetchLabels,
    createLabel,
    deleteLabel,
  }
})
