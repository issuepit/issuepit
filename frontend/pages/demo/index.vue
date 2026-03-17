<template>
  <div class="p-8 max-w-4xl">
    <h1 class="text-2xl font-bold text-white mb-2">Component Demo</h1>
    <p class="text-gray-400 text-sm mb-8">Interactive previews of UI components. Not linked in the sidebar.</p>

    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <NuxtLink
        v-for="page in pages" :key="page.to"
        :to="page.to"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-brand-500 transition-colors group">
        <div class="flex items-center gap-3 mb-2">
          <span class="text-2xl">{{ page.icon }}</span>
          <span class="font-semibold text-white group-hover:text-brand-300 transition-colors">{{ page.title }}</span>
        </div>
        <p class="text-sm text-gray-400">{{ page.description }}</p>
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
const pages = [
  {
    to: '/demo/test-history-chart',
    icon: '📊',
    title: 'Test History Chart',
    description: 'Bar chart variants: failure-rate, pass/fail, groups; date vs runs x-axis; count vs duration y-axis.',
  },
  {
    to: '/demo/kanban',
    icon: '🗂',
    title: 'Kanban Dashboard Card',
    description: 'Inline kanban board embedded as a dashboard section card.',
  },
]
</script>
