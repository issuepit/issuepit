<template>
  <div class="min-h-screen bg-gray-950 flex items-center justify-center">
    <div class="text-center">
      <div v-if="error" class="bg-red-900/30 border border-red-700 rounded-xl p-6 max-w-sm">
        <p class="text-red-400 font-medium mb-2">Authentication failed</p>
        <p class="text-gray-400 text-sm">{{ error }}</p>
        <NuxtLink to="/login" class="mt-4 inline-block text-brand-400 hover:text-brand-300 text-sm">
          Try again →
        </NuxtLink>
      </div>
      <div v-else class="flex items-center gap-3 text-gray-400">
        <svg class="animate-spin w-5 h-5" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        Completing sign-in…
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const route = useRoute()
const authStore = useAuthStore()
const error = ref<string | null>(null)

onMounted(async () => {
  const token = route.query.token as string | undefined

  if (!token) {
    error.value = 'No authentication token received.'
    return
  }

  authStore.setToken(token)
  await authStore.fetchMe()

  if (!authStore.isAuthenticated) {
    error.value = 'Could not verify your identity. Please try again.'
    return
  }

  await navigateTo('/')
})
</script>
