export default defineNuxtRouteMiddleware(async (to) => {
  // Skip auth check for public pages
  const publicPaths = ['/login', '/auth/callback']
  if (publicPaths.some(p => to.path.startsWith(p))) return

  const authStore = useAuthStore()
  await authStore.init()

  if (!authStore.isAuthenticated) {
    // Only use relative paths for the redirect parameter to prevent open-redirect attacks
    const redirect = to.fullPath.startsWith('/') ? to.fullPath : '/'
    return navigateTo(`/login?redirect=${encodeURIComponent(redirect)}`)
  }
})
