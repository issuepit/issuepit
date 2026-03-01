import { useAuthStore } from '~/stores/auth'

export default defineNuxtRouteMiddleware(async (to) => {
  if (to.path === '/login') return

  const auth = useAuthStore()

  if (!auth.isAuthenticated && !auth.loading) {
    await auth.fetchMe()
  }

  if (!auth.isAuthenticated) {
    return navigateTo(`/login?returnUrl=${encodeURIComponent(to.fullPath)}`)
  }
})
