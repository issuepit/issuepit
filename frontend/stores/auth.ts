import { defineStore } from 'pinia'
import type { AuthUser } from '~/types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null)
  const loading = ref(false)
  const initialized = ref(false)

  const isAuthenticated = computed(() => user.value !== null)

  const api = useApi()
  const config = useRuntimeConfig()

  function setToken(token: string) {
    localStorage.setItem('issuepit_token', token)
  }

  function clearToken() {
    localStorage.removeItem('issuepit_token')
  }

  function getToken(): string | null {
    if (!process.client) return null
    return localStorage.getItem('issuepit_token')
  }

  async function fetchMe() {
    if (!getToken()) return
    try {
      const data = await api.get<AuthUser>('/api/auth/me')
      user.value = data
    } catch {
      // Token may be expired or invalid
      clearToken()
      user.value = null
    }
  }

  async function init() {
    if (initialized.value) return
    initialized.value = true
    await fetchMe()
  }

  function loginWithGitHub(returnTo?: string) {
    const base = config.public.apiBase as string
    const callbackUrl = `${window.location.origin}/auth/callback`
    const url = new URL(`${base}/api/auth/github`)
    url.searchParams.set('return_to', returnTo ?? callbackUrl)
    window.location.href = url.toString()
  }

  async function getGitHubToken(): Promise<string | null> {
    try {
      const data = await api.get<{ token: string }>('/api/auth/token')
      return data.token
    } catch {
      return null
    }
  }

  async function logout() {
    loading.value = true
    try {
      await api.del('/api/auth/logout')
    } catch {
      // ignore errors on logout
    } finally {
      clearToken()
      user.value = null
      loading.value = false
    }
  }

  return {
    user,
    loading,
    initialized,
    isAuthenticated,
    setToken,
    clearToken,
    getToken,
    fetchMe,
    init,
    loginWithGitHub,
    getGitHubToken,
    logout,
  }
})
