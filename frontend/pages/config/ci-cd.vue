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

    <!-- Act Container Image -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5 mb-6">
      <div class="flex items-start justify-between gap-4 mb-4">
        <div>
          <h3 class="font-medium text-white mb-1">Act Container Image</h3>
          <p class="text-sm text-gray-400">
            The Docker image that provides the <code class="text-gray-300 bg-gray-800 px-1 rounded">act</code> binary and CI/CD tooling.
            Override at the server level with <code class="text-gray-300 bg-gray-800 px-1 rounded">CiCd__Docker__Image</code>.
            See the
            <a
              href="https://github.com/issuepit/issuepit/pkgs/container/issuepit-helper-act"
              target="_blank"
              rel="noopener noreferrer"
              class="text-brand-400 hover:text-brand-300"
            >issuepit-helper-act packages ↗</a>
            for available tags.
          </p>
        </div>
      </div>

      <div class="space-y-2">
        <div
          v-for="tag in actContainerTags"
          :key="tag.value"
          class="flex items-center gap-3 rounded-lg border p-3 cursor-pointer transition-colors"
          :class="selectedActImage === tag.value
            ? 'border-brand-500 bg-brand-950/40 ring-1 ring-brand-500/50'
            : !selectedActImage && tag.isDefault
              ? 'border-brand-700 bg-brand-950/30'
              : 'border-gray-800 bg-gray-900/20 hover:border-gray-700'"
          @click="selectedActImage = tag.value; customActImageActive = false"
        >
          <div
            class="w-4 h-4 rounded-full border-2 flex items-center justify-center shrink-0"
            :class="selectedActImage === tag.value || (!selectedActImage && tag.isDefault)
              ? 'border-brand-500 bg-brand-600'
              : 'border-gray-600'"
          >
            <div
              v-if="selectedActImage === tag.value || (!selectedActImage && tag.isDefault)"
              class="w-1.5 h-1.5 rounded-full bg-white"
            />
          </div>
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 flex-wrap">
              <code class="text-xs font-mono text-gray-300">{{ tag.value }}</code>
              <span v-if="tag.isDefault && !selectedActImage" class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-900/60 text-brand-300 border border-brand-800">Default</span>
              <span v-if="selectedActImage === tag.value" class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-700/80 text-white border border-brand-600">Selected</span>
            </div>
            <p class="text-xs text-gray-500 mt-0.5">{{ tag.description }}</p>
          </div>
        </div>

        <!-- Custom image option -->
        <div
          class="flex items-center gap-3 rounded-lg border p-3 cursor-pointer transition-colors"
          :class="customActImageActive
            ? 'border-brand-500 bg-brand-950/40 ring-1 ring-brand-500/50'
            : 'border-gray-800 bg-gray-900/20 hover:border-gray-700'"
          @click="activateCustomActImage"
        >
          <div
            class="w-4 h-4 rounded-full border-2 flex items-center justify-center shrink-0"
            :class="customActImageActive ? 'border-brand-500 bg-brand-600' : 'border-gray-600'"
          >
            <div v-if="customActImageActive" class="w-1.5 h-1.5 rounded-full bg-white" />
          </div>
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-white">Custom tag</span>
              <span v-if="customActImageActive" class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-700/80 text-white border border-brand-600">Selected</span>
            </div>
            <p class="text-xs text-gray-500 mt-0.5">Enter any tag or full image reference. Version tags follow the format <code class="bg-gray-900 px-1 rounded">1.2.3-dotnet10-node24</code>.</p>
            <div v-if="customActImageActive" class="mt-2" @click.stop>
              <input
                ref="customActInputRef"
                v-model="customActImageInput"
                type="text"
                placeholder="e.g. 1.2.0-dotnet10-node24 or ghcr.io/issuepit/issuepit-helper-act:sha-abc1234"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white font-mono placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
                @input="onCustomActImageInput"
              />
            </div>
          </div>
        </div>
      </div>

      <div class="mt-4 flex items-center gap-3">
        <button
          class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
          @click="saveActImageSelection"
        >
          {{ savedActImage ? '✓ Saved' : 'Save Default' }}
        </button>
        <button
          v-if="selectedActImage"
          class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm transition-colors"
          @click="clearActImageSelection"
        >
          Reset to default
        </button>
        <span v-if="selectedActImage" class="text-xs text-gray-500 font-mono truncate">{{ selectedActImage }}</span>
        <span v-else class="text-xs text-gray-500">Using system default (<code class="text-gray-400 bg-gray-800 px-1 rounded">latest</code>)</span>
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
const ACT_CONTAINER_STORAGE_KEY = 'cicd-act-container-image'

const selectedImage = ref<string | null>(null)
const saved = ref(false)

onMounted(() => {
  if (import.meta.client) {
    selectedImage.value = localStorage.getItem(STORAGE_KEY) || null
    selectedActImage.value = localStorage.getItem(ACT_CONTAINER_STORAGE_KEY) || null
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

// Act Container Image
const actContainerTags = [
  {
    value: 'ghcr.io/issuepit/issuepit-helper-act:latest',
    description: 'Most recent stable release — recommended for production use.',
    isDefault: true,
  },
  {
    value: 'ghcr.io/issuepit/issuepit-helper-act:main-dotnet10-node24',
    description: 'Latest build from the main branch — may include unreleased changes.',
    isDefault: false,
  },
]

const selectedActImage = ref<string | null>(null)
const savedActImage = ref(false)
const customActImageActive = ref(false)
const customActImageInput = ref('')
const customActInputRef = ref<HTMLInputElement | null>(null)

const isKnownActImage = computed(() =>
  actContainerTags.some(t => t.value === selectedActImage.value),
)

watch(
  () => selectedActImage.value,
  (val) => {
    if (val && !actContainerTags.some(t => t.value === val)) {
      customActImageInput.value = val
      customActImageActive.value = true
    } else {
      if (!customActImageActive.value) customActImageInput.value = ''
    }
  },
  { immediate: true },
)

function activateCustomActImage() {
  customActImageActive.value = true
  // Clear known-tag selection when switching to custom
  if (isKnownActImage.value) selectedActImage.value = null
  if (customActImageInput.value) {
    selectedActImage.value = customActImageInput.value
  }
  nextTick(() => {
    customActInputRef.value?.focus()
  })
}

function onCustomActImageInput() {
  selectedActImage.value = customActImageInput.value || null
}

function saveActImageSelection() {
  if (import.meta.client) {
    if (selectedActImage.value) {
      localStorage.setItem(ACT_CONTAINER_STORAGE_KEY, selectedActImage.value)
    } else {
      localStorage.removeItem(ACT_CONTAINER_STORAGE_KEY)
    }
    savedActImage.value = true
    setTimeout(() => { savedActImage.value = false }, 2000)
  }
}

function clearActImageSelection() {
  selectedActImage.value = null
  customActImageActive.value = false
  customActImageInput.value = ''
  if (import.meta.client) {
    localStorage.removeItem(ACT_CONTAINER_STORAGE_KEY)
    savedActImage.value = true
    setTimeout(() => { savedActImage.value = false }, 2000)
  }
}
</script>

