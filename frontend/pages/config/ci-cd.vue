<template>
  <div>
    <div class="mb-6">
      <h2 class="text-lg font-semibold text-white">CI/CD Settings</h2>
      <p class="text-sm text-gray-400 mt-0.5">
        Configure how <code class="text-gray-300 bg-gray-800 px-1 rounded">act</code> runs your GitHub Actions workflows locally.
        <a
          href="https://nektosact.com/introduction.html"
          target="_blank"
          rel="noopener noreferrer"
          class="text-brand-400 hover:text-brand-300 ml-1"
        >act docs ↗</a>
      </p>
    </div>

    <!-- Runner Image -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5 mb-6">
      <div class="flex items-start justify-between gap-4 mb-4">
        <div>
          <h3 class="font-medium text-white mb-1">Runner Images</h3>
          <p class="text-sm text-gray-400">
            The Docker image used inside each job container. Set
            <code class="text-gray-300 bg-gray-800 px-1 rounded">CiCd__ActImage</code> on the CI/CD client to override.
            <!-- https://github.com/catthehacker/docker_images/blob/master/README.md -->
            See the
            <a
              href="https://github.com/catthehacker/docker_images/blob/master/README.md"
              target="_blank"
              rel="noopener noreferrer"
              class="text-brand-400 hover:text-brand-300"
            >catthehacker/docker_images README ↗</a>
            for the full list of images and update schedule.
          </p>
        </div>
      </div>

      <div class="space-y-4">
        <div
          v-for="group in imageGroups"
          :key="group.id"
          class="rounded-lg border p-4 transition-colors"
          :class="group.isDefault ? 'border-brand-700 bg-brand-950/30' : 'border-gray-800 bg-gray-900/20'"
        >
          <div class="flex items-start gap-3">
            <div class="mt-0.5 text-lg">{{ group.emoji }}</div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap mb-1">
                <span class="font-medium text-white">{{ group.label }}</span>
                <span
                  v-if="group.isDefault"
                  class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-900/60 text-brand-300 border border-brand-800"
                >
                  Default
                </span>
                <span v-if="group.size" class="text-xs text-gray-500">{{ group.size }}</span>
              </div>
              <p class="text-sm text-gray-400 mb-2">{{ group.description }}</p>
              <div class="flex flex-wrap gap-1.5">
                <code
                  v-for="tag in group.tags"
                  :key="tag"
                  class="text-xs font-mono px-2 py-0.5 rounded border"
                  :class="group.isDefault && tag.endsWith(':act-latest') ? 'bg-brand-950/60 border-brand-800 text-brand-300' : 'bg-gray-950/60 border-gray-700 text-gray-400'"
                >{{ tag }}</code>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- actrc reference -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5">
      <h3 class="font-medium text-white mb-1">actrc Configuration</h3>
      <p class="text-sm text-gray-400 mb-3">
        IssuePit automatically injects <code class="text-gray-300 bg-gray-800 px-1 rounded">/root/.config/act/actrc</code>
        on each runner start so you don't need to configure it manually.
        You can still provide a custom file inside your Docker image — it will be respected if it already exists.
      </p>
      <div class="bg-gray-950 rounded-lg border border-gray-800 p-4">
        <p class="text-xs text-gray-500 mb-2 font-mono">Example actrc (medium image, default)</p>
        <pre class="text-xs text-gray-300 font-mono leading-relaxed">-P ubuntu-latest=ghcr.io/catthehacker/ubuntu:act-latest
-P ubuntu-24.04=ghcr.io/catthehacker/ubuntu:act-latest
-P ubuntu-22.04=ghcr.io/catthehacker/ubuntu:act-latest
-P ubuntu-20.04=ghcr.io/catthehacker/ubuntu:act-latest</pre>
      </div>
      <p class="text-xs text-gray-500 mt-3">
        See the
        <a
          href="https://nektosact.com/usage/runners.html"
          target="_blank"
          rel="noopener noreferrer"
          class="text-brand-400 hover:text-brand-300"
        >act runner documentation</a>
        for configuration options and
        <a
          href="https://github.com/nektos/act/blob/master/cmd/root.go"
          target="_blank"
          rel="noopener noreferrer"
          class="text-brand-400 hover:text-brand-300 ml-1"
        >act CLI reference (root.go) ↗</a>
        for a full list of supported flags.
      </p>
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
</script>
