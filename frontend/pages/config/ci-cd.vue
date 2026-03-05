<template>
  <div>
    <div class="mb-6">
      <h2 class="text-lg font-semibold text-white">CI/CD Settings</h2>
      <p class="text-sm text-gray-400 mt-0.5">
        Configure how <code class="text-gray-300 bg-gray-800 px-1 rounded">act</code> runs your GitHub Actions workflows locally.
        This is the global default — individual organizations and projects can override it.
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
            The Docker image used inside each job container. Click to select the default image.
            Set <code class="text-gray-300 bg-gray-800 px-1 rounded">CiCd__ActImage</code> on the CI/CD client to override at the server level.
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

      <CiCdImageSelector v-model="selectedImage" />

      <div class="mt-4 flex items-center gap-3">
        <button
          class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
          @click="saveImageSelection"
        >
          {{ saved ? '✓ Saved' : 'Save Default' }}
        </button>
        <button
          v-if="selectedImage"
          class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm transition-colors"
          @click="clearImageSelection"
        >
          Reset to default
        </button>
        <span v-if="selectedImage" class="text-xs text-gray-500 font-mono truncate">{{ selectedImage }}</span>
        <span v-else class="text-xs text-gray-500">Using system default (act medium image)</span>
      </div>
    </div>

    <!-- actrc reference -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5 mb-6">
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

    <!-- Action Cache & Local Repositories -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5">
      <h3 class="font-medium text-white mb-1">Action Cache &amp; Offline Mode</h3>
      <p class="text-sm text-gray-400 mb-3">
        IssuePit supports caching cloned actions and rerouting private workflow repositories.
        Configure these settings at the organization or project level.
      </p>
      <div class="space-y-3">
        <div class="bg-gray-950 rounded-lg border border-gray-800 p-4">
          <p class="text-xs text-gray-500 mb-2 font-mono">Pre-cache &amp; offline mode</p>
          <pre class="text-xs text-gray-300 font-mono leading-relaxed">--action-cache-path ~/act-cache
--use-new-action-cache
--action-offline-mode</pre>
        </div>
        <div class="bg-gray-950 rounded-lg border border-gray-800 p-4">
          <p class="text-xs text-gray-500 mb-2 font-mono">Reroute private workflows to local path</p>
          <pre class="text-xs text-gray-300 font-mono leading-relaxed">--local-repository "myorg/private-actions@v1=/home/act/private-actions"</pre>
        </div>
      </div>
      <p class="text-xs text-gray-500 mt-3">
        Set <code class="text-gray-300 bg-gray-800 px-1 rounded">CiCd__ActionCachePath</code> on the CI/CD client to configure a system-wide action cache path.
        Per-organization and per-project overrides are available in the organization CI/CD settings.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
const STORAGE_KEY = 'cicd-global-runner-image'

const selectedImage = ref<string | null>(null)
const saved = ref(false)

onMounted(() => {
  if (import.meta.client) {
    selectedImage.value = localStorage.getItem(STORAGE_KEY) || null
  }
})

function saveImageSelection() {
  if (import.meta.client) {
    if (selectedImage.value) {
      localStorage.setItem(STORAGE_KEY, selectedImage.value)
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
    saved.value = true
    setTimeout(() => { saved.value = false }, 2000)
  }
}

function clearImageSelection() {
  selectedImage.value = null
  if (import.meta.client) {
    localStorage.removeItem(STORAGE_KEY)
    saved.value = true
    setTimeout(() => { saved.value = false }, 2000)
  }
}
</script>

