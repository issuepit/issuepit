import { defineStore } from 'pinia'
import type { AuthUser } from '~/types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null)
  const loading = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  async function fetchMe() {
    const api = useApi()
    loading.value = true
    try {
      const me = await api.get<AuthUser>('/api/auth/me')
      user.value = me
    } catch {
      user.value = null
    } finally {
      loading.value = false
    }
  }

  async function logout() {
    const api = useApi()
    try {
      await api.post('/api/auth/logout', {})
    } catch {
      // ignore
    }
    user.value = null
    await navigateTo('/login')
  }

  return { user, loading, isAuthenticated, fetchMe, logout }
})
