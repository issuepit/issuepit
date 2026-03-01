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
        <!-- Tabs -->
        <div class="flex border-b border-gray-800 mb-6">
          <button
            class="flex-1 pb-3 text-sm font-medium transition-colors"
            :class="tab === 'local' ? 'text-white border-b-2 border-brand-500' : 'text-gray-500 hover:text-gray-300'"
            @click="tab = 'local'"
          >
            Sign in
          </button>
          <button
            class="flex-1 pb-3 text-sm font-medium transition-colors"
            :class="tab === 'register' ? 'text-white border-b-2 border-brand-500' : 'text-gray-500 hover:text-gray-300'"
            @click="tab = 'register'"
          >
            Create account
          </button>
        </div>

        <!-- Local login -->
        <form v-if="tab === 'local'" class="space-y-4" @submit.prevent="handleLogin">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Username</label>
            <input
              v-model="loginForm.username"
              type="text"
              required
              autocomplete="username"
              placeholder="username"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Password</label>
            <input
              v-model="loginForm.password"
              type="password"
              required
              autocomplete="current-password"
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
            {{ loading ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>

        <!-- Register -->
        <form v-else class="space-y-4" @submit.prevent="handleRegister">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Username</label>
            <input
              v-model="registerForm.username"
              type="text"
              required
              autocomplete="username"
              placeholder="username"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Email <span class="text-gray-500">(optional)</span></label>
            <input
              v-model="registerForm.email"
              type="email"
              autocomplete="email"
              placeholder="you@example.com"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Password</label>
            <input
              v-model="registerForm.password"
              type="password"
              required
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
            {{ loading ? 'Creating account…' : 'Create account' }}
          </button>
        </form>

        <!-- Divider -->
        <div v-if="githubLoginUrl" class="flex items-center gap-3 my-5">
          <div class="flex-1 h-px bg-gray-800" />
          <span class="text-xs text-gray-500">or</span>
          <div class="flex-1 h-px bg-gray-800" />
        </div>

        <!-- GitHub SSO -->
        <a
          v-if="githubLoginUrl"
          :href="githubLoginUrl"
          class="flex items-center justify-center gap-3 w-full bg-gray-800 hover:bg-gray-700
                 text-white font-medium py-2.5 px-4 rounded-lg border border-gray-700
                 transition-colors duration-150"
        >
          <!-- GitHub logo -->
          <svg class="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 0C5.37 0 0 5.37 0 12c0 5.3 3.44 9.8 8.2 11.38.6.11.82-.26.82-.58
                     0-.28-.01-1.02-.02-2C5.67 21.46 4.97 19.46 4.97 19.46c-.55-1.4-1.34-1.77-1.34-1.77
                     -1.09-.75.08-.73.08-.73 1.2.08 1.84 1.24 1.84 1.24 1.07 1.83 2.8 1.3 3.49 1
                     .11-.78.42-1.3.76-1.6-2.67-.3-5.47-1.33-5.47-5.93 0-1.31.47-2.38 1.24-3.22
                     -.13-.3-.54-1.52.12-3.17 0 0 1.01-.32 3.3 1.23a11.5 11.5 0 0 1 3-.4c1.02.005
                     2.04.14 3 .4 2.28-1.55 3.29-1.23 3.29-1.23.66 1.65.25 2.87.12 3.17
                     .77.84 1.24 1.91 1.24 3.22 0 4.61-2.81 5.63-5.48 5.92.43.37.81 1.1.81 2.22
                     0 1.6-.01 2.9-.01 3.29 0 .32.21.7.82.58C20.56 21.8 24 17.3 24 12c0-6.63-5.37-12-12-12z" />
          </svg>
          Continue with GitHub
        </a>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'

definePageMeta({ layout: false })

const config = useRuntimeConfig()
const apiBase = config.public.apiBase as string
const auth = useAuthStore()
const router = useRouter()

// Build the GitHub login URL so the callback redirects back to the current page.
const returnUrl = useRoute().query.returnUrl as string | undefined
const query = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''
const githubLoginUrl = apiBase ? `${apiBase}/api/auth/github${query}` : null

const tab = ref<'local' | 'register'>('local')
const loading = ref(false)
const error = ref<string | null>(null)

const loginForm = reactive({ username: '', password: '' })
const registerForm = reactive({ username: '', email: '', password: '' })

watch(tab, () => { error.value = null })

async function handleLogin() {
  error.value = null
  loading.value = true
  try {
    const api = useApi()
    await api.post('/api/auth/login', { username: loginForm.username, password: loginForm.password })
    await auth.fetchMe()
    await router.push(returnUrl ?? '/')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Invalid username or password.'
  } finally {
    loading.value = false
  }
}

async function handleRegister() {
  error.value = null
  loading.value = true
  try {
    const api = useApi()
    await api.post('/api/auth/register', {
      username: registerForm.username,
      email: registerForm.email || undefined,
      password: registerForm.password,
    })
    await auth.fetchMe()
    await router.push(returnUrl ?? '/')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to create account.'
  } finally {
    loading.value = false
  }
}
</script>
