<template>
  <div class="p-8 max-w-4xl mx-auto">
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-white">Configuration</h1>
      <p class="text-gray-400 mt-1">Manage API keys and agent runtime environments.</p>
    </div>

    <!-- Tab bar -->
    <div class="flex gap-1 mb-8 border-b border-gray-800">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        class="px-4 py-2 text-sm font-medium rounded-t transition-colors"
        :class="active === tab.id
          ? 'text-white border-b-2 border-brand-500 -mb-px'
          : 'text-gray-400 hover:text-gray-200'"
        @click="active = tab.id"
      >
        {{ tab.label }}
      </button>
    </div>

    <NuxtPage />
  </div>
</template>

<script setup lang="ts">
const active = ref('keys')
const route = useRoute()

const tabs = [
  { id: 'keys', label: 'API Keys', href: '/config/keys' },
  { id: 'runtimes', label: 'Agent Runtimes', href: '/config/runtimes' },
  { id: 'mcp-servers', label: 'MCP Servers', href: '/config/mcp-servers' },
  { id: 'github-identities', label: 'GitHub Identities', href: '/config/github-identities' },
  { id: 'telegram-bots', label: 'Telegram Bots', href: '/config/telegram-bots' },
]

watch(active, (val) => {
  const tab = tabs.find(t => t.id === val)
  if (tab) navigateTo(tab.href)
})

watch(() => route.path, (path) => {
  const match = tabs.find(t => path.startsWith(t.href))
  if (match) active.value = match.id
}, { immediate: true })
</script>
