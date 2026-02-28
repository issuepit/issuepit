import { defineStore } from 'pinia'
import type { ApiKey, RuntimeConfiguration, ApiKeyProvider, RuntimeType } from '~/types'

export const useConfigStore = defineStore('config', () => {
  const { get, post, put, del } = useApi()

  // --- API Keys ---
  const apiKeys = ref<ApiKey[]>([])
  const keysLoading = ref(false)

  async function fetchApiKeys() {
    keysLoading.value = true
    try {
      apiKeys.value = await get<ApiKey[]>('/api/config/keys')
    } finally {
      keysLoading.value = false
    }
  }

  async function createApiKey(payload: { orgId: string; name: string; provider: ApiKeyProvider; value: string; expiresAt?: string }) {
    const created = await post<ApiKey>('/api/config/keys', payload)
    await fetchApiKeys()
    return created
  }

  async function deleteApiKey(id: string) {
    await del(`/api/config/keys/${id}`)
    apiKeys.value = apiKeys.value.filter(k => k.id !== id)
  }

  // --- Runtime Configurations ---
  const runtimes = ref<RuntimeConfiguration[]>([])
  const runtimesLoading = ref(false)

  async function fetchRuntimes() {
    runtimesLoading.value = true
    try {
      runtimes.value = await get<RuntimeConfiguration[]>('/api/config/runtimes')
    } finally {
      runtimesLoading.value = false
    }
  }

  async function createRuntime(payload: { orgId: string; name: string; type: RuntimeType; configuration: string; isDefault: boolean }) {
    const created = await post<RuntimeConfiguration>('/api/config/runtimes', payload)
    await fetchRuntimes()
    return created
  }

  async function updateRuntime(id: string, payload: { orgId: string; name: string; type: RuntimeType; configuration: string; isDefault: boolean }) {
    const updated = await put<RuntimeConfiguration>(`/api/config/runtimes/${id}`, payload)
    await fetchRuntimes()
    return updated
  }

  async function deleteRuntime(id: string) {
    await del(`/api/config/runtimes/${id}`)
    runtimes.value = runtimes.value.filter(r => r.id !== id)
  }

  return {
    apiKeys, keysLoading, fetchApiKeys, createApiKey, deleteApiKey,
    runtimes, runtimesLoading, fetchRuntimes, createRuntime, updateRuntime, deleteRuntime,
  }
})
