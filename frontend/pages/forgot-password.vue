<template>
  <div class="min-h-screen bg-gray-950 flex items-center justify-center">
    <div class="w-full max-w-sm">
      <!-- Logo -->
      <div class="flex items-center justify-center gap-3 mb-10">
        <div class="w-10 h-10 rounded-xl bg-brand-600 flex items-center justify-center">
          <svg class="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18m0 0h10a2 2 0 002-2V9M9 21H5a2 2 0 01-2-2V9m0 0h18" />
          </svg>
        </div>
        <span class="text-2xl font-bold text-white tracking-wide">IssuePit</span>
      </div>

      <!-- Card -->
      <div class="bg-gray-900 border border-gray-800 rounded-2xl p-8 shadow-xl">
        <h1 class="text-lg font-semibold text-white mb-2">Reset your password</h1>
        <p class="text-sm text-gray-400 mb-6">
          Enter your username or email and we'll generate a reset link. Because this
          self-hosted instance may not have email configured, ask your administrator
          to retrieve the reset link from the application logs.
        </p>

        <form v-if="!submitted" class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Username or email</label>
            <input
              v-model="username"
              type="text"
              required
              autocomplete="username"
              placeholder="username"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div v-if="error" class="text-sm text-red-400 bg-red-900/20 border border-red-900/30 rounded-lg px-3 py-2">
            {{ error }}
          </div>
          <button
            type="submit"
            :disabled="loading"
            class="w-full bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white font-medium py-2.5 px-4 rounded-lg transition-colors duration-150"
          >
            {{ loading ? 'Sending…' : 'Send reset link' }}
          </button>
        </form>

        <div v-else class="space-y-4">
          <div class="text-sm text-gray-300 bg-gray-800/50 border border-gray-700 rounded-lg px-3 py-3">
            If an account matches that username or email, a password reset link has been
            generated. Check the application logs (or contact your administrator) for the
            reset URL.
          </div>
        </div>

        <div class="mt-6 text-center">
          <NuxtLink to="/login" class="text-sm text-brand-400 hover:text-brand-300">
            ← Back to sign in
          </NuxtLink>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const username = ref('')
const loading = ref(false)
const error = ref<string | null>(null)
const submitted = ref(false)

async function handleSubmit() {
  error.value = null
  loading.value = true
  try {
    const api = useApi()
    await api.post('/api/auth/forgot-password', { username: username.value })
    submitted.value = true
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to request password reset.'
  } finally {
    loading.value = false
  }
}
</script>
