<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </NuxtLink>
      <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
      </svg>
      <h1 class="text-xl font-bold text-white">Code</h1>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.repo && !repoChecked"
      class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- No repo configured -->
    <div v-else-if="repoChecked && !store.repo">
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-8 max-w-lg">
        <div class="flex items-center gap-3 mb-4">
          <div class="w-10 h-10 bg-orange-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
          </div>
          <div>
            <h2 class="font-semibold text-white">Link a Git Repository</h2>
            <p class="text-sm text-gray-400">Connect a git repository to browse code</p>
          </div>
        </div>
        <div class="space-y-3">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Remote URL</label>
            <input v-model="form.remoteUrl" type="text" placeholder="https://github.com/org/repo.git or git@..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Default branch</label>
            <input v-model="form.defaultBranch" type="text" placeholder="main"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Username <span class="text-gray-500">(optional)</span></label>
            <input v-model="form.authUsername" type="text" placeholder="git user or leave blank for anonymous"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Token / Password <span class="text-gray-500">(optional)</span></label>
            <input v-model="form.authToken" type="password" placeholder="PAT or password"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <p v-if="store.error" class="text-red-400 text-sm">{{ store.error }}</p>
          <button @click="linkRepo"
            class="w-full bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Link Repository
          </button>
        </div>
      </div>
    </div>

    <!-- Repo configured -->
    <template v-else-if="store.repo">
      <!-- Repo info bar -->
      <div class="flex flex-wrap items-center gap-3 mb-4">
        <div class="flex items-center gap-2 bg-gray-900 border border-gray-800 rounded-lg px-3 py-1.5">
          <svg class="w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
          </svg>
          <span class="text-sm text-gray-300 truncate max-w-xs">{{ store.repo.remoteUrl }}</span>
        </div>

        <!-- Branch selector -->
        <select v-model="selectedBranch" @change="onBranchChange"
          class="bg-gray-900 border border-gray-800 rounded-lg px-3 py-1.5 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
          <option v-for="b in localBranches" :key="b.name" :value="b.name">{{ b.name }}</option>
        </select>

        <div class="flex items-center gap-2 ml-auto">
          <button @click="doFetch" :disabled="fetching"
            class="flex items-center gap-1.5 bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 text-sm px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
            <svg class="w-4 h-4" :class="{ 'animate-spin': fetching }" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ fetching ? 'Fetching…' : 'Fetch' }}
          </button>
          <span v-if="store.repo.lastFetchedAt" class="text-xs text-gray-500">
            Last fetched {{ formatDate(store.repo.lastFetchedAt) }}
          </span>
        </div>
      </div>

      <!-- Tabs -->
      <div class="flex border-b border-gray-800 mb-4">
        <button v-for="tab in tabs" :key="tab.id" @click="activeTab = tab.id"
          class="px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px"
          :class="activeTab === tab.id
            ? 'border-brand-500 text-white'
            : 'border-transparent text-gray-400 hover:text-gray-200'">
          {{ tab.label }}
        </button>
      </div>

      <!-- Code browser tab -->
      <div v-if="activeTab === 'code'" class="flex gap-4">
        <!-- File tree -->
        <div class="w-64 shrink-0 bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <!-- Breadcrumb path -->
          <div class="flex items-center gap-1 px-3 py-2 border-b border-gray-800 text-sm text-gray-400 flex-wrap">
            <button @click="navigateTo('')" class="hover:text-white transition-colors">root</button>
            <template v-for="(part, i) in pathParts" :key="i">
              <span class="text-gray-600">/</span>
              <button @click="navigateTo(pathParts.slice(0, i + 1).join('/'))"
                class="hover:text-white transition-colors">{{ part }}</button>
            </template>
          </div>
          <div v-if="store.loading" class="flex justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
          <div v-else class="overflow-y-auto max-h-[600px]">
            <button v-if="currentPath" @click="navigateUp"
              class="w-full flex items-center gap-2 px-3 py-1.5 text-sm text-gray-400 hover:bg-gray-800 hover:text-white transition-colors">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
              </svg>
              ..
            </button>
            <button v-for="entry in store.tree" :key="entry.path"
              @click="onEntryClick(entry)"
              class="w-full flex items-center gap-2 px-3 py-1.5 text-sm transition-colors"
              :class="[
                entry.path === selectedFile ? 'bg-gray-800 text-white' : 'text-gray-300 hover:bg-gray-800 hover:text-white',
              ]">
              <!-- folder icon -->
              <svg v-if="entry.type === 'tree'" class="w-4 h-4 text-yellow-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
              </svg>
              <!-- file icon -->
              <svg v-else class="w-4 h-4 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              <span class="truncate">{{ entry.name }}</span>
              <span v-if="entry.type === 'blob' && entry.size > 0" class="ml-auto text-xs text-gray-600">
                {{ formatSize(entry.size) }}
              </span>
            </button>
            <div v-if="!store.loading && store.tree.length === 0"
              class="px-3 py-4 text-sm text-gray-500 text-center">
              Empty directory
            </div>
          </div>
        </div>

        <!-- File content -->
        <div class="flex-1 bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div v-if="!store.blob && !selectedFile" class="flex flex-col items-center justify-center h-48 text-gray-500">
            <svg class="w-8 h-8 mb-2 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <p class="text-sm">Select a file to view its contents</p>
          </div>
          <div v-else-if="store.loading" class="flex justify-center py-12">
            <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
          <template v-else-if="store.blob">
            <!-- File header -->
            <div class="flex items-center justify-between px-4 py-2 border-b border-gray-800">
              <span class="text-sm text-gray-300 font-mono">{{ store.blob.path }}</span>
              <span class="text-xs text-gray-500">{{ formatSize(store.blob.size) }}</span>
            </div>
            <!-- Binary notice -->
            <div v-if="store.blob.isBinary" class="p-6 text-center text-gray-400 text-sm">
              Binary file — preview not available
            </div>
            <!-- Text content -->
            <pre v-else
              class="p-4 text-sm font-mono text-gray-200 overflow-auto max-h-[600px] whitespace-pre leading-relaxed">{{ store.blob.content }}</pre>
          </template>
        </div>
      </div>

      <!-- Commits tab -->
      <div v-else-if="activeTab === 'commits'">
        <div v-if="store.loading && store.commits.length === 0" class="flex justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
        </div>
        <div v-else class="space-y-0 bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div v-for="commit in store.commits" :key="commit.sha"
            class="flex items-start gap-3 px-4 py-3 border-b border-gray-800 last:border-0">
            <div class="w-8 h-8 rounded-full bg-gray-800 flex items-center justify-center text-xs font-medium text-gray-300 shrink-0">
              {{ commit.authorName?.charAt(0)?.toUpperCase() || '?' }}
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-sm text-white font-medium truncate">{{ commit.messageShort }}</p>
              <p class="text-xs text-gray-400 mt-0.5">
                {{ commit.authorName }}
                <span class="text-gray-600 mx-1">·</span>
                {{ formatDate(commit.date) }}
              </p>
            </div>
            <code class="text-xs text-gray-500 font-mono shrink-0">{{ commit.sha.slice(0, 7) }}</code>
          </div>
          <div v-if="store.commits.length === 0 && !store.loading"
            class="p-6 text-center text-gray-500 text-sm">
            No commits found
          </div>
        </div>
        <div v-if="store.hasMoreCommits" class="mt-3 text-center">
          <button @click="loadMoreCommits"
            class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
            Load more
          </button>
        </div>
      </div>

      <!-- Branches tab -->
      <div v-else-if="activeTab === 'branches'">
        <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div v-for="branch in store.branches" :key="branch.name"
            class="flex items-center gap-3 px-4 py-3 border-b border-gray-800 last:border-0">
            <svg class="w-4 h-4 text-gray-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            <span class="text-sm text-white font-mono flex-1">{{ branch.name }}</span>
            <span v-if="branch.isRemote"
              class="text-xs bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded">remote</span>
            <code class="text-xs text-gray-500 font-mono">{{ branch.sha.slice(0, 7) }}</code>
            <span v-if="branch.commitDate" class="text-xs text-gray-500">
              {{ formatDate(branch.commitDate) }}
            </span>
          </div>
          <div v-if="store.branches.length === 0"
            class="p-6 text-center text-gray-500 text-sm">
            No branches found
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useGitStore } from '~/stores/git'

const route = useRoute()
const id = route.params.id as string
const store = useGitStore()

const repoChecked = ref(false)
const fetching = ref(false)
const activeTab = ref<'code' | 'commits' | 'branches'>('code')
const tabs = [
  { id: 'code' as const, label: 'Code' },
  { id: 'commits' as const, label: 'Commits' },
  { id: 'branches' as const, label: 'Branches' },
]

const selectedBranch = ref('')
const currentPath = ref('')
const selectedFile = ref('')
const commitSkip = ref(0)
const commitTake = 30
const hasMoreCommits = ref(false)

const form = reactive({
  remoteUrl: '',
  defaultBranch: 'main',
  authUsername: '',
  authToken: ''
})

const pathParts = computed(() => currentPath.value ? currentPath.value.split('/') : [])

const localBranches = computed(() =>
  store.branches.filter(b => !b.isRemote)
)

onMounted(async () => {
  store.reset()
  await store.fetchRepo(id)
  repoChecked.value = true
  if (store.repo) {
    await initRepo()
  }
})

onUnmounted(() => store.reset())

async function initRepo() {
  await Promise.all([
    store.fetchBranches(id),
    store.fetchTree(id, store.repo?.defaultBranch, '')
  ])
  // Set default branch selection
  const def = store.repo?.defaultBranch ?? 'main'
  const found = localBranches.value.find(b => b.name === def) ?? localBranches.value[0]
  selectedBranch.value = found?.name ?? def
  await store.fetchCommits(id, selectedBranch.value, 0, commitTake)
}

async function onBranchChange() {
  currentPath.value = ''
  selectedFile.value = ''
  store.blob = null
  await store.fetchTree(id, selectedBranch.value, '')
  commitSkip.value = 0
  if (activeTab.value === 'commits')
    await store.fetchCommits(id, selectedBranch.value, 0, commitTake)
}

async function onEntryClick(entry: { type: string; path: string }) {
  if (entry.type === 'tree') {
    currentPath.value = entry.path
    selectedFile.value = ''
    store.blob = null
    await store.fetchTree(id, selectedBranch.value, entry.path)
  } else {
    selectedFile.value = entry.path
    await store.fetchBlob(id, entry.path, selectedBranch.value)
  }
}

async function navigateTo(path: string) {
  currentPath.value = path
  selectedFile.value = ''
  store.blob = null
  await store.fetchTree(id, selectedBranch.value, path)
}

async function navigateUp() {
  const parts = currentPath.value.split('/')
  parts.pop()
  await navigateTo(parts.join('/'))
}

async function doFetch() {
  fetching.value = true
  await store.triggerFetch(id)
  fetching.value = false
}

async function loadMoreCommits() {
  commitSkip.value += commitTake
  await store.fetchCommits(id, selectedBranch.value, commitSkip.value, commitTake)
}

async function linkRepo() {
  await store.createRepo(id, {
    remoteUrl: form.remoteUrl,
    defaultBranch: form.defaultBranch || 'main',
    authUsername: form.authUsername || undefined,
    authToken: form.authToken || undefined
  })
  if (store.repo) {
    repoChecked.value = true
    await initRepo()
  }
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
</script>
