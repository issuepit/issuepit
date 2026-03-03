import { defineStore } from 'pinia'
import type { ApiKey, RuntimeConfiguration, ApiKeyProvider, RuntimeType, TelegramBot, DigestInterval, PoolStatus } from '~/types'

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

  async function createRuntime(payload: { orgId: string; name: string; type: RuntimeType; configuration: string; isDefault: boolean; maxConcurrentAgents: number }) {
    const created = await post<RuntimeConfiguration>('/api/config/runtimes', payload)
    await fetchRuntimes()
    return created
  }

  async function updateRuntime(id: string, payload: { orgId: string; name: string; type: RuntimeType; configuration: string; isDefault: boolean; maxConcurrentAgents: number }) {
    const updated = await put<RuntimeConfiguration>(`/api/config/runtimes/${id}`, payload)
    await fetchRuntimes()
    return updated
  }

  async function deleteRuntime(id: string) {
    await del(`/api/config/runtimes/${id}`)
    runtimes.value = runtimes.value.filter(r => r.id !== id)
  }

  // --- Pool Status ---
  const poolStatus = ref<PoolStatus | null>(null)
  const poolStatusLoading = ref(false)

  async function fetchPoolStatus() {
    poolStatusLoading.value = true
    try {
      poolStatus.value = await get<PoolStatus>('/api/config/pool-status')
    } finally {
      poolStatusLoading.value = false
    }
  }

  // --- Telegram Bots ---
  const telegramBots = ref<TelegramBot[]>([])
  const telegramBotsLoading = ref(false)

  async function fetchTelegramBots() {
    telegramBotsLoading.value = true
    try {
      telegramBots.value = await get<TelegramBot[]>('/api/config/telegram-bots')
    } finally {
      telegramBotsLoading.value = false
    }
  }

  async function createTelegramBot(payload: { name: string; botToken: string; chatId: string; events: number; isSilent: boolean; digestInterval: DigestInterval; orgId?: string; projectId?: string }) {
    const created = await post<TelegramBot>('/api/config/telegram-bots', payload)
    await fetchTelegramBots()
    return created
  }

  async function updateTelegramBot(id: string, payload: { name: string; botToken: string; chatId: string; events: number; isSilent: boolean; digestInterval: DigestInterval; orgId?: string; projectId?: string }) {
    const updated = await put<TelegramBot>(`/api/config/telegram-bots/${id}`, payload)
    await fetchTelegramBots()
    return updated
  }

  async function deleteTelegramBot(id: string) {
    await del(`/api/config/telegram-bots/${id}`)
    telegramBots.value = telegramBots.value.filter(b => b.id !== id)
  }

  return {
    apiKeys, keysLoading, fetchApiKeys, createApiKey, deleteApiKey,
    runtimes, runtimesLoading, fetchRuntimes, createRuntime, updateRuntime, deleteRuntime,
    poolStatus, poolStatusLoading, fetchPoolStatus,
    telegramBots, telegramBotsLoading, fetchTelegramBots, createTelegramBot, updateTelegramBot, deleteTelegramBot,
  }
})
