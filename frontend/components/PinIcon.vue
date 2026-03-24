<template>
  <svg
    :class="['shrink-0', sizeClass]"
    fill="currentColor"
    viewBox="0 0 24 24"
    aria-hidden="true"
  >
    <path
      d="M16 12V4h1V2H7v2h1v8l-2 2v2h5.2v6h1.6v-6H18v-2l-2-2z"
      :opacity="pinned ? 1 : 0.4"
    />
  </svg>
</template>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  pinned?: boolean
  size?: 'sm' | 'md'
}>(), {
  pinned: false,
  size: 'md',
})

const sizeClass = computed(() => props.size === 'sm' ? 'w-3 h-3' : 'w-4 h-4')
</script>
