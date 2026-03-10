<template>
  <div class="flex items-center gap-2.5 flex-wrap">
    <template v-for="(item, index) in items" :key="index">
      <span v-if="index > 0" class="text-gray-600 text-xl select-none">/</span>
      <NuxtLink
        :to="item.to"
        :class="[
          'flex items-center gap-2 font-bold text-2xl transition-colors',
          index < items.length - 1
            ? 'text-gray-500 hover:text-gray-300'
            : 'text-white'
        ]"
      >
        <span
          v-if="item.color"
          class="w-7 h-7 rounded-md flex items-center justify-center text-white font-bold text-sm shrink-0"
          :style="{ background: item.color }"
        >{{ item.label.charAt(0).toUpperCase() }}</span>
        <svg v-else-if="item.icon" class="w-5 h-5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="item.icon" />
        </svg>
        {{ item.label }}
      </NuxtLink>
    </template>
  </div>
</template>

<script setup lang="ts">
export interface BreadcrumbItem {
  label: string
  to: string
  icon?: string
  color?: string
}

defineProps<{
  items: BreadcrumbItem[]
}>()
</script>
