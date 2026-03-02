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
      <h3 class="font-medium text-white mb-1">Runner Image</h3>
      <p class="text-sm text-gray-400 mb-4">
        The Docker image used inside each job container. Set the
        <code class="text-gray-300 bg-gray-800 px-1 rounded">CiCd__ActImage</code> environment variable on the
        CI/CD client to override the default.
      </p>

      <div class="space-y-3">
        <div
          v-for="preset in imagePresets"
          :key="preset.id"
          class="rounded-lg border p-4 transition-colors"
          :class="preset.id === 'medium' ? 'border-brand-700 bg-brand-950/30' : 'border-gray-800 bg-gray-900/20'"
        >
          <div class="flex items-start gap-3">
            <div class="mt-0.5 text-lg">{{ preset.emoji }}</div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="font-medium text-white">{{ preset.label }}</span>
                <span
                  v-if="preset.id === 'medium'"
                  class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-900/60 text-brand-300 border border-brand-800"
                >
                  Default
                </span>
                <span class="text-xs text-gray-500">{{ preset.size }}</span>
              </div>
              <p class="text-sm text-gray-400 mt-0.5">{{ preset.description }}</p>
              <code class="text-xs text-gray-500 font-mono mt-1 block">{{ preset.image }}</code>
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
        <p class="text-xs text-gray-500 mb-2 font-mono">Example actrc (medium image)</p>
        <pre class="text-xs text-gray-300 font-mono leading-relaxed">-P ubuntu-latest=catthehacker/ubuntu:act-latest
-P ubuntu-24.04=catthehacker/ubuntu:act-latest
-P ubuntu-22.04=catthehacker/ubuntu:act-latest
-P ubuntu-20.04=catthehacker/ubuntu:act-latest</pre>
      </div>
      <p class="text-xs text-gray-500 mt-3">
        See the
        <a
          href="https://nektosact.com/usage/runners.html"
          target="_blank"
          rel="noopener noreferrer"
          class="text-brand-400 hover:text-brand-300"
        >act runner documentation</a>
        for a full list of available runner images.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
const imagePresets = [
  {
    id: 'large',
    emoji: '🏋️',
    label: 'Large',
    size: '~17 GB download / ~53 GB storage',
    description: 'Snapshots of GitHub-hosted runners. Highest compatibility but requires ~75 GB of free disk space.',
    image: 'catthehacker/ubuntu:full-latest',
  },
  {
    id: 'medium',
    emoji: '⚡',
    label: 'Medium',
    size: '~500 MB',
    description: 'Includes only the tools needed to bootstrap actions. Compatible with most workflows.',
    image: 'catthehacker/ubuntu:act-latest',
  },
  {
    id: 'micro',
    emoji: '🪶',
    label: 'Micro',
    size: '< 200 MB',
    description: 'Contains only Node.js to bootstrap actions. Does not work with all actions.',
    image: 'node:20-bookworm-slim',
  },
]
</script>
