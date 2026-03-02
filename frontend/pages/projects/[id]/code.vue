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

      <!-- Review session bar -->
      <div v-if="reviewComments.length > 0"
        class="mb-4 bg-brand-900/20 border border-brand-800/40 rounded-xl px-4 py-2.5 flex items-center gap-3">
        <div class="w-2 h-2 rounded-full bg-brand-500 animate-pulse shrink-0"></div>
        <span class="text-sm text-brand-300">
          Review in progress —
          <strong class="text-white">{{ reviewComments.length }}</strong>
          comment{{ reviewComments.length !== 1 ? 's' : '' }} across
          <strong class="text-white">{{ reviewedFilesCount }}</strong>
          file{{ reviewedFilesCount !== 1 ? 's' : '' }}
        </span>
        <div class="ml-auto flex items-center gap-2">
          <button @click="discardReview"
            class="text-xs text-gray-500 hover:text-red-400 transition-colors">
            Discard
          </button>
          <button @click="finishReview" :disabled="savingReview"
            class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-4 py-1.5 rounded-lg transition-colors disabled:opacity-50">
            {{ savingReview ? 'Creating issue…' : 'Finish Review' }}
          </button>
        </div>
      </div>

      <!-- Code browser tab -->
      <div v-if="activeTab === 'code'" class="flex gap-4 items-start">
        <!-- File tree -->
        <div class="w-64 shrink-0 bg-gray-900 border border-gray-800 rounded-xl overflow-hidden sticky top-4">
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
          <div v-else class="overflow-y-auto max-h-[calc(100vh-8rem)]">
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
              <div class="flex items-center gap-3">
                <button v-if="isMdFile" @click="showRenderedMd = !showRenderedMd"
                  class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-2 py-0.5 rounded transition-colors">
                  {{ showRenderedMd ? 'Source' : 'Preview' }}
                </button>
                <span class="text-xs text-gray-500">{{ formatSize(store.blob.size) }}</span>
              </div>
            </div>
            <!-- Binary notice -->
            <div v-if="store.blob.isBinary" class="p-6 text-center text-gray-400 text-sm">
              Binary file — preview not available
            </div>
            <!-- Markdown preview -->
            <div v-else-if="isMdFile && showRenderedMd"
              class="p-6 prose prose-invert prose-sm max-w-none"
              v-html="renderedMd"></div>
            <!-- Code with line numbers -->
            <div v-else class="overflow-x-auto">
              <table class="w-full border-collapse text-sm font-mono leading-relaxed">
                <tbody>
                  <tr v-for="(line, idx) in fileLines" :key="idx"
                    :class="isLineInSelection(idx + 1) ? 'bg-brand-900/30' : ''">
                    <td class="select-none text-right text-gray-600 pr-3 pl-3 w-12 border-r border-gray-800/60 align-top cursor-pointer hover:text-brand-400 transition-colors"
                      @click="onLineNumberClick(idx + 1, $event)">
                      {{ idx + 1 }}
                    </td>
                    <td class="pl-4 pr-4 whitespace-pre align-top" v-html="highlightedLines[idx] ?? ''"></td>
                  </tr>
                </tbody>
              </table>
              <!-- Selection action bar -->
              <div v-if="selectedLines && !showCommentPanel"
                class="sticky bottom-0 border-t border-gray-800 bg-gray-900 px-4 py-2 flex items-center gap-3">
                <span class="text-xs text-gray-400">
                  <template v-if="selectedLines.start === selectedLines.end">Line {{ selectedLines.start }}</template>
                  <template v-else>Lines {{ selectedLines.start }}–{{ selectedLines.end }}</template>
                  selected
                </span>
                <button @click="showCommentPanel = true"
                  class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1 rounded transition-colors">
                  Add Comment
                </button>
                <button @click="clearSelection" class="text-xs text-gray-500 hover:text-gray-300 transition-colors">
                  Clear
                </button>
              </div>
              <!-- Comment input -->
              <div v-if="showCommentPanel && selectedLines"
                class="sticky bottom-0 border-t border-gray-800 bg-gray-900 p-3">
                <p class="text-xs text-gray-500 mb-2">
                  Comment on
                  <template v-if="selectedLines.start === selectedLines.end">line {{ selectedLines.start }}</template>
                  <template v-else>lines {{ selectedLines.start }}–{{ selectedLines.end }}</template>
                  of <code class="text-brand-400">{{ store.blob.path }}</code>
                </p>
                <div class="flex items-start gap-2">
                  <textarea v-model="commentText" rows="3"
                    placeholder="Add a comment… (Ctrl+Enter to submit)"
                    class="flex-1 bg-gray-800 border border-gray-700 rounded-lg text-sm text-white px-3 py-2 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none"
                    @keydown.ctrl.enter="addReviewComment"></textarea>
                  <div class="flex flex-col gap-1.5">
                    <button @click="addReviewComment" :disabled="!commentText.trim()"
                      class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
                      Add
                    </button>
                    <button @click="cancelComment"
                      class="text-xs bg-gray-700 hover:bg-gray-600 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
                      Cancel
                    </button>
                  </div>
                </div>
              </div>
            </div>
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
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import hljs from 'highlight.js'
import { useGitStore } from '~/stores/git'
import { useIssuesStore } from '~/stores/issues'
import { useAuthStore } from '~/stores/auth'
import { IssueType, IssuePriority, IssueStatus } from '~/types'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const store = useGitStore()
const issuesStore = useIssuesStore()
const authStore = useAuthStore()

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

// Handle browser back/forward navigation by syncing state with URL query params.
// State is updated before router.push() is called, so these comparisons prevent
// duplicate fetches when navigations are initiated within the component.
watch(() => route.query, async (query) => {
  if (!store.repo || !repoChecked.value) return
  const path = (query.path as string) || ''
  const file = (query.file as string) || ''
  if (path !== currentPath.value) {
    currentPath.value = path
    selectedFile.value = ''
    store.blob = null
    await store.fetchTree(id, selectedBranch.value, path)
  }
  if (file !== selectedFile.value) {
    if (file) {
      selectedFile.value = file
      await store.fetchBlob(id, file, selectedBranch.value)
    } else {
      selectedFile.value = ''
      store.blob = null
    }
  }
}, { deep: true })

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
  // Restore navigation state from URL query params (e.g. on page reload or direct link)
  const queryPath = (route.query.path as string) || ''
  const queryFile = (route.query.file as string) || ''
  if (queryPath) {
    currentPath.value = queryPath
    await store.fetchTree(id, selectedBranch.value, queryPath)
  }
  if (queryFile) {
    selectedFile.value = queryFile
    await store.fetchBlob(id, queryFile, selectedBranch.value)
  }
}

async function onBranchChange() {
  currentPath.value = ''
  selectedFile.value = ''
  store.blob = null
  await store.fetchTree(id, selectedBranch.value, '')
  commitSkip.value = 0
  if (activeTab.value === 'commits')
    await store.fetchCommits(id, selectedBranch.value, 0, commitTake)
  router.push({ query: {} })
}

async function onEntryClick(entry: { type: string; path: string }) {
  if (entry.type === 'tree') {
    currentPath.value = entry.path
    selectedFile.value = ''
    store.blob = null
    await store.fetchTree(id, selectedBranch.value, entry.path)
    router.push({ query: { path: entry.path } })
  } else {
    selectedFile.value = entry.path
    await store.fetchBlob(id, entry.path, selectedBranch.value)
    // Use undefined for empty path so the query param is omitted from the URL
    router.push({ query: { path: currentPath.value ? currentPath.value : undefined, file: entry.path } })
  }
}

async function navigateTo(path: string) {
  currentPath.value = path
  selectedFile.value = ''
  store.blob = null
  await store.fetchTree(id, selectedBranch.value, path)
  // Use undefined for empty path so the query param is omitted from the URL
  router.push({ query: { path: path ? path : undefined } })
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

// Markdown support
const showRenderedMd = ref(false)
const isMdFile = computed(() => {
  const p = store.blob?.path.toLowerCase() ?? ''
  return p.endsWith('.md') || p.endsWith('.markdown')
})
const renderedMd = computed(() => {
  if (!store.blob?.content) return ''
  return DOMPurify.sanitize(marked.parse(store.blob.content) as string)
})

// Line numbers
const fileLines = computed(() => store.blob?.content.split('\n') ?? [])

// Language map: file extension → highlight.js language alias
const EXT_TO_LANG: Record<string, string> = {
  ts: 'typescript', tsx: 'typescript',
  js: 'javascript', jsx: 'javascript', mjs: 'javascript', cjs: 'javascript',
  vue: 'html',
  py: 'python',
  cs: 'csharp',
  java: 'java',
  go: 'go',
  rs: 'rust',
  rb: 'ruby',
  php: 'php',
  css: 'css', scss: 'scss', less: 'less',
  html: 'html', htm: 'html',
  xml: 'xml', svg: 'xml',
  json: 'json', jsonc: 'json',
  yaml: 'yaml', yml: 'yaml',
  sh: 'bash', bash: 'bash', zsh: 'bash',
  md: 'markdown',
  sql: 'sql',
  cpp: 'cpp', cc: 'cpp', cxx: 'cpp',
  c: 'c', h: 'c',
  kt: 'kotlin', kts: 'kotlin',
  swift: 'swift',
  r: 'r',
  tf: 'hcl', hcl: 'hcl',
  toml: 'toml',
  ini: 'ini',
  dockerfile: 'dockerfile',
}

const highlightedLines = computed(() => {
  const content = store.blob?.content
  if (!content) return []
  const path = store.blob?.path.toLowerCase() ?? ''
  const filename = path.split('/').pop() ?? ''
  const ext = filename.includes('.') ? filename.split('.').pop()! : filename
  const lang = EXT_TO_LANG[ext]
  let highlighted: string
  try {
    if (lang && hljs.getLanguage(lang)) {
      highlighted = hljs.highlight(content, { language: lang }).value
    } else {
      highlighted = hljs.highlightAuto(content).value
    }
    highlighted = DOMPurify.sanitize(highlighted)
  } catch {
    highlighted = content.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  }
  return highlighted.split('\n')
})

// Line selection
const selectionStart = ref<number | null>(null)
const selectionEnd = ref<number | null>(null)
const showCommentPanel = ref(false)
const commentText = ref('')

const selectedLines = computed(() => {
  if (selectionStart.value === null) return null
  const s = Math.min(selectionStart.value, selectionEnd.value ?? selectionStart.value)
  const e = Math.max(selectionStart.value, selectionEnd.value ?? selectionStart.value)
  return { start: s, end: e }
})

function isLineInSelection(lineNum: number) {
  if (!selectedLines.value) return false
  return lineNum >= selectedLines.value.start && lineNum <= selectedLines.value.end
}

function onLineNumberClick(lineNum: number, event: MouseEvent) {
  if (event.shiftKey && selectionStart.value !== null) {
    selectionEnd.value = lineNum
  } else {
    selectionStart.value = lineNum
    selectionEnd.value = lineNum
    showCommentPanel.value = false
    commentText.value = ''
  }
}

function clearSelection() {
  selectionStart.value = null
  selectionEnd.value = null
  showCommentPanel.value = false
  commentText.value = ''
}

function cancelComment() {
  showCommentPanel.value = false
  commentText.value = ''
}

// Review session
interface ReviewComment {
  filePath: string
  lines: { start: number; end: number }
  comment: string
  snippet: string
}

const reviewComments = ref<ReviewComment[]>([])
const savingReview = ref(false)
const reviewedFilesCount = computed(() => new Set(reviewComments.value.map(c => c.filePath)).size)

function addReviewComment() {
  if (!commentText.value.trim() || !selectedLines.value || !store.blob) return
  const lines = fileLines.value
  const snippet = lines.slice(selectedLines.value.start - 1, selectedLines.value.end).join('\n')
  reviewComments.value.push({
    filePath: store.blob.path,
    lines: { ...selectedLines.value },
    comment: commentText.value.trim(),
    snippet
  })
  clearSelection()
}

function discardReview() {
  reviewComments.value = []
}

async function finishReview() {
  if (reviewComments.value.length === 0) return
  savingReview.value = true
  try {
    const body = reviewComments.value.map(c => {
      const lineRange = c.lines.start === c.lines.end
        ? `line ${c.lines.start}`
        : `lines ${c.lines.start}–${c.lines.end}`
      const ext = c.filePath.split('.').pop() ?? ''
      return `### \`${c.filePath}\` (${lineRange})\n\`\`\`${ext}\n${c.snippet}\n\`\`\`\n\n${c.comment}`
    }).join('\n\n---\n\n')
    const now = new Date()
    const title = `Code Review – ${selectedBranch.value} – ${now.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`
    const newIssue = await issuesStore.createIssue(id, {
      title,
      body,
      type: IssueType.Issue,
      priority: IssuePriority.Medium,
      status: IssueStatus.Backlog
    })
    if (newIssue && authStore.user) {
      await issuesStore.addAssignee(newIssue.id, { userId: authStore.user.id })
    }
    reviewComments.value = []
    if (newIssue) {
      router.push(`/projects/${id}/issues/${newIssue.id}`)
    }
  } finally {
    savingReview.value = false
  }
}

watch(() => store.blob?.path, () => {
  clearSelection()
  showRenderedMd.value = false
})
</script>
