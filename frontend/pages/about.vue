<template>
  <div class="p-8 max-w-2xl">
    <h1 class="text-2xl font-bold text-white mb-2">About</h1>
    <p class="text-gray-400 mb-8">IssuePit — Agent Orchestration Platform with Issue Tracking</p>

    <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
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
</template>

<script setup lang="ts">
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
