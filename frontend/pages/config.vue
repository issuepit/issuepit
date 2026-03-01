<template>
  <div class="p-8 max-w-4xl mx-auto">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-white">Configuration</h1>
      <p class="text-gray-400 mt-1">Manage API keys, MCP servers, and agent runtime environments.</p>
    </div>

    <!-- Tab bar -->
    <div class="flex gap-1 mb-8 border-b border-gray-800 overflow-x-auto">
      <NuxtLink
        v-for="tab in tabs"
        :key="tab.id"
        :to="tab.href"
        class="px-4 py-2 text-sm font-medium rounded-t transition-colors whitespace-nowrap"
        :class="route.path.startsWith(tab.href)
          ? 'text-white border-b-2 border-brand-500 -mb-px'
          : 'text-gray-400 hover:text-gray-200'"
      >
        {{ tab.label }}
      </NuxtLink>
    </div>

    <NuxtPage />
  </div>
</template>

<script setup lang="ts">
const route = useRoute()

const tabs = [
  { id: 'keys', label: 'API Keys', href: '/config/keys' },
  { id: 'runtimes', label: 'Agent Runtimes', href: '/config/runtimes' },
  { id: 'mcp-servers', label: 'MCP Servers', href: '/config/mcp-servers' },
  { id: 'mcp-playground', label: 'MCP Playground', href: '/config/mcp-playground' },
  { id: 'github-identities', label: 'GitHub Identities', href: '/config/github-identities' },
  { id: 'telegram-bots', label: 'Telegram Bots', href: '/config/telegram-bots' },
]
</script>
