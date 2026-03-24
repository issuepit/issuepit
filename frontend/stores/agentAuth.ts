import { defineStore } from 'pinia'
import type { AgentAuth, AgentAuthDetail } from '~/types'

export const useAgentAuthStore = defineStore('agentAuth', () => {
  const auths = ref<AgentAuth[]>([])
  const currentAuth = ref<AgentAuthDetail | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchAuths() {
    loading.value = true
    error.value = null
    try {
      auths.value = await api.get<AgentAuth[]>('/api/agent-auth')
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch auth backups'
    }
    finally {
      loading.value = false
    }
  }

  async function fetchAuth(id: string) {
    loading.value = true
    error.value = null
    try {
      currentAuth.value = await api.get<AgentAuthDetail>(`/api/agent-auth/${id}`)
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch auth backup'
    }
    finally {
      loading.value = false
    }
  }

  async function updateAuth(id: string, payload: { label?: string; restoreOnAgentRuns?: boolean }) {
    const updated = await api.patch<AgentAuth>(`/api/agent-auth/${id}`, payload)
    const idx = auths.value.findIndex(a => a.id === id)
    if (idx !== -1) auths.value[idx] = updated
    if (currentAuth.value?.id === id) currentAuth.value = { ...currentAuth.value, ...updated }
    return updated
  }

  async function deleteAuth(id: string) {
    await api.del(`/api/agent-auth/${id}`)
    auths.value = auths.value.filter(a => a.id !== id)
    if (currentAuth.value?.id === id) currentAuth.value = null
  }

  return {
    auths,
    currentAuth,
    loading,
    error,
    fetchAuths,
    fetchAuth,
    updateAuth,
    deleteAuth,
  }
})
