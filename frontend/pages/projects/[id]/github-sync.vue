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
          { label: 'GitHub Sync', to: `/projects/${id}/github-sync`, icon: 'M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207' },
        ]" />
      </div>

      <!-- Tabs (shared with other settings pages) -->
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
      </div>

      <!-- Inner tabs -->
      <div class="flex gap-1 mb-6">
        <button
          v-for="tab in tabs"
          :key="tab"
          :class="[
            'px-4 py-1.5 text-sm font-medium rounded-lg transition-colors',
            activeTab === tab ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-400 hover:text-gray-200'
          ]"
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
              <select v-model="form.gitHubIdentityId"
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
              <input v-model="form.gitHubRepo" type="text" placeholder="owner/repo (e.g. acme/backend)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              <p class="text-xs text-gray-600 mt-1">Format: <span class="font-mono">owner/repo</span></p>
            </div>

            <!-- Trigger Mode -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Trigger Mode</label>
              <select v-model.number="form.triggerMode"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option :value="GitHubSyncTriggerMode.Off">Off — sync disabled</option>
                <option :value="GitHubSyncTriggerMode.Manual">Manual — trigger from this page only</option>
                <option :value="GitHubSyncTriggerMode.Auto">Auto — sync runs on a schedule (not recommended as default)</option>
              </select>
            </div>

            <!-- Auto-Create on GitHub -->
            <div class="flex items-center justify-between py-2 border-t border-gray-800">
              <div>
                <p class="text-sm font-medium text-gray-300">Auto-Create on GitHub</p>
                <p class="text-xs text-gray-500 mt-0.5">
                  When enabled, new issues created in IssuePit are automatically pushed to GitHub as new issues.
                </p>
              </div>
              <button
                type="button"
                :class="form.autoCreateOnGitHub ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full transition-colors duration-200"
                @click="form.autoCreateOnGitHub = !form.autoCreateOnGitHub"
              >
                <span
                  :class="form.autoCreateOnGitHub ? 'translate-x-4' : 'translate-x-0.5'"
                  class="inline-block h-4 w-4 mt-0.5 rounded-full bg-white transition-transform duration-200"
                />
              </button>
            </div>

            <p v-if="saveError" class="text-red-400 text-sm">{{ saveError }}</p>
            <p v-if="saveSuccess" class="text-green-400 text-sm">Configuration saved.</p>

            <div class="flex gap-3 pt-1">
              <button type="submit" :disabled="syncStore.loading"
                class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
                {{ syncStore.loading ? 'Saving…' : 'Save Configuration' }}
              </button>
              <button
                v-if="form.triggerMode !== GitHubSyncTriggerMode.Off"
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
        <div class="flex items-center justify-between mb-4">
          <p class="text-sm text-gray-400">{{ syncStore.runs.length }} run(s)</p>
          <button
            :disabled="triggering"
            class="flex items-center gap-1.5 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm px-3 py-1.5 rounded-lg transition-colors"
            @click="triggerSync"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ triggering ? 'Triggered…' : 'Trigger Sync' }}
          </button>
        </div>

        <div v-if="syncStore.loading" class="flex items-center justify-center py-16">
          <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="syncStore.runs.length === 0" class="py-16 text-center">
          <p class="text-gray-500">No sync runs yet. Trigger a sync to see history here.</p>
        </div>

        <div v-else class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Summary</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Started</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Duration</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="run in syncStore.runs" :key="run.id"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="openRun(run.id)">
                <td class="px-4 py-3">
                  <span :class="runStatusClass(run.status)" class="text-xs px-2 py-0.5 rounded-full font-medium">
                    {{ RunStatusLabels[run.status] }}
                  </span>
                </td>
                <td class="px-4 py-3 text-gray-300 text-xs">{{ run.summary || '—' }}</td>
                <td class="px-4 py-3 text-gray-400 text-xs">{{ formatDate(run.startedAt) }}</td>
                <td class="px-4 py-3 text-gray-400 text-xs">{{ duration(run.startedAt, run.completedAt) }}</td>
                <td class="px-4 py-3 text-right">
                  <button class="text-xs text-brand-400 hover:text-brand-300" @click.stop="openRun(run.id)">
                    View logs →
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Run Detail Modal -->
        <div v-if="selectedRun" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl shadow-xl flex flex-col max-h-[80vh]">
            <div class="flex items-center justify-between px-6 py-4 border-b border-gray-800">
              <div>
                <h2 class="text-base font-bold text-white">Sync Run Logs</h2>
                <p class="text-xs text-gray-500 mt-0.5">
                  <span :class="runStatusClass(selectedRun.status)" class="px-1.5 py-0.5 rounded-full font-medium">{{ RunStatusLabels[selectedRun.status] }}</span>
                  <span class="ml-2">{{ formatDate(selectedRun.startedAt) }}</span>
                  <span v-if="selectedRun.summary" class="ml-2">— {{ selectedRun.summary }}</span>
                </p>
              </div>
              <button @click="selectedRun = null" class="text-gray-500 hover:text-gray-300 text-xl leading-none">&times;</button>
            </div>
            <div class="overflow-y-auto p-4 font-mono text-xs space-y-0.5">
              <div v-if="!selectedRun.logs?.length" class="text-gray-600 text-center py-6">No log entries.</div>
              <div
                v-for="log in selectedRun.logs"
                :key="log.id"
                :class="logLevelClass(log.level)"
              >
                <span class="text-gray-600 mr-2">{{ formatTime(log.timestamp) }}</span>
                <span :class="logLevelBadgeClass(log.level)" class="mr-2 text-xs px-1 rounded">[{{ LogLevelLabels[log.level] }}]</span>
                {{ log.message }}
              </div>
            </div>
          </div>
        </div>
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
          <div v-for="conflict in syncStore.conflicts" :key="conflict.issueId"
            class="bg-gray-900 border border-orange-800/40 rounded-xl p-5">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <span class="text-xs bg-orange-800/40 text-orange-300 px-2 py-0.5 rounded-full font-medium">Conflict</span>
                <span class="text-sm font-semibold text-white">
                  IssuePit #{{ conflict.issueNumber }} ↔ GitHub #{{ conflict.gitHubIssueNumber }}
                </span>
              </div>
              <a
                :href="conflict.gitHubUrl"
                target="_blank"
                rel="noopener noreferrer"
                class="text-xs text-brand-400 hover:text-brand-300 flex items-center gap-1"
              >
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
                class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
              >
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
import { GitHubSyncTriggerMode, GitHubSyncRunStatus, GitHubSyncLogLevel } from '~/types'
import type { GitHubSyncRunDetail } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useGitHubSyncStore } from '~/stores/github-sync'
import { useGitHubIdentitiesStore } from '~/stores/github-identities'

const route = useRoute()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const syncStore = useGitHubSyncStore()
const identitiesStore = useGitHubIdentitiesStore()

const tabs = ['Configuration', 'Sync Runs', 'Conflicts']
const activeTab = ref('Configuration')

const form = reactive({
  gitHubIdentityId: '',
  gitHubRepo: '',
  triggerMode: GitHubSyncTriggerMode.Off as number,
  autoCreateOnGitHub: false,
})

const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const triggering = ref(false)
const selectedRun = ref<GitHubSyncRunDetail | null>(null)

const RunStatusLabels: Record<GitHubSyncRunStatus, string> = {
  [GitHubSyncRunStatus.Pending]: 'Pending',
  [GitHubSyncRunStatus.Running]: 'Running',
  [GitHubSyncRunStatus.Succeeded]: 'Succeeded',
  [GitHubSyncRunStatus.Failed]: 'Failed',
}

const LogLevelLabels: Record<GitHubSyncLogLevel, string> = {
  [GitHubSyncLogLevel.Info]: 'INFO',
  [GitHubSyncLogLevel.Warn]: 'WARN',
  [GitHubSyncLogLevel.Error]: 'ERR',
}

function runStatusClass(status: GitHubSyncRunStatus) {
  switch (status) {
    case GitHubSyncRunStatus.Succeeded: return 'bg-green-900/40 text-green-300'
    case GitHubSyncRunStatus.Failed: return 'bg-red-900/40 text-red-300'
    case GitHubSyncRunStatus.Running: return 'bg-blue-900/40 text-blue-300'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function logLevelClass(level: GitHubSyncLogLevel) {
  switch (level) {
    case GitHubSyncLogLevel.Warn: return 'text-yellow-300'
    case GitHubSyncLogLevel.Error: return 'text-red-400'
    default: return 'text-gray-300'
  }
}

function logLevelBadgeClass(level: GitHubSyncLogLevel) {
  switch (level) {
    case GitHubSyncLogLevel.Warn: return 'bg-yellow-900/40 text-yellow-300'
    case GitHubSyncLogLevel.Error: return 'bg-red-900/40 text-red-300'
    default: return 'bg-gray-800 text-gray-500'
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString()
}

function duration(start: string, end?: string | null) {
  if (!end) return '—'
  const ms = new Date(end).getTime() - new Date(start).getTime()
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${Math.round(ms / 1000)}s`
  return `${Math.round(ms / 60000)}m`
}

async function saveConfig() {
  saveError.value = null
  saveSuccess.value = false
  try {
    await syncStore.saveConfig(id, {
      gitHubIdentityId: form.gitHubIdentityId || null,
      gitHubRepo: form.gitHubRepo || null,
      triggerMode: form.triggerMode,
      autoCreateOnGitHub: form.autoCreateOnGitHub,
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

async function openRun(runId: string) {
  const detail = await syncStore.fetchRun(id, runId)
  if (detail) selectedRun.value = detail
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
  ])

  if (syncStore.config) {
    form.gitHubIdentityId = syncStore.config.gitHubIdentityId ?? ''
    form.gitHubRepo = syncStore.config.gitHubRepo ?? ''
    form.triggerMode = syncStore.config.triggerMode
    form.autoCreateOnGitHub = syncStore.config.autoCreateOnGitHub
  }
})
</script>
