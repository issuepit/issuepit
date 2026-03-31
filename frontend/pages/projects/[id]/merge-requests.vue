<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Merge Requests', to: `/projects/${id}/merge-requests`, icon: 'M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4' },
      ]" />
      <span class="ml-auto">
        <button @click="showCreateModal = true"
          class="flex items-center gap-1.5 bg-brand-600 hover:bg-brand-700 text-white text-sm px-4 py-2 rounded-lg transition-colors">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          New Merge Request
        </button>
      </span>
    </div>

    <!-- Filter tabs -->
    <div class="flex gap-1 border-b border-gray-800 mb-6">
      <button v-for="tab in tabs" :key="tab.value" @click="activeTab = tab.value"
        :class="[
          'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
          activeTab === tab.value
            ? 'text-white border-brand-500'
            : 'text-gray-400 hover:text-gray-200 border-transparent'
        ]">
        {{ tab.label }}
        <span v-if="tabCount(tab.value) !== null"
          class="ml-1.5 text-xs bg-gray-700 text-gray-300 px-1.5 py-0.5 rounded-full">
          {{ tabCount(tab.value) }}
        </span>
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="p-4 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
      {{ error }}
    </div>

    <!-- Empty state -->
    <div v-else-if="filteredMrs.length === 0"
      class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
      <svg class="w-10 h-10 text-gray-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
          d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
      </svg>
      <p class="text-gray-500 text-sm">
        {{ activeTab === 'Open' ? 'No open merge requests.' : activeTab === 'Merged' ? 'No merged requests yet.' : 'No closed merge requests.' }}
      </p>
      <button v-if="activeTab === 'Open'" @click="showCreateModal = true"
        class="mt-4 text-sm text-brand-400 hover:text-brand-300 transition-colors">
        Create a merge request →
      </button>
    </div>

    <!-- List -->
    <div v-else class="space-y-2">
      <NuxtLink v-for="mr in filteredMrs" :key="mr.id"
        :to="`/projects/${id}/merge-requests/${mr.id}`"
        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 transition-colors cursor-pointer block">
        <div class="flex items-start gap-3">
          <!-- Status icon -->
          <div class="mt-0.5 shrink-0">
            <span v-if="mr.statusName === 'Open'" class="w-5 h-5 rounded-full bg-green-900/40 border border-green-600 flex items-center justify-center" title="Open">
              <svg class="w-3 h-3 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5"
                  d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
            </span>
            <span v-else-if="mr.statusName === 'Merged'" class="w-5 h-5 rounded-full bg-purple-900/40 border border-purple-600 flex items-center justify-center" title="Merged">
              <svg class="w-3 h-3 text-purple-400" fill="currentColor" viewBox="0 0 16 16">
                <path d="M5 3.25a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm0 2.122a2.25 2.25 0 10-1.5 0v.878A2.25 2.25 0 005.75 8.5h1.5v2.128a2.251 2.251 0 101.5 0V8.5h1.5a2.25 2.25 0 002.25-2.25v-.878a2.25 2.25 0 10-1.5 0v.878a.75.75 0 01-.75.75h-4.5A.75.75 0 015 6.25v-.878zm3.75 7.378a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm3-8.75a.75.75 0 11-1.5 0 .75.75 0 011.5 0z" />
              </svg>
            </span>
            <span v-else class="w-5 h-5 rounded-full bg-gray-700 border border-gray-600 flex items-center justify-center" title="Closed">
              <svg class="w-3 h-3 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </span>
          </div>

          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="font-medium text-white text-sm">{{ mr.title }}</span>
              <span v-if="mr.autoMergeEnabled"
                class="text-xs bg-blue-900/30 text-blue-400 px-1.5 py-0.5 rounded-full">Auto-merge</span>
              <span v-if="mr.mergeStrategyName && mr.mergeStrategyName !== 'Merge'"
                class="text-xs bg-gray-700 text-gray-300 px-1.5 py-0.5 rounded-full">{{ mr.mergeStrategyName }}</span>
              <span v-if="mr.requireCiToPass"
                class="text-xs bg-yellow-900/30 text-yellow-400 px-1.5 py-0.5 rounded-full">CI required</span>
              <!-- CI status badge -->
              <span v-if="mr.lastCiCdRunStatusName"
                :class="ciStatusClass(mr.lastCiCdRunStatusName)"
                class="text-xs px-1.5 py-0.5 rounded-full">
                CI: {{ mr.lastCiCdRunStatusName }}
              </span>
            </div>
            <p class="text-xs text-gray-500 mt-1">
              <code class="bg-gray-800 px-1 rounded text-gray-300">{{ mr.sourceBranch }}</code>
              <span class="mx-1">→</span>
              <code class="bg-gray-800 px-1 rounded text-gray-300">{{ mr.targetBranch }}</code>
              <span class="ml-2 text-gray-600"><DateDisplay :date="mr.createdAt" mode="auto" resolution="date" /></span>
            </p>
          </div>

          <!-- Actions for open MRs -->
          <div v-if="mr.statusName === 'Open'" class="flex items-center gap-2 shrink-0" @click.prevent>
            <button @click.prevent="mergeMr(mr)"
              :disabled="actionLoading === mr.id"
              class="text-xs bg-purple-700 hover:bg-purple-600 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
              Merge
            </button>
            <button @click.prevent="closeMr(mr)"
              :disabled="actionLoading === mr.id"
              class="text-xs text-gray-400 hover:text-gray-200 px-2 py-1.5 rounded-lg hover:bg-gray-800 transition-colors disabled:opacity-50">
              Close
            </button>
          </div>
          <div v-else-if="mr.statusName === 'Closed'" class="shrink-0" @click.prevent>
            <button @click.prevent="reopenMr(mr)"
              :disabled="actionLoading === mr.id"
              class="text-xs text-gray-400 hover:text-gray-200 px-2 py-1.5 rounded-lg hover:bg-gray-800 transition-colors disabled:opacity-50">
              Reopen
            </button>
          </div>
        </div>
      </NuxtLink>
    </div>

    <!-- Create MR Modal -->
    <div v-if="showCreateModal"
      class="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4"
      @mousedown.self="showCreateModal = false">
      <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-md p-6 space-y-4">
        <h2 class="text-lg font-bold text-white">New Merge Request</h2>

        <div class="space-y-3">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Title <span class="text-red-400">*</span></label>
            <input v-model="form.title" type="text" placeholder="Merge feature branch into main"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Description</label>
            <textarea v-model="form.description" rows="3" placeholder="Optional description…"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none" />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Source Branch <span class="text-red-400">*</span></label>
            <BranchSelect v-model="form.sourceBranch" :branches="branches" />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Target Branch</label>
            <BranchSelect v-model="form.targetBranch" :branches="branches" />
            <p class="text-xs text-gray-500 mt-1">Leave blank to use the repository's default branch.</p>
          </div>

          <div class="flex items-center justify-between py-1">
            <div>
              <label class="block text-sm font-medium text-gray-300">Auto-merge when CI succeeds</label>
              <p class="text-xs text-gray-500 mt-0.5">Automatically merge once all CI checks pass.</p>
            </div>
            <button type="button"
              :class="form.autoMergeEnabled ? 'bg-brand-600' : 'bg-gray-700'"
              class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
              @click="form.autoMergeEnabled = !form.autoMergeEnabled">
              <span :class="form.autoMergeEnabled ? 'translate-x-6' : 'translate-x-1'"
                class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
            </button>
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Merge Strategy</label>
            <select v-model="form.mergeStrategy"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="opt in mergeStrategyOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
            </select>
            <p class="text-xs text-gray-500 mt-1">{{ mergeStrategyDescription }}</p>
          </div>

          <div class="flex items-center justify-between py-1">
            <div>
              <label class="block text-sm font-medium text-gray-300">Require CI to pass</label>
              <p class="text-xs text-gray-500 mt-0.5">Block merge until all CI checks succeed.</p>
            </div>
            <button type="button"
              :class="form.requireCiToPass ? 'bg-brand-600' : 'bg-gray-700'"
              class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
              @click="form.requireCiToPass = !form.requireCiToPass">
              <span :class="form.requireCiToPass ? 'translate-x-6' : 'translate-x-1'"
                class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
            </button>
          </div>

          <div class="flex items-center justify-between py-1">
            <div>
              <label class="block text-sm font-medium text-gray-300">Delete source branch on merge</label>
              <p class="text-xs text-gray-500 mt-0.5">Remove the source branch after a successful merge.</p>
            </div>
            <button type="button"
              :class="form.deleteSourceBranchOnMerge ? 'bg-brand-600' : 'bg-gray-700'"
              class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
              @click="form.deleteSourceBranchOnMerge = !form.deleteSourceBranchOnMerge">
              <span :class="form.deleteSourceBranchOnMerge ? 'translate-x-6' : 'translate-x-1'"
                class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
            </button>
          </div>
        </div>

        <div v-if="createError" class="p-3 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
          {{ createError }}
        </div>

        <div class="flex gap-2 pt-2">
          <button @click="createMr" :disabled="creating || !form.title || !form.sourceBranch"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm py-2 rounded-lg transition-colors disabled:opacity-50">
            <span v-if="creating">Creating…</span>
            <span v-else>Create Merge Request</span>
          </button>
          <button @click="showCreateModal = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-white text-sm py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { GitBranch } from '~/types'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const api = useApi()

const projectsStore = useProjectsStore()

interface MergeRequestDto {
  id: string
  projectId: string
  title: string
  description: string | null
  sourceBranch: string
  targetBranch: string
  status: number
  statusName: string
  mergeStrategy: number
  mergeStrategyName: string
  autoMergeEnabled: boolean
  deleteSourceBranchOnMerge: boolean
  requireCiToPass: boolean
  lastKnownSourceSha: string | null
  lastCiCdRunId: string | null
  lastCiCdRunStatus: number | null
  lastCiCdRunStatusName: string | null
  createdAt: string
  updatedAt: string
  mergedAt: string | null
  mergeCommitSha: string | null
}

const mergeRequests = ref<MergeRequestDto[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const actionLoading = ref<string | null>(null)

const activeTab = ref<'Open' | 'Merged' | 'Closed'>('Open')
const tabs = [
  { label: 'Open', value: 'Open' as const },
  { label: 'Merged', value: 'Merged' as const },
  { label: 'Closed', value: 'Closed' as const },
]

const filteredMrs = computed(() =>
  mergeRequests.value.filter(m => m.statusName === activeTab.value)
)

const tabCount = (tab: string) =>
  mergeRequests.value.filter(m => m.statusName === tab).length

// Create modal state
const showCreateModal = ref(false)
const creating = ref(false)
const createError = ref<string | null>(null)
const branches = ref<GitBranch[]>([])
const defaultBranch = ref<string>('')
const form = reactive({
  title: '',
  description: '',
  sourceBranch: '',
  targetBranch: '',
  mergeStrategy: 0,
  autoMergeEnabled: false,
  deleteSourceBranchOnMerge: false,
  requireCiToPass: false,
})

const mergeStrategyOptions = [
  { value: 0, label: 'Merge commit' },
  { value: 1, label: 'Squash and merge' },
  { value: 2, label: 'Rebase and merge' },
]

const mergeStrategyDescription = computed(() => {
  switch (form.mergeStrategy) {
    case 1: return 'All commits will be squashed into a single commit on the target branch.'
    case 2: return 'Commits will be rebased on top of the target branch for a linear history.'
    default: return 'A merge commit will be created to combine the branches.'
  }
})

function resetForm() {
  form.title = ''
  form.description = ''
  form.sourceBranch = ''
  form.targetBranch = defaultBranch.value
  form.mergeStrategy = 0
  form.autoMergeEnabled = false
  form.deleteSourceBranchOnMerge = false
  form.requireCiToPass = false
  createError.value = null
}

watch(showCreateModal, async (val) => {
  if (val) {
    resetForm()
    await loadBranches()
    // Update targetBranch after branches load in case defaultBranch was just resolved
    if (form.targetBranch === '' && defaultBranch.value) {
      form.targetBranch = defaultBranch.value
    }
  }
})

async function loadBranches() {
  try {
    branches.value = await api.get<GitBranch[]>(`/api/projects/${id}/git/branches`)
    // Resolve default branch from the git repo config if not yet known
    if (!defaultBranch.value) {
      const repos = await api.get<{ defaultBranch: string }[]>(`/api/projects/${id}/git/repos`)
      if (repos.length > 0) {
        defaultBranch.value = repos[0].defaultBranch ?? 'main'
      }
    }
    if (!form.targetBranch) {
      form.targetBranch = defaultBranch.value
    }
  } catch {
    branches.value = []
  }
}

async function fetchMrs() {
  loading.value = true
  error.value = null
  try {
    mergeRequests.value = await api.get<MergeRequestDto[]>(`/api/projects/${id}/merge-requests`)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load merge requests'
  } finally {
    loading.value = false
  }
}

async function createMr() {
  creating.value = true
  createError.value = null
  try {
    const mr = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests`, {
      title: form.title,
      description: form.description || null,
      sourceBranch: form.sourceBranch,
      targetBranch: form.targetBranch || null,
      mergeStrategy: form.mergeStrategy,
      autoMergeEnabled: form.autoMergeEnabled,
      deleteSourceBranchOnMerge: form.deleteSourceBranchOnMerge,
      requireCiToPass: form.requireCiToPass,
    })
    mergeRequests.value.unshift(mr)
    showCreateModal.value = false
    activeTab.value = 'Open'
  } catch (e: unknown) {
    createError.value = e instanceof Error ? e.message : 'Failed to create merge request'
  } finally {
    creating.value = false
  }
}

async function mergeMr(mr: MergeRequestDto) {
  actionLoading.value = mr.id
  try {
    const updated = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mr.id}/merge`, {})
    const idx = mergeRequests.value.findIndex(m => m.id === mr.id)
    if (idx !== -1) mergeRequests.value[idx] = updated
  } catch (e: unknown) {
    alert(e instanceof Error ? e.message : 'Merge failed')
  } finally {
    actionLoading.value = null
  }
}

async function closeMr(mr: MergeRequestDto) {
  actionLoading.value = mr.id
  try {
    const updated = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mr.id}/close`, {})
    const idx = mergeRequests.value.findIndex(m => m.id === mr.id)
    if (idx !== -1) mergeRequests.value[idx] = updated
  } catch (e: unknown) {
    alert(e instanceof Error ? e.message : 'Failed to close MR')
  } finally {
    actionLoading.value = null
  }
}

async function reopenMr(mr: MergeRequestDto) {
  actionLoading.value = mr.id
  try {
    const updated = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mr.id}/reopen`, {})
    const idx = mergeRequests.value.findIndex(m => m.id === mr.id)
    if (idx !== -1) mergeRequests.value[idx] = updated
  } catch (e: unknown) {
    alert(e instanceof Error ? e.message : 'Failed to reopen MR')
  } finally {
    actionLoading.value = null
  }
}

function ciStatusClass(statusName: string): string {
  switch (statusName) {
    case 'Succeeded': return 'bg-green-900/30 text-green-400'
    case 'Failed': return 'bg-red-900/30 text-red-400'
    case 'Running': return 'bg-yellow-900/30 text-yellow-400'
    case 'Pending': return 'bg-gray-700 text-gray-400'
    case 'Cancelled': return 'bg-gray-700 text-gray-400'
    default: return 'bg-gray-700 text-gray-400'
  }
}

onMounted(async () => {
  await projectsStore.fetchProject(id)
  await fetchMrs()
})
</script>
