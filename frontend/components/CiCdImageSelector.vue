<template>
  <div class="space-y-3">
    <div
      v-for="group in imageGroups"
      :key="group.id"
      class="rounded-lg border p-4 transition-colors cursor-pointer"
      :class="[
        selectedGroupId === group.id
          ? 'border-brand-500 bg-brand-950/40 ring-1 ring-brand-500/50'
          : group.isDefault && !selectedGroupId
            ? 'border-brand-700 bg-brand-950/30'
            : 'border-gray-800 bg-gray-900/20 hover:border-gray-700'
      ]"
      @click="selectGroup(group.id)"
    >
      <div class="flex items-start gap-3">
        <div class="mt-0.5 text-lg">{{ group.emoji }}</div>
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2 flex-wrap mb-1">
            <span class="font-medium text-white">{{ group.label }}</span>
            <span
              v-if="group.isDefault && !selectedGroupId"
              class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-900/60 text-brand-300 border border-brand-800"
            >
              Default
            </span>
            <span
              v-if="selectedGroupId === group.id"
              class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-700/80 text-white border border-brand-600"
            >
              Selected
            </span>
            <span v-if="group.size" class="text-xs text-gray-500">{{ group.size }}</span>
          </div>
          <p class="text-sm text-gray-400 mb-2">{{ group.description }}</p>
          <div class="flex flex-wrap gap-1.5">
            <code
              v-for="tag in group.tags"
              :key="tag"
              class="text-xs font-mono px-2 py-0.5 rounded border"
              :class="selectedGroupId === group.id && tag.endsWith(':act-latest')
                ? 'bg-brand-950/60 border-brand-800 text-brand-300'
                : group.isDefault && !selectedGroupId && tag.endsWith(':act-latest')
                  ? 'bg-brand-950/60 border-brand-800 text-brand-300'
                  : 'bg-gray-950/60 border-gray-700 text-gray-400'"
            >{{ tag }}</code>
          </div>
        </div>
        <!-- Selected indicator -->
        <div class="shrink-0 mt-0.5">
          <div
            v-if="selectedGroupId === group.id"
            class="w-5 h-5 rounded-full bg-brand-600 flex items-center justify-center"
          >
            <svg class="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <div v-else class="w-5 h-5 rounded-full border-2 border-gray-700" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// Image groups sourced from https://github.com/catthehacker/docker_images/blob/master/README.md
const imageGroups = [
  {
    id: 'full',
    emoji: '🏋️',
    label: 'Full (GitHub-hosted runner copy)',
    size: '~20 GB compressed / ~60 GB extracted',
    description: 'Weekly snapshot of GitHub-hosted runners — highest compatibility, requires ~75 GB of free disk space.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:full-latest',
      'ghcr.io/catthehacker/ubuntu:full-24.04',
      'ghcr.io/catthehacker/ubuntu:full-22.04',
    ],
  },
  {
    id: 'act',
    emoji: '⚡',
    label: 'Act (medium)',
    size: '~500 MB',
    description: 'Default act medium image. Includes only the tools needed to bootstrap actions. Compatible with most workflows.',
    isDefault: true,
    tags: [
      'ghcr.io/catthehacker/ubuntu:act-latest',
      'ghcr.io/catthehacker/ubuntu:act-24.04',
      'ghcr.io/catthehacker/ubuntu:act-22.04',
    ],
  },
  {
    id: 'runner',
    emoji: '👤',
    label: 'Runner',
    size: null,
    description: 'Same as act but runs as the `runner` user instead of `root`.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:runner-latest',
      'ghcr.io/catthehacker/ubuntu:runner-24.04',
      'ghcr.io/catthehacker/ubuntu:runner-22.04',
    ],
  },
  {
    id: 'js',
    emoji: '🟨',
    label: 'JS',
    size: null,
    description: 'Act base + JS tools: yarn, nvm, node v20/v24, pnpm, grunt, etc.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:js-latest',
      'ghcr.io/catthehacker/ubuntu:js-24.04',
      'ghcr.io/catthehacker/ubuntu:js-22.04',
    ],
  },
  {
    id: 'dotnet',
    emoji: '🟣',
    label: '.NET',
    size: null,
    description: 'Act base + .NET SDK and tools.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:dotnet-latest',
      'ghcr.io/catthehacker/ubuntu:dotnet-24.04',
      'ghcr.io/catthehacker/ubuntu:dotnet-22.04',
    ],
  },
  {
    id: 'go',
    emoji: '🐹',
    label: 'Go',
    size: null,
    description: 'Act base + Go toolchain.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:go-latest',
      'ghcr.io/catthehacker/ubuntu:go-24.04',
      'ghcr.io/catthehacker/ubuntu:go-22.04',
    ],
  },
  {
    id: 'rust',
    emoji: '🦀',
    label: 'Rust',
    size: null,
    description: 'Act base + Rust tools: rustfmt, clippy, cbindgen, etc.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:rust-latest',
      'ghcr.io/catthehacker/ubuntu:rust-24.04',
      'ghcr.io/catthehacker/ubuntu:rust-22.04',
    ],
  },
  {
    id: 'java-tools',
    emoji: '☕',
    label: 'Java',
    size: null,
    description: 'Act base + Java tools.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:java-tools-latest',
      'ghcr.io/catthehacker/ubuntu:java-tools-24.04',
      'ghcr.io/catthehacker/ubuntu:java-tools-22.04',
    ],
  },
  {
    id: 'gh',
    emoji: '🐙',
    label: 'GitHub CLI',
    size: null,
    description: 'Act base + GitHub CLI (gh).',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:gh-latest',
      'ghcr.io/catthehacker/ubuntu:gh-24.04',
      'ghcr.io/catthehacker/ubuntu:gh-22.04',
    ],
  },
  {
    id: 'pwsh',
    emoji: '🔷',
    label: 'PowerShell',
    size: null,
    description: 'Act base + PowerShell (pwsh) tools and modules.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:pwsh-latest',
      'ghcr.io/catthehacker/ubuntu:pwsh-24.04',
      'ghcr.io/catthehacker/ubuntu:pwsh-22.04',
    ],
  },
  {
    id: 'custom',
    emoji: '🔧',
    label: 'Custom',
    size: null,
    description: 'Act base + custom tools.',
    isDefault: false,
    tags: [
      'ghcr.io/catthehacker/ubuntu:custom-latest',
      'ghcr.io/catthehacker/ubuntu:custom-24.04',
      'ghcr.io/catthehacker/ubuntu:custom-22.04',
    ],
  },
]

const props = defineProps<{
  modelValue?: string | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
}>()

// Returns the image group id matching a stored tag value (exact match preferred,
// otherwise falls back to matching by image-type prefix before the version suffix).
function findGroupForTag(tag: string): typeof imageGroups[0] | undefined {
  // 1. Exact match
  const exact = imageGroups.find(g => g.tags.includes(tag))
  if (exact) return exact
  // 2. Prefix match: compare "ghcr.io/catthehacker/ubuntu:act" portion only
  const prefix = tag.replace(/-[^-:]+$/, '') // strip trailing version like "-22.04" or "-latest"
  return imageGroups.find(g => g.tags.some(t => t.replace(/-[^-:]+$/, '') === prefix))
}

// Derive selected group from the current model value
const selectedGroupId = computed(() => {
  if (!props.modelValue) return null
  return findGroupForTag(props.modelValue)?.id ?? null
})

function selectGroup(id: string) {
  const group = imageGroups.find(g => g.id === id)
  if (!group) return
  if (selectedGroupId.value === id) {
    // Deselect (revert to default/inherit)
    emit('update:modelValue', null)
    return
  }
  // Use the first tag (latest) as the representative image
  emit('update:modelValue', group.tags[0])
}
</script>
