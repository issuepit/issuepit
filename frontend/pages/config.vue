<template>
  <div class="p-8 max-w-4xl mx-auto">
    <div class="mb-6">
      <PageBreadcrumb :items="[
        { label: 'System', to: '/config/keys', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
        { label: 'Configuration', to: '/config/keys', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
      ]" />
    </div>

    <!-- Tab bar -->
    <div class="flex flex-wrap gap-1 mb-8 border-b border-gray-800">
      <NuxtLink
        v-for="tab in tabs"
        :key="tab.id"
        :to="tab.href"
        class="px-4 py-2 text-sm font-medium rounded-t transition-colors"
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
  { id: 'github-identities', label: 'GitHub Identities', href: '/config/github-identities' },
  { id: 'telegram-bots', label: 'Telegram Bots', href: '/config/telegram-bots' },
  { id: 'ci-cd', label: 'CI/CD', href: '/config/ci-cd' },
]
</script>
