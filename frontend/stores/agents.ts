import { defineStore } from 'pinia'
import type { Agent } from '~/types'

export const useAgentsStore = defineStore('agents', () => {
  const agents = ref<Agent[]>([])
  const currentAgent = ref<Agent | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchAgents() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Agent[]>('/api/agents')
      agents.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agents'
    } finally {
      loading.value = false
    }
  }

  async function fetchAgent(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Agent>(`/api/agents/${id}`)
      currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent'
    } finally {
      loading.value = false
    }
  }

  async function createAgent(payload: Partial<Agent>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Agent>('/api/agents', payload)
      agents.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create agent'
    } finally {
      loading.value = false
    }
  }

  async function updateAgent(id: string, payload: Partial<Agent>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Agent>(`/api/agents/${id}`, payload)
      const idx = agents.value.findIndex(a => a.id === id)
      if (idx !== -1) agents.value[idx] = data
      if (currentAgent.value?.id === id) currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update agent'
    } finally {
      loading.value = false
    }
  }

  async function deleteAgent(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/agents/${id}`)
      agents.value = agents.value.filter(a => a.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete agent'
    } finally {
      loading.value = false
    }
  }

  async function toggleAgent(id: string, isActive: boolean) {
    loading.value = true
    error.value = null
    try {
      const data = await api.patch<Agent>(`/api/agents/${id}/active`, { isActive })
      const idx = agents.value.findIndex(a => a.id === id)
      if (idx !== -1) agents.value[idx] = data
      if (currentAgent.value?.id === id) currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to toggle agent'
    } finally {
      loading.value = false
    }
  }

  return {
    agents,
    currentAgent,
    loading,
    error,
    fetchAgents,
    fetchAgent,
    createAgent,
    updateAgent,
    deleteAgent,
    toggleAgent
  }
})
