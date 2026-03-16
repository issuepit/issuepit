<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 mb-4">
        <PageBreadcrumb
:items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject.name, to: `/projects/${id}`, color: projectsStore.currentProject.color || '#4c6ef5' },
          { label: 'GitHub Sync', to: `/projects/${id}/github-sync`, icon: 'M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207' },
        ]" />
      </div>

      <!-- Settings nav tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <NuxtLink
          :to="`/projects/${id}/settings`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/settings` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >Settings</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/ci-cd`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/ci-cd` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >CI/CD</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/members`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/members` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >Members</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/github-sync`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/github-sync` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >GitHub Sync</NuxtLink>
      </div>

      <!-- Inner tabs -->
      <div class="flex gap-1 mb-6">
        <button
          v-for="tab in tabs"
          :key="tab"
          :class="['px-4 py-1.5 text-sm font-medium rounded-lg transition-colors', activeTab === tab ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-400 hover:text-gray-200']"
          @click="activeTab = tab"
        >
          {{ tab }}
          <span
            v-if="tab === 'Conflicts' && syncStore.conflicts.length > 0"
            class="ml-1.5 text-xs bg-orange-600 text-white px-1.5 py-0.5 rounded-full"
          >{{ syncStore.conflicts.length }}</span>
        </button>
      </div>

      <!-- ── Configuration ───────────────────────────────────────────────── -->
      <div v-if="activeTab === 'Configuration'" class="space-y-6 max-w-2xl">
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">GitHub Sync Configuration</h2>
          <p class="text-sm text-gray-500 mb-4">
            Configure how this project synchronises issues with a GitHub repository.
            A linked
            <NuxtLink to="/config/github-identities" class="text-brand-400 hover:text-brand-300">GitHub identity</NuxtLink>
            (PAT) is required.
          </p>

          <form class="space-y-4" @submit.prevent="saveConfig">
            <!-- GitHub Identity -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">GitHub Identity (PAT)</label>
              <select
v-model="form.gitHubIdentityId"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="">— None —</option>
                <option v-for="identity in identitiesStore.identities" :key="identity.id" :value="identity.id">
                  {{ identity.name || identity.gitHubUsername }}
                </option>
              </select>
            </div>

            <!-- GitHub Repo -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">GitHub Repository</label>
              <input
v-model="form.gitHubRepo" type="text" placeholder="owner/repo or https://github.com/owner/repo"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" >
              <p class="text-xs text-gray-600 mt-1">Format: <span class="font-mono">owner/repo</span> or <span class="font-mono">https://github.com/owner/repo</span></p>
            </div>

            <!-- Sync Mode -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Sync Mode</label>
              <div class="space-y-2">
                <label
                  v-for="(desc, mode) in syncModeOptions"
                  :key="mode"
                  :class="['flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors',
                    form.syncMode === Number(mode)
                      ? 'border-brand-500 bg-brand-500/10'
                      : 'border-gray-700 hover:border-gray-600'
                  ]"
                >
                  <input
                    v-model.number="form.syncMode"
                    type="radio"
                    :value="Number(mode)"
                    class="mt-0.5 accent-brand-500"
                  >
                  <div>
                    <p class="text-sm font-medium text-gray-200">{{ GitHubSyncModeLabels[Number(mode) as GitHubSyncMode] }}</p>
                    <p class="text-xs text-gray-500 mt-0.5">{{ desc }}</p>
                  </div>
                </label>
              </div>
            </div>

            <!-- Trigger Mode -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Trigger Mode</label>
              <select
v-model.number="form.triggerMode"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option :value="GitHubSyncTriggerMode.Off">Off — sync disabled</option>
                <option :value="GitHubSyncTriggerMode.Manual">Manual — trigger from this page only</option>
                <option :value="GitHubSyncTriggerMode.Auto">Auto — sync runs on a schedule</option>
              </select>
            </div>

            <p v-if="saveError" class="text-red-400 text-sm">{{ saveError }}</p>
            <p v-if="saveSuccess" class="text-green-400 text-sm">Configuration saved.</p>

            <div class="flex gap-3 pt-1">
              <button
type="submit" :disabled="syncStore.loading"
                class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
                {{ syncStore.loading ? 'Saving…' : 'Save Configuration' }}
              </button>
              <button
                v-if="form.triggerMode !== GitHubSyncTriggerMode.Off && form.syncMode !== GitHubSyncMode.CreateOnGitHub"
                type="button"
                :disabled="triggering"
                class="bg-gray-700 hover:bg-gray-600 disabled:opacity-50 text-gray-300 text-sm font-medium px-4 py-2 rounded-lg transition-colors"
                @click="triggerSync"
              >
                {{ triggering ? 'Triggered…' : 'Trigger Sync Now' }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- ── Sync Runs (Audit Log) ───────────────────────────────────────── -->
      <div v-else-if="activeTab === 'Sync Runs'">
        <ScheduledTaskRuns
          ref="runsComponent"
          :runs="syncStore.runs"
          :loading="syncStore.loading"
          :triggering="triggering"
          trigger-label="Trigger Sync"
          :fetch-run-detail="(runId) => syncStore.fetchRun(id, runId)"
          @trigger="triggerSync"
          @open-run="(runId) => runsComponent?.openRun(runId)"
        />
      </div>

      <!-- ── Conflicts ───────────────────────────────────────────────────── -->
      <div v-else-if="activeTab === 'Conflicts'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-sm text-gray-400">
            Issues that exist in both systems but have divergent content.
          </p>
          <button
            :disabled="syncStore.conflictsLoading"
            class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors"
            @click="loadConflicts"
          >
            {{ syncStore.conflictsLoading ? 'Loading…' : 'Refresh' }}
          </button>
        </div>

        <div v-if="syncStore.conflictsLoading" class="flex items-center justify-center py-16">
          <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="syncStore.conflicts.length === 0" class="py-16 text-center">
          <p class="text-green-400 text-sm">✓ No conflicts — all linked issues are in sync.</p>
        </div>

        <div v-else class="space-y-4">
          <div
v-for="conflict in syncStore.conflicts" :key="conflict.issueId"
            class="bg-gray-900 border border-orange-800/40 rounded-xl p-5">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <span class="text-xs bg-orange-800/40 text-orange-300 px-2 py-0.5 rounded-full font-medium">Conflict</span>
                <span class="text-sm font-semibold text-white">
                  IssuePit #{{ conflict.issueNumber }} ↔ GitHub #{{ conflict.gitHubIssueNumber }}
                </span>
              </div>
              <a
:href="conflict.gitHubUrl" target="_blank" rel="noopener noreferrer"
                class="text-xs text-brand-400 hover:text-brand-300 flex items-center gap-1">
                View on GitHub →
              </a>
            </div>

            <div v-if="conflict.titleDiffers" class="mb-3">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Title</p>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <p class="text-xs text-gray-600 mb-0.5">IssuePit</p>
                  <p class="text-sm text-gray-300 bg-gray-800 rounded p-2">{{ conflict.localTitle }}</p>
                </div>
                <div>
                  <p class="text-xs text-gray-600 mb-0.5">GitHub</p>
                  <p class="text-sm text-gray-300 bg-gray-800 rounded p-2">{{ conflict.gitHubTitle }}</p>
                </div>
              </div>
            </div>

            <div v-if="conflict.bodyDiffers">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Body</p>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <p class="text-xs text-gray-600 mb-0.5">IssuePit</p>
                  <p class="text-xs text-gray-400 bg-gray-800 rounded p-2 font-mono max-h-24 overflow-y-auto whitespace-pre-wrap">{{ conflict.localBody || '(empty)' }}</p>
                </div>
                <div>
                  <p class="text-xs text-gray-600 mb-0.5">GitHub</p>
                  <p class="text-xs text-gray-400 bg-gray-800 rounded p-2 font-mono max-h-24 overflow-y-auto whitespace-pre-wrap">{{ conflict.gitHubBody || '(empty)' }}</p>
                </div>
              </div>
            </div>

            <div class="flex gap-2 mt-3">
              <NuxtLink
:to="`/projects/${id}/issues/${conflict.issueId}`"
                class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
                Open in IssuePit →
              </NuxtLink>
            </div>
          </div>
        </div>
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400">Project not found</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { GitHubSyncTriggerMode, GitHubSyncMode, GitHubSyncModeLabels, GitHubSyncModeDescriptions } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useGitHubSyncStore } from '~/stores/github-sync'
import { useGitHubIdentitiesStore } from '~/stores/github-identities'

const route = useRoute()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const syncStore = useGitHubSyncStore()
const identitiesStore = useGitHubIdentitiesStore()

const tabs = ['Configuration', 'Sync Runs', 'Conflicts']
const activeTab = ref(
  tabs.includes(route.query.tab as string) ? (route.query.tab as string) : 'Configuration',
)

const form = reactive({
  gitHubIdentityId: '',
  gitHubRepo: '',
  triggerMode: GitHubSyncTriggerMode.Off as number,
  syncMode: GitHubSyncMode.Import as number,
})

// Map mode enum value to description text for the radio cards
const syncModeOptions: Record<number, string> = {
  [GitHubSyncMode.Import]: GitHubSyncModeDescriptions[GitHubSyncMode.Import],
  [GitHubSyncMode.TwoWay]: GitHubSyncModeDescriptions[GitHubSyncMode.TwoWay],
  [GitHubSyncMode.CreateOnGitHub]: GitHubSyncModeDescriptions[GitHubSyncMode.CreateOnGitHub],
}

const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const triggering = ref(false)
const runsComponent = ref<InstanceType<typeof import('~/components/ScheduledTaskRuns.vue').default> | null>(null)

async function saveConfig() {
  saveError.value = null
  saveSuccess.value = false
  try {
    await syncStore.saveConfig(id, {
      gitHubIdentityId: form.gitHubIdentityId || null,
      gitHubRepo: form.gitHubRepo || null,
      triggerMode: form.triggerMode,
      syncMode: form.syncMode,
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save configuration'
  }
}

async function triggerSync() {
  triggering.value = true
  try {
    await syncStore.triggerSync(id)
    activeTab.value = 'Sync Runs'
    await new Promise(resolve => setTimeout(resolve, 1500))
    await syncStore.fetchRuns(id)
  } catch {
    // error already in store
  } finally {
    triggering.value = false
  }
}

async function loadConflicts() {
  await syncStore.fetchConflicts(id)
}

watch(activeTab, async (tab) => {
  if (tab === 'Sync Runs') await syncStore.fetchRuns(id)
  if (tab === 'Conflicts') await syncStore.fetchConflicts(id)
})

onMounted(async () => {
  await Promise.all([
    projectsStore.fetchProject(id),
    syncStore.fetchConfig(id),
    identitiesStore.fetchIdentities(),
    activeTab.value === 'Sync Runs' ? syncStore.fetchRuns(id) : Promise.resolve(),
    activeTab.value === 'Conflicts' ? syncStore.fetchConflicts(id) : Promise.resolve(),
  ])

  if (syncStore.config) {
    form.gitHubIdentityId = syncStore.config.gitHubIdentityId ?? ''
    form.gitHubRepo = syncStore.config.gitHubRepo ?? ''
    form.triggerMode = syncStore.config.triggerMode
    form.syncMode = syncStore.config.syncMode
  }
})
</script>
