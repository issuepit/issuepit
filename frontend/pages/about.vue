<template>
  <div class="p-8 max-w-2xl">
    <PageBreadcrumb :items="[
      { label: 'System', to: '/about', icon: 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z' },
      { label: 'About', to: '/about', icon: 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z' },
    ]" class="mb-8" />

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
