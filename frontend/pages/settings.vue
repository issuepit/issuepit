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

      <!-- Appearance -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Appearance</h2>
        <p class="text-sm text-gray-500">Dark mode is the only theme currently supported.</p>
      </div>

      <!-- About -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-2">About</h2>
        <p class="text-sm text-gray-400 mb-3">IssuePit — Agent Orchestration Platform with Issue Tracking</p>
        <div class="space-y-1.5 text-sm">
          <div class="flex items-center gap-2">
            <span class="text-gray-500 w-20">Version</span>
            <a v-if="versionInfo?.version"
              :href="`https://github.com/issuepit/issuepit/releases/tag/v${versionInfo.version}`"
              target="_blank" rel="noopener noreferrer"
              class="text-brand-400 hover:text-brand-300 font-mono">
              v{{ versionInfo.version }}
            </a>
            <span v-else class="text-gray-500 font-mono">—</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-gray-500 w-20">Commit</span>
            <a v-if="versionInfo?.gitHash"
              :href="`https://github.com/issuepit/issuepit/commit/${versionInfo.gitHash}`"
              target="_blank" rel="noopener noreferrer"
              class="text-brand-400 hover:text-brand-300 font-mono">
              {{ versionInfo.gitHashShort }}
            </a>
            <span v-else class="text-gray-500 font-mono">—</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const apiBase = config.public.apiBase

interface VersionInfo {
  version: string
  gitHash: string | null
  gitHashShort: string | null
}

const { get } = useApi()
const versionInfo = ref<VersionInfo | null>(null)

onMounted(async () => {
  try {
    versionInfo.value = await get<VersionInfo>('/api/version')
  } catch {
    // version info is non-critical; ignore errors
  }
})
</script>
