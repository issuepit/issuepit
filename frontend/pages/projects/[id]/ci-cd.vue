<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M4 6h16M4 10h16M4 14h16M4 18h16" />
        </svg>
        <h1 class="text-xl font-bold text-white">CI/CD Config — {{ projectsStore.currentProject.name }}</h1>
      </div>

      <div class="space-y-6 max-w-2xl">
        <!-- Runner Image -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Image</h2>
          <p class="text-sm text-gray-500 mb-4">
            Override the Docker runner image for this project. Leave unset to inherit from the organization or global default.
          </p>
          <CiCdImageSelector v-model="ciCdForm.actRunnerImage" />
          <p v-if="!ciCdForm.actRunnerImage" class="text-xs text-gray-500 mt-3">
            No override set — inheriting from org or global default.
          </p>
          <p v-else class="text-xs text-gray-500 mt-3 font-mono">{{ ciCdForm.actRunnerImage }}</p>
        </div>

        <!-- Runner Options -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Options</h2>
          <p class="text-sm text-gray-500 mb-4">Configure CI/CD runner behaviour for this project</p>
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">Mount repository in Docker</label>
                <p class="text-xs text-gray-500 mt-0.5">Bind the workspace directory into the runner container</p>
              </div>
              <button
                type="button"
                :class="ciCdForm.mountRepositoryInDocker ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900"
                @click="ciCdForm.mountRepositoryInDocker = !ciCdForm.mountRepositoryInDocker">
                <span
                  :class="ciCdForm.mountRepositoryInDocker ? 'translate-x-6' : 'translate-x-1'"
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
              </button>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Max concurrent runners
                <span class="text-gray-500 font-normal">(0 = unlimited)</span>
              </label>
              <input v-model.number="ciCdForm.maxConcurrentRunners" type="number" min="0"
                class="w-40 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
          </div>
        </div>

        <!-- Environment & Secrets -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Environment &amp; Secrets</h2>
          <p class="text-sm text-gray-500 mb-4">
            Values passed as <code class="text-gray-300 bg-gray-800 px-1 rounded">--env</code> and
            <code class="text-gray-300 bg-gray-800 px-1 rounded">--secret</code> arguments to
            <code class="text-gray-300 bg-gray-800 px-1 rounded">act</code> on every run.
            One <code class="text-gray-300 bg-gray-800 px-1 rounded">KEY=VALUE</code> per line.
          </p>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Environment variables
                <span class="text-gray-500 font-normal">(--env KEY=VALUE)</span>
              </label>
              <textarea
                v-model="ciCdForm.actEnv"
                rows="4"
                placeholder="MY_VAR=my_value&#10;NODE_ENV=test"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Secrets
                <span class="text-gray-500 font-normal">(--secret KEY=VALUE)</span>
              </label>
              <textarea
                v-model="ciCdForm.actSecrets"
                rows="4"
                placeholder="MY_SECRET=value&#10;API_KEY=key"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y"
              />
              <p class="text-xs text-gray-500 mt-1">Secret values are stored as plain text — avoid committing sensitive credentials that can be rotated.</p>
            </div>
          </div>
        </div>

        <!-- Save button -->
        <div class="flex items-center gap-4">
          <button
            :disabled="saving"
            class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
            @click="save"
          >
            {{ saving ? 'Saving…' : 'Save CI/CD Config' }}
          </button>
          <p v-if="saveError" class="text-red-400 text-sm">{{ saveError }}</p>
          <p v-if="savedOk" class="text-green-400 text-sm">Saved successfully</p>
        </div>
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">Project not found</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const projectsStore = useProjectsStore()

const ciCdForm = reactive({
  actRunnerImage: null as string | null,
  mountRepositoryInDocker: true,
  maxConcurrentRunners: 0,
  actEnv: '',
  actSecrets: '',
})

const saving = ref(false)
const saveError = ref<string | null>(null)
const savedOk = ref(false)

onMounted(async () => {
  await projectsStore.fetchProject(id)
  const p = projectsStore.currentProject
  if (p) {
    ciCdForm.actRunnerImage = p.actRunnerImage ?? null
    ciCdForm.mountRepositoryInDocker = p.mountRepositoryInDocker
    ciCdForm.maxConcurrentRunners = p.maxConcurrentRunners ?? 0
    ciCdForm.actEnv = p.actEnv || ''
    ciCdForm.actSecrets = p.actSecrets || ''
  }
})

async function save() {
  const p = projectsStore.currentProject
  if (!p) return
  saving.value = true
  saveError.value = null
  savedOk.value = false
  try {
    await projectsStore.updateProject(id, {
      ...p,
      actRunnerImage: ciCdForm.actRunnerImage || undefined,
      mountRepositoryInDocker: ciCdForm.mountRepositoryInDocker,
      maxConcurrentRunners: ciCdForm.maxConcurrentRunners,
      actEnv: ciCdForm.actEnv || undefined,
      actSecrets: ciCdForm.actSecrets || undefined,
    })
    savedOk.value = true
    setTimeout(() => { savedOk.value = false }, 3000)
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    saving.value = false
  }
}
</script>
