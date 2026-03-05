<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-2">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <h1 class="text-xl font-bold text-white">Merge Requests</h1>
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">
          {{ store.mergeRequests.length }}
        </span>
      </div>
      <button @click="showCreate = true"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Merge Request
      </button>
    </div>

    <!-- Filter tabs -->
    <div class="flex gap-2 mb-5">
      <button v-for="tab in tabs" :key="tab.value" @click="activeTab = tab.value"
        :class="[
          'text-sm px-3 py-1.5 rounded-lg transition-colors',
          activeTab === tab.value ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
        ]">
        {{ tab.label }}
        <span class="ml-1 text-xs text-gray-500">{{ tab.count }}</span>
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Merge Request List -->
    <div v-else class="space-y-3">
      <div v-if="filteredMrs.length === 0" class="py-16 text-center bg-gray-900 border border-gray-800 rounded-xl">
        <p class="text-gray-400">No merge requests found</p>
        <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
          Create the first merge request →
        </button>
      </div>

      <div v-for="mr in filteredMrs" :key="mr.id"
        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 transition-colors">
        <div class="flex items-start justify-between gap-4">
          <div class="flex-1 min-w-0">
            <!-- Status badge + title -->
            <div class="flex items-center gap-2 mb-2 flex-wrap">
              <span :class="statusBadgeClass(mr.status)" class="text-xs px-2 py-0.5 rounded-full font-medium shrink-0">
                {{ statusLabel(mr.status) }}
              </span>
              <span v-if="mr.autoMerge && mr.status === MergeRequestStatus.Open"
                class="text-xs px-2 py-0.5 rounded-full font-medium bg-purple-900/40 text-purple-400 shrink-0">
                Auto-merge
              </span>
              <span class="text-base font-semibold text-white">{{ mr.title }}</span>
            </div>

            <!-- Branch info -->
            <div class="flex items-center gap-1.5 text-sm text-gray-400 mb-2">
              <svg class="w-3.5 h-3.5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M13 16h-1v-4h-1m1-4h.01M12 20h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <code class="bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded text-xs">{{ mr.sourceBranch }}</code>
              <svg class="w-3 h-3 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
              <code class="bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded text-xs">{{ mr.targetBranch }}</code>
            </div>

            <!-- Metadata -->
            <div class="flex items-center gap-4 text-xs text-gray-500">
              <span>Opened {{ formatDate(mr.createdAt) }}</span>
              <span v-if="mr.mergedAt">Merged {{ formatDate(mr.mergedAt) }}</span>
              <span v-else-if="mr.closedAt">Closed {{ formatDate(mr.closedAt) }}</span>
              <span v-if="mr.headCommitSha" class="font-mono">{{ mr.headCommitSha.slice(0, 7) }}</span>
            </div>
          </div>

          <!-- Actions for open MRs -->
          <div v-if="mr.status === MergeRequestStatus.Open" class="flex items-center gap-2 shrink-0">
            <button @click="handleMerge(mr.id)" :disabled="merging === mr.id"
              class="text-sm bg-green-700 hover:bg-green-600 disabled:opacity-50 text-white px-3 py-1.5 rounded-lg transition-colors flex items-center gap-1.5">
              <svg v-if="merging === mr.id" class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              <svg v-else class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
              Merge
            </button>
            <button @click="handleClose(mr.id)" :disabled="closing === mr.id"
              class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Create MR Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg shadow-2xl">
        <div class="p-6 border-b border-gray-800">
          <h2 class="text-lg font-semibold text-white">New Merge Request</h2>
        </div>
        <div class="p-6 space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Title <span class="text-red-400">*</span></label>
            <input v-model="form.title" type="text" placeholder="Describe the changes..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Source Branch <span class="text-red-400">*</span></label>
            <BranchSelect v-model="form.sourceBranch" :branches="allBranches"
              placeholder="Select source branch..." />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Target Branch</label>
            <BranchSelect v-model="form.targetBranch" :branches="allBranches"
              placeholder="Default branch (main)..." />
          </div>
          <div class="flex items-center gap-3">
            <button @click="form.autoMerge = !form.autoMerge"
              :class="form.autoMerge ? 'bg-brand-600' : 'bg-gray-700'"
              class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors">
              <span :class="form.autoMerge ? 'translate-x-4' : 'translate-x-0'"
                class="pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform" />
            </button>
            <span class="text-sm text-gray-300">Auto-merge when CI succeeds</span>
          </div>
          <ErrorBox :error="createError" />
        </div>
        <div class="p-6 border-t border-gray-800 flex justify-end gap-3">
          <button @click="closeCreateModal"
            class="text-sm text-gray-400 hover:text-gray-200 px-4 py-2 rounded-lg hover:bg-gray-800 transition-colors">
            Cancel
          </button>
          <button @click="handleCreate" :disabled="creating || !form.title || !form.sourceBranch"
            class="text-sm bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white px-4 py-2 rounded-lg transition-colors">
            {{ creating ? 'Creating...' : 'Create Merge Request' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { MergeRequestStatus } from '~/types'
import type { GitBranch } from '~/types'
import { useMergeRequestsStore } from '~/stores/mergeRequests'
import { useGitStore } from '~/stores/git'

const route = useRoute()
const id = route.params.id as string

const store = useMergeRequestsStore()
const gitStore = useGitStore()

// Tabs
const activeTab = ref<'open' | 'merged' | 'closed' | 'all'>('open')
const tabs = computed(() => [
  { label: 'Open', value: 'open' as const, count: store.mergeRequests.filter(m => m.status === MergeRequestStatus.Open).length },
  { label: 'Merged', value: 'merged' as const, count: store.mergeRequests.filter(m => m.status === MergeRequestStatus.Merged).length },
  { label: 'Closed', value: 'closed' as const, count: store.mergeRequests.filter(m => m.status === MergeRequestStatus.Closed).length },
  { label: 'All', value: 'all' as const, count: store.mergeRequests.length },
])

const filteredMrs = computed(() => {
  if (activeTab.value === 'all') return store.mergeRequests
  return store.mergeRequests.filter(m => m.status === activeTab.value)
})

// Branches for the create form
const allBranches = computed<GitBranch[]>(() => gitStore.branches)

// Status helpers
function statusLabel(status: MergeRequestStatus) {
  switch (status) {
    case MergeRequestStatus.Open: return 'Open'
    case MergeRequestStatus.Merged: return 'Merged'
    case MergeRequestStatus.Closed: return 'Closed'
  }
}

function statusBadgeClass(status: MergeRequestStatus) {
  switch (status) {
    case MergeRequestStatus.Open: return 'bg-green-900/40 text-green-400'
    case MergeRequestStatus.Merged: return 'bg-purple-900/40 text-purple-400'
    case MergeRequestStatus.Closed: return 'bg-gray-800 text-gray-500'
  }
}

function formatDate(dateStr: string) {
  const d = new Date(dateStr)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

// Create MR
const showCreate = ref(false)
const creating = ref(false)
const createError = ref<string | null>(null)

const form = reactive({
  title: '',
  sourceBranch: '',
  targetBranch: '',
  autoMerge: false,
})

function closeCreateModal() {
  showCreate.value = false
  form.title = ''
  form.sourceBranch = ''
  form.targetBranch = ''
  form.autoMerge = false
  createError.value = null
}

async function handleCreate() {
  if (!form.title || !form.sourceBranch) return
  creating.value = true
  createError.value = null
  try {
    await store.createMergeRequest(id, form.title, form.sourceBranch, form.targetBranch, form.autoMerge)
    closeCreateModal()
    // Switch to Open tab to see the new MR
    activeTab.value = 'open'
  } catch (e: unknown) {
    createError.value = e instanceof Error ? e.message : 'Failed to create merge request'
  } finally {
    creating.value = false
  }
}

// Merge / close actions
const merging = ref<string | null>(null)
const closing = ref<string | null>(null)

async function handleMerge(mrId: string) {
  if (merging.value) return
  merging.value = mrId
  store.error = null
  try {
    await store.mergeMergeRequest(id, mrId)
  } finally {
    merging.value = null
  }
}

async function handleClose(mrId: string) {
  if (closing.value) return
  closing.value = mrId
  store.error = null
  try {
    await store.closeMergeRequest(id, mrId)
  } finally {
    closing.value = null
  }
}

// Load data
onMounted(async () => {
  await Promise.all([
    store.fetchMergeRequests(id),
    gitStore.fetchBranches(id),
  ])
})
</script>
