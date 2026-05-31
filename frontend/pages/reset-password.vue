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
        <h1 class="text-lg font-semibold text-white mb-6">Choose a new password</h1>

        <div v-if="!token" class="text-sm text-red-400 bg-red-900/20 border border-red-900/30 rounded-lg px-3 py-3">
          Missing reset token. Please request a new password reset link.
        </div>

        <form v-else-if="!success" class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">New password</label>
            <input
              v-model="newPassword"
              type="password"
              required
              minlength="6"
              autocomplete="new-password"
              placeholder="••••••••"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Confirm new password</label>
            <input
              v-model="confirmPassword"
              type="password"
              required
              minlength="6"
              autocomplete="new-password"
              placeholder="••••••••"
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
            {{ loading ? 'Updating…' : 'Set new password' }}
          </button>
        </form>

        <div v-else class="space-y-4">
          <div class="text-sm text-green-400 bg-green-900/20 border border-green-900/30 rounded-lg px-3 py-3">
            Your password has been updated. You can now sign in with your new password.
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

const route = useRoute()
const router = useRouter()

const token = computed(() => (route.query.token as string | undefined) ?? '')

const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const error = ref<string | null>(null)
const success = ref(false)

async function handleSubmit() {
  error.value = null

  if (newPassword.value.length < 6) {
    error.value = 'Password must be at least 6 characters.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  try {
    const api = useApi()
    await api.post('/api/auth/reset-password', {
      token: token.value,
      newPassword: newPassword.value,
    })
    success.value = true
    // Auto-redirect to /login after a short delay.
    setTimeout(() => { router.push('/login') }, 2000)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to reset password. The link may be invalid or expired.'
  } finally {
    loading.value = false
  }
}
</script>
