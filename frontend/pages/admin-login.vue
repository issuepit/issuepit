<template>
  <div class="min-h-screen bg-gray-950 flex items-center justify-center">
    <div class="text-center space-y-3">
      <div v-if="error" class="text-sm text-red-400 bg-red-900/20 border border-red-900/30 rounded-lg px-4 py-3">
        {{ error }}
      </div>
      <p v-else class="text-gray-400 text-sm">Generating admin login link…</p>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const config = useRuntimeConfig()
const apiBase = config.public.apiBase as string

const error = ref<string | null>(null)

onMounted(async () => {
  try {
    const data = await $fetch<{ loginUrl: string }>('/api/auth/admin-login-link', {
      baseURL: apiBase,
      credentials: 'include',
    })
    window.location.href = data.loginUrl
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to generate admin login link. Make sure you are accessing this page from the local machine.'
  }
})
</script>
