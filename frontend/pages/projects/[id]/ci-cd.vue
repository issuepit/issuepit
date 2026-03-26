<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 mb-4">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject.name, to: `/projects/${id}`, color: projectsStore.currentProject.color || '#4c6ef5' },
          { label: 'CI/CD', to: `/projects/${id}/ci-cd`, icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
        ]" />
      </div>

      <!-- Tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <NuxtLink
          :to="`/projects/${id}/settings`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/settings`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >Settings</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/ci-cd`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/ci-cd`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >CI/CD</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/members`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/members`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >Members</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/github-sync`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/github-sync`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >GitHub Sync</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/jira-sync`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/jira-sync`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >Jira Sync</NuxtLink>
      </div>

      <div class="space-y-6 max-w-2xl">
        <!-- Runner Image -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Image</h2>
          <p class="text-sm text-gray-500 mb-4">
            Override the Docker runner image for this project. Leave unset to inherit from the organization or global default.
          </p>
          <template v-if="isProjectFieldImported('actRunnerImage')">
            <p class="text-sm font-mono text-gray-300">{{ ciCdForm.actRunnerImage || '(not set)' }}</p>
            <ImportedBadge :source-file="projectFieldSourceFile('actRunnerImage')" />
          </template>
          <template v-else>
            <CiCdImageSelector v-model="ciCdForm.actRunnerImage" :inherited-value="inheritedRunnerImage" />
            <p v-if="!ciCdForm.actRunnerImage" class="text-xs text-gray-500 mt-3">
              <template v-if="inheritedRunnerImage">
                No override set — inheriting: <code class="font-mono text-gray-300 bg-gray-800 px-1 rounded">{{ inheritedRunnerImage }}</code>
              </template>
              <template v-else>
                No override set — inheriting from org or global default.
              </template>
            </p>
            <p v-else class="text-xs text-gray-500 mt-3 font-mono">{{ ciCdForm.actRunnerImage }}</p>
          </template>
        </div>

        <!-- Runner Options -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Options</h2>
          <p class="text-sm text-gray-500 mb-4">Configure CI/CD runner behaviour for this project</p>
          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">
                  Mount repository in Docker
                  <ImportedBadge v-if="isProjectFieldImported('mountRepositoryInDocker')" :source-file="projectFieldSourceFile('mountRepositoryInDocker')" />
                </label>
                <p class="text-xs text-gray-500 mt-0.5">Bind the workspace directory into the runner container</p>
              </div>
              <button
                type="button"
                :disabled="isProjectFieldImported('mountRepositoryInDocker')"
                :class="ciCdForm.mountRepositoryInDocker ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900 disabled:opacity-50 disabled:cursor-not-allowed"
                @click="!isProjectFieldImported('mountRepositoryInDocker') && (ciCdForm.mountRepositoryInDocker = !ciCdForm.mountRepositoryInDocker)">
                <span
                  :class="ciCdForm.mountRepositoryInDocker ? 'translate-x-6' : 'translate-x-1'"
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
              </button>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Max concurrent runners
                <span class="text-gray-500 font-normal">(0 = unlimited)</span>
                <ImportedBadge v-if="isProjectFieldImported('maxConcurrentRunners')" :source-file="projectFieldSourceFile('maxConcurrentRunners')" />
              </label>
              <input v-model.number="ciCdForm.maxConcurrentRunners" type="number" min="0"
                :disabled="isProjectFieldImported('maxConcurrentRunners')"
                class="w-40 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Concurrent jobs per run
                <span class="text-gray-500 font-normal">(0 = unlimited, blank = inherit from org / default 4)</span>
                <ImportedBadge v-if="isProjectFieldImported('concurrentJobs')" :source-file="projectFieldSourceFile('concurrentJobs')" />
              </label>
              <input v-model.number="ciCdForm.concurrentJobs" type="number" min="0" placeholder="inherit (org or default 4)"
                :disabled="isProjectFieldImported('concurrentJobs')"
                class="w-40 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed" />
              <p class="text-xs text-gray-500 mt-1">Overrides the organization setting for <code class="bg-gray-800 px-1 rounded">--concurrent-jobs</code>.</p>
            </div>
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">Require run approval</label>
                <p class="text-xs text-gray-500 mt-0.5">Hold auto-triggered runs (git push, agent) for manual approval. User-triggered runs (manual trigger, retry) always bypass approval.</p>
              </div>
              <button
                type="button"
                :class="ciCdForm.requiresRunApproval ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900"
                @click="ciCdForm.requiresRunApproval = !ciCdForm.requiresRunApproval">
                <span
                  :class="ciCdForm.requiresRunApproval ? 'translate-x-6' : 'translate-x-1'"
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
              </button>
            </div>
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">Unwrap single-file artifacts</label>
                <p class="text-xs text-gray-500 mt-0.5">When an artifact contains exactly one supported file (PDF, PNG), download it directly instead of as a ZIP archive.</p>
              </div>
              <button
                type="button"
                :class="ciCdForm.unwrapSingleFileArtifacts ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900"
                @click="ciCdForm.unwrapSingleFileArtifacts = !ciCdForm.unwrapSingleFileArtifacts">
                <span
                  :class="ciCdForm.unwrapSingleFileArtifacts ? 'translate-x-6' : 'translate-x-1'"
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
              </button>
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
                <ImportedBadge v-if="isProjectFieldImported('actEnv')" :source-file="projectFieldSourceFile('actEnv')" />
              </label>
              <textarea
                v-model="ciCdForm.actEnv"
                rows="4"
                placeholder="MY_VAR=my_value&#10;NODE_ENV=test"
                :disabled="isProjectFieldImported('actEnv')"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Secrets
                <span class="text-gray-500 font-normal">(--secret KEY=VALUE)</span>
                <ImportedBadge v-if="isProjectFieldImported('actSecrets')" :source-file="projectFieldSourceFile('actSecrets')" />
              </label>
              <textarea
                v-model="ciCdForm.actSecrets"
                rows="4"
                placeholder="MY_SECRET=value&#10;API_KEY=key"
                :disabled="isProjectFieldImported('actSecrets')"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y disabled:opacity-50 disabled:cursor-not-allowed"
              />
              <p class="text-xs text-gray-500 mt-1">Secret values are stored as plain text — avoid committing sensitive credentials that can be rotated.</p>
            </div>
          </div>
        </div>

        <!-- Action Cache & Offline Mode -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Action Cache</h2>
          <p class="text-sm text-gray-500 mb-4">
            Configure action and repository caching to avoid repeated network downloads.
            Overrides the organization setting. Leave blank to inherit from the organization.
          </p>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Action cache path
                <span class="text-gray-500 font-normal">(--action-cache-path)</span>
                <ImportedBadge v-if="isProjectFieldImported('actionCachePath')" :source-file="projectFieldSourceFile('actionCachePath')" />
              </label>
              <input
                v-model="ciCdForm.actionCachePath"
                type="text"
                placeholder="inherit from org (default: /var/lib/issuepit-action-cache)"
                :disabled="isProjectFieldImported('actionCachePath')"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
              <p class="text-xs text-gray-500 mt-1">Host directory for caching cloned actions. Leave blank to inherit from organization.</p>
            </div>
            <div class="flex items-center gap-3">
              <input
                id="projectUseNewActionCache"
                v-model="ciCdForm.useNewActionCache"
                type="checkbox"
                :disabled="isProjectFieldImported('useNewActionCache')"
                class="w-4 h-4 rounded bg-gray-800 border-gray-600 text-brand-500 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
              <label for="projectUseNewActionCache" class="text-sm text-gray-300">
                Use new action cache
                <span class="text-gray-500 font-normal">(--use-new-action-cache)</span>
                <ImportedBadge v-if="isProjectFieldImported('useNewActionCache')" :source-file="projectFieldSourceFile('useNewActionCache')" />
              </label>
            </div>
            <div class="flex items-center gap-3">
              <input
                id="projectActionOfflineMode"
                v-model="ciCdForm.actionOfflineMode"
                type="checkbox"
                :disabled="isProjectFieldImported('actionOfflineMode')"
                class="w-4 h-4 rounded bg-gray-800 border-gray-600 text-brand-500 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
              <label for="projectActionOfflineMode" class="text-sm text-gray-300">
                Offline mode — use only cached actions, no network downloads
                <span class="text-gray-500 font-normal">(--action-offline-mode)</span>
                <ImportedBadge v-if="isProjectFieldImported('actionOfflineMode')" :source-file="projectFieldSourceFile('actionOfflineMode')" />
              </label>
            </div>
          </div>
        </div>

        <!-- Local Repository Rerouting -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Local Repository Mappings</h2>
          <p class="text-sm text-gray-500 mb-4">
            Map remote workflow/action repositories to local paths using
            <code class="text-gray-300 bg-gray-800 px-1 rounded">--local-repository</code>.
            Useful for private or internal reusable workflows. One mapping per line:
            <code class="text-gray-300 bg-gray-800 px-1 rounded">owner/repo@ref=/local/path</code>.
            Overrides the organization setting.
          </p>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">
              Repository mappings
              <ImportedBadge v-if="isProjectFieldImported('localRepositories')" :source-file="projectFieldSourceFile('localRepositories')" />
            </label>
            <textarea
              v-model="ciCdForm.localRepositories"
              rows="4"
              :placeholder="`myorg/private-actions@v1=/home/act/private-actions\nmyorg/shared-workflows@main=/home/act/workflows`"
              :disabled="isProjectFieldImported('localRepositories')"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y disabled:opacity-50 disabled:cursor-not-allowed"
            />
            <p class="text-xs text-gray-500 mt-1">
              Each line is passed as a separate
              <code class="bg-gray-800 px-1 rounded">--local-repository</code> argument to act.
              Paths must be accessible inside the act runner container.
            </p>
          </div>
        </div>

        <!-- Skip Steps -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">
            Skip Steps
            <ImportedBadge v-if="isProjectFieldImported('skipSteps')" :source-file="projectFieldSourceFile('skipSteps')" />
          </h2>
          <p class="text-sm text-gray-500 mb-4">
            Skip specific workflow steps on every run without modifying the workflow file.
            Useful to disable push, deploy, or notification steps in non-production environments.
            Overrides the organization setting.
          </p>
          <SkipStepsEditor v-model="ciCdForm.skipSteps" :project-id="id" :disabled="isProjectFieldImported('skipSteps')" />
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
import { useOrgsStore } from '~/stores/orgs'

const route = useRoute()
const id = route.params.id as string
const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()

const ciCdForm = reactive({
  actRunnerImage: null as string | null,
  mountRepositoryInDocker: true,
  maxConcurrentRunners: 0,
  concurrentJobs: null as number | null,
  actEnv: '',
  actSecrets: '',
  actionCachePath: '' as string,
  useNewActionCache: false as boolean | null,
  actionOfflineMode: false as boolean | null,
  localRepositories: '' as string,
  skipSteps: '' as string,
  requiresRunApproval: false,
  unwrapSingleFileArtifacts: false,
})

const saving = ref(false)
const saveError = ref<string | null>(null)
const savedOk = ref(false)

// --- Config field source helpers ---
function isProjectFieldImported(fieldName: string): boolean {
  return !!projectsStore.currentProject?.configFieldSources?.[fieldName]
}
function projectFieldSourceFile(fieldName: string): string {
  return projectsStore.currentProject?.configFieldSources?.[fieldName] ?? ''
}

// The runner image inherited from the org (shown when no project-level override is set).
const inheritedRunnerImage = computed<string | null>(() => {
  const p = projectsStore.currentProject
  if (!p) return null
  const org = orgsStore.orgs.find(o => o.id === p.orgId)
  return org?.actRunnerImage ?? null
})

onMounted(async () => {
  const fetchOrgIfNeeded = orgsStore.orgs.length === 0 ? orgsStore.fetchOrgs() : Promise.resolve()
  await Promise.all([
    projectsStore.fetchProject(id),
    fetchOrgIfNeeded,
  ])
  const p = projectsStore.currentProject
  if (p) {
    ciCdForm.actRunnerImage = p.actRunnerImage ?? null
    ciCdForm.mountRepositoryInDocker = p.mountRepositoryInDocker
    ciCdForm.maxConcurrentRunners = p.maxConcurrentRunners ?? 0
    ciCdForm.concurrentJobs = p.concurrentJobs ?? null
    ciCdForm.actEnv = p.actEnv || ''
    ciCdForm.actSecrets = p.actSecrets || ''
    ciCdForm.actionCachePath = p.actionCachePath || ''
    ciCdForm.useNewActionCache = p.useNewActionCache ?? null
    ciCdForm.actionOfflineMode = p.actionOfflineMode ?? null
    ciCdForm.localRepositories = p.localRepositories || ''
    ciCdForm.skipSteps = p.skipSteps || ''
    ciCdForm.requiresRunApproval = p.requiresRunApproval ?? false
    ciCdForm.unwrapSingleFileArtifacts = p.unwrapSingleFileArtifacts ?? false
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
      concurrentJobs: ciCdForm.concurrentJobs,
      actEnv: ciCdForm.actEnv || undefined,
      actSecrets: ciCdForm.actSecrets || undefined,
      actionCachePath: ciCdForm.actionCachePath || null,
      useNewActionCache: ciCdForm.useNewActionCache,
      actionOfflineMode: ciCdForm.actionOfflineMode,
      localRepositories: ciCdForm.localRepositories || null,
      skipSteps: ciCdForm.skipSteps || null,
      requiresRunApproval: ciCdForm.requiresRunApproval,
      unwrapSingleFileArtifacts: ciCdForm.unwrapSingleFileArtifacts,
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
