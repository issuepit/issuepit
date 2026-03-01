<template>
  <div class="p-8 max-w-2xl">
    <h1 class="text-2xl font-bold text-white mb-2">Settings</h1>
    <p class="text-gray-400 mb-8">Configure your IssuePit workspace.</p>

    <div class="space-y-6">
      <!-- API Section -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">API Configuration</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Backend API URL</label>
            <input :value="apiBase" disabled type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-400 font-mono" />
            <p class="text-xs text-gray-600 mt-1">Set via NUXT_PUBLIC_API_BASE environment variable</p>
          </div>
        </div>
      </div>

      <!-- Agent Token Export -->
      <div v-if="authStore.isAuthenticated" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-1">GitHub Token for Agents</h2>
        <p class="text-sm text-gray-500 mb-4">
          Export your stored GitHub token to use with external agents like the GitHub CLI or GitHub Copilot.
        </p>

        <div v-if="!tokenVisible" class="flex gap-3">
          <button
            class="flex items-center gap-2 bg-gray-800 hover:bg-gray-700 text-gray-200 text-sm font-medium px-4 py-2 rounded-lg transition-colors"
            :disabled="loadingToken"
            @click="loadToken"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
            </svg>
            {{ loadingToken ? 'Loading…' : 'Reveal Token' }}
          </button>
        </div>

        <div v-else class="space-y-3">
          <div class="flex gap-2">
            <input
              :value="githubToken"
              type="text"
              readonly
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono"
            />
            <button
              class="bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm px-3 py-2 rounded-lg transition-colors"
              @click="copyToken"
            >
              {{ copied ? '✓ Copied' : 'Copy' }}
            </button>
            <button
              class="text-gray-600 hover:text-gray-400 text-sm px-2"
              @click="tokenVisible = false; githubToken = null"
            >
              Hide
            </button>
          </div>

          <!-- Usage instructions -->
          <div class="bg-gray-800/50 rounded-lg p-4 space-y-3 text-sm">
            <p class="text-gray-300 font-medium">Use with agents:</p>

            <div>
              <p class="text-gray-500 mb-1">GitHub CLI (<code class="text-brand-400">gh</code>):</p>
              <pre class="text-xs text-gray-300 bg-gray-950 rounded p-2 overflow-x-auto">echo "{{ githubToken }}" | gh auth login --with-token</pre>
            </div>

            <div>
              <p class="text-gray-500 mb-1">Environment variable (Copilot CLI, opencode, etc.):</p>
              <pre class="text-xs text-gray-300 bg-gray-950 rounded p-2 overflow-x-auto">export GITHUB_TOKEN="{{ githubToken }}"</pre>
            </div>
          </div>
        </div>
      </div>

      <!-- Appearance -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Appearance</h2>
        <p class="text-sm text-gray-500">Dark mode is the only theme currently supported.</p>
      </div>

      <!-- About -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-2">About</h2>
        <p class="text-sm text-gray-400">IssuePit v0.1.0 — Agent Orchestration Platform with Issue Tracking</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const apiBase = config.public.apiBase

const authStore = useAuthStore()

const tokenVisible = ref(false)
const githubToken = ref<string | null>(null)
const loadingToken = ref(false)
const copied = ref(false)

async function loadToken() {
  loadingToken.value = true
  githubToken.value = await authStore.getGitHubToken()
  tokenVisible.value = true
  loadingToken.value = false
}

async function copyToken() {
  if (!githubToken.value) return
  await navigator.clipboard.writeText(githubToken.value)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}
</script>
