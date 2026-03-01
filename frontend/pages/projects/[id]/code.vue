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

    <!-- No repo configured -->
    <div v-if="!projectStore.currentProject?.gitHubRepo && !projectStore.loading"
      class="bg-gray-900 border border-gray-800 rounded-xl p-8 text-center">
      <svg class="w-12 h-12 text-gray-600 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
      </svg>
      <p class="text-gray-400 font-medium">No GitHub repository configured for this project.</p>
      <p class="text-gray-500 text-sm mt-1">Set a GitHub repository in the project settings to enable code browsing.
      </p>
    </div>

    <template v-else>
      <!-- Tabs -->
      <div class="flex gap-1 mb-6 border-b border-gray-800">
        <button v-for="tab in tabs" :key="tab.id" @click="activeTab = tab.id" :class="[
          'px-4 py-2 text-sm font-medium rounded-t transition-colors',
          activeTab === tab.id
            ? 'text-white border-b-2 border-brand-500'
            : 'text-gray-500 hover:text-gray-300'
        ]">
          {{ tab.label }}
        </button>
      </div>

      <!-- Error -->
      <div v-if="gitStore.error" class="mb-4 p-3 bg-red-900/30 border border-red-800 rounded-lg text-red-400 text-sm">
        {{ gitStore.error }}
      </div>

      <!-- Commits Tab -->
      <div v-if="activeTab === 'commits'">
        <!-- Branch selector -->
        <div class="flex items-center gap-3 mb-4">
          <select v-model="selectedRef"
            class="bg-gray-900 border border-gray-700 text-white rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:border-brand-500">
            <option v-for="b in gitStore.branches" :key="b.name" :value="b.name">{{ b.name }}</option>
          </select>
          <button @click="loadCommits"
            class="px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg text-sm transition-colors">
            Refresh
          </button>
        </div>

        <!-- Loading -->
        <div v-if="gitStore.loading" class="flex justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
        </div>

        <!-- Commits list -->
        <div v-else class="space-y-2">
          <div v-if="gitStore.commits.length === 0" class="text-center py-10 text-gray-500">No commits found.</div>
          <a v-for="commit in gitStore.commits" :key="commit.sha" :href="commit.url" target="_blank"
            class="block bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 transition-colors">
            <div class="flex items-start justify-between gap-4">
              <p class="text-sm text-white font-medium line-clamp-2">{{ firstLine(commit.message) }}</p>
              <code
                class="text-xs text-brand-400 font-mono whitespace-nowrap bg-brand-900/20 px-2 py-0.5 rounded">{{
                  commit.sha.substring(0, 7) }}</code>
            </div>
            <div class="mt-1.5 flex items-center gap-2 text-xs text-gray-500">
              <span>{{ commit.author }}</span>
              <span>·</span>
              <span>{{ formatDate(commit.date) }}</span>
            </div>
          </a>
        </div>

        <!-- Pagination -->
        <div class="flex justify-between mt-4">
          <button v-if="commitsPage > 1" @click="changeCommitsPage(commitsPage - 1)"
            class="px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg text-sm transition-colors">
            ← Previous
          </button>
          <span v-else></span>
          <button v-if="gitStore.commits.length === 30" @click="changeCommitsPage(commitsPage + 1)"
            class="px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg text-sm transition-colors">
            Next →
          </button>
        </div>
      </div>

      <!-- Browse Tab -->
      <div v-if="activeTab === 'browse'">
        <!-- Toolbar -->
        <div class="flex items-center gap-3 mb-4 flex-wrap">
          <select v-model="selectedRef" @change="navigateTo('')"
            class="bg-gray-900 border border-gray-700 text-white rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:border-brand-500">
            <option v-for="b in gitStore.branches" :key="b.name" :value="b.name">{{ b.name }}</option>
          </select>

          <!-- Breadcrumb -->
          <div class="flex items-center gap-1 text-sm text-gray-400 flex-wrap">
            <button @click="navigateTo('')" class="hover:text-white transition-colors font-mono">/</button>
            <template v-for="(segment, idx) in pathSegments" :key="idx">
              <span class="text-gray-600">/</span>
              <button @click="navigateTo(pathSegments.slice(0, idx + 1).join('/'))"
                class="hover:text-white transition-colors font-mono">
                {{ segment }}
              </button>
            </template>
          </div>
        </div>

        <!-- Loading -->
        <div v-if="gitStore.loading" class="flex justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
        </div>

        <!-- File content view -->
        <div v-else-if="gitStore.blob" class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div class="flex items-center justify-between px-4 py-3 border-b border-gray-800">
            <span class="text-sm font-mono text-gray-300">{{ gitStore.blob.name }}</span>
            <span class="text-xs text-gray-500">{{ formatSize(gitStore.blob.size) }}</span>
          </div>
          <pre
            class="p-4 text-sm font-mono text-gray-300 overflow-auto max-h-[60vh] whitespace-pre-wrap break-all">{{ decodedContent }}</pre>
        </div>

        <!-- Directory listing -->
        <div v-else class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div v-if="gitStore.treeEntries.length === 0 && !gitStore.loading"
            class="text-center py-10 text-gray-500">
            Empty directory.
          </div>
          <div v-for="(entry, idx) in gitStore.treeEntries" :key="entry.sha"
            :class="['flex items-center gap-3 px-4 py-2.5 hover:bg-gray-800 transition-colors cursor-pointer', idx !== 0 && 'border-t border-gray-800']"
            @click="handleEntryClick(entry)">
            <!-- Directory icon -->
            <svg v-if="entry.type === 'dir'" class="w-4 h-4 text-blue-400 flex-shrink-0" fill="currentColor"
              viewBox="0 0 20 20">
              <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
            </svg>
            <!-- File icon -->
            <svg v-else class="w-4 h-4 text-gray-500 flex-shrink-0" fill="none" stroke="currentColor"
              viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <span class="text-sm font-mono text-gray-200 flex-1">{{ entry.name }}</span>
            <span v-if="entry.type === 'file' && entry.size !== undefined" class="text-xs text-gray-500">{{
              formatSize(entry.size) }}</span>
          </div>
        </div>
      </div>

      <!-- Branches Tab -->
      <div v-if="activeTab === 'branches'">
        <!-- Loading -->
        <div v-if="gitStore.loading" class="flex justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
        </div>

        <div v-else class="space-y-2">
          <div v-if="gitStore.branches.length === 0" class="text-center py-10 text-gray-500">No branches found.</div>
          <div v-for="branch in gitStore.branches" :key="branch.name"
            class="bg-gray-900 border border-gray-800 rounded-xl p-4 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <svg class="w-4 h-4 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              <span class="font-mono text-sm text-white">{{ branch.name }}</span>
              <span v-if="branch.isProtected"
                class="text-xs bg-yellow-900/30 text-yellow-400 border border-yellow-800/50 px-2 py-0.5 rounded-full">protected</span>
            </div>
            <code class="text-xs text-gray-500 font-mono">{{ branch.sha.substring(0, 7) }}</code>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useGitStore } from '~/stores/git'
import type { GitTreeEntry } from '~/types'

const route = useRoute()
const id = route.params.id as string

const projectStore = useProjectsStore()
const gitStore = useGitStore()

const activeTab = ref<'commits' | 'browse' | 'branches'>('browse')
const selectedRef = ref('')
const currentPath = ref('')
const commitsPage = ref(1)

const tabs = [
  { id: 'browse', label: 'Browse' },
  { id: 'commits', label: 'Commits' },
  { id: 'branches', label: 'Branches' },
]

const pathSegments = computed(() =>
  currentPath.value ? currentPath.value.split('/').filter(Boolean) : []
)

const decodedContent = computed(() => {
  if (!gitStore.blob) return ''
  if (gitStore.blob.encoding === 'base64') {
    try {
      return atob(gitStore.blob.content.replace(/\n/g, ''))
    } catch {
      return gitStore.blob.content
    }
  }
  return gitStore.blob.content
})

async function loadBranches() {
  await gitStore.fetchBranches(id)
  if (!selectedRef.value && gitStore.branches.length > 0) {
    selectedRef.value = gitStore.branches[0].name
  }
}

async function loadCommits() {
  await gitStore.fetchCommits(id, selectedRef.value || undefined, commitsPage.value)
}

async function changeCommitsPage(page: number) {
  commitsPage.value = page
  await loadCommits()
}

async function navigateTo(path: string) {
  currentPath.value = path
  gitStore.blob = null
  await gitStore.fetchTree(id, selectedRef.value || undefined, path || undefined)
}

async function handleEntryClick(entry: GitTreeEntry) {
  if (entry.type === 'dir') {
    await navigateTo(entry.path)
  } else {
    await gitStore.fetchBlob(id, entry.path, selectedRef.value || undefined)
  }
}

function firstLine(message: string) {
  return message.split('\n')[0]
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

watch(activeTab, async (tab) => {
  if (tab === 'commits') await loadCommits()
  if (tab === 'branches') await gitStore.fetchBranches(id)
})

onMounted(async () => {
  if (!projectStore.currentProject) {
    await projectStore.fetchProject(id)
  }
  gitStore.reset()
  await loadBranches()
  await navigateTo('')
})
</script>
