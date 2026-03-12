<template>
  <div class="p-8">
    <!-- Breadcrumb + Header -->
    <div class="flex items-center gap-2 mb-6">
      <NuxtLink :to="`/projects/${id}`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">{{ projectsStore.currentProject?.name }}</NuxtLink>
      <span class="text-gray-600">/</span>
      <NuxtLink :to="`/projects/${id}/merge-requests`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">Merge Requests</NuxtLink>
      <span class="text-gray-600">/</span>
      <NuxtLink v-if="mr" :to="`/projects/${id}/merge-requests/${mrId}`" class="text-xl font-bold text-white truncate max-w-xs">{{ mr.title }}</NuxtLink>
      <div v-else class="h-7 w-48 bg-gray-800 rounded animate-pulse"></div>
    </div>

    <!-- Loading MR -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Error -->
    <div v-else-if="error" class="p-4 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
      {{ error }}
    </div>

    <template v-else-if="mr">
      <!-- MR metadata card -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
        <div class="flex flex-wrap items-start gap-4">
          <!-- Status badge -->
          <div class="flex items-center gap-2">
            <span v-if="mr.statusName === 'Open'"
              class="inline-flex items-center gap-1.5 text-sm px-3 py-1 rounded-full bg-green-900/40 border border-green-700 text-green-400">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
              </svg>
              Open
            </span>
            <span v-else-if="mr.statusName === 'Merged'"
              class="inline-flex items-center gap-1.5 text-sm px-3 py-1 rounded-full bg-purple-900/40 border border-purple-700 text-purple-400">
              <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 16 16">
                <path d="M5 3.25a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm0 2.122a2.25 2.25 0 10-1.5 0v.878A2.25 2.25 0 005.75 8.5h1.5v2.128a2.251 2.251 0 101.5 0V8.5h1.5a2.25 2.25 0 002.25-2.25v-.878a2.25 2.25 0 10-1.5 0v.878a.75.75 0 01-.75.75h-4.5A.75.75 0 015 6.25v-.878zm3.75 7.378a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm3-8.75a.75.75 0 11-1.5 0 .75.75 0 011.5 0z" />
              </svg>
              Merged
            </span>
            <span v-else
              class="inline-flex items-center gap-1.5 text-sm px-3 py-1 rounded-full bg-gray-800 border border-gray-700 text-gray-400">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M6 18L18 6M6 6l12 12" />
              </svg>
              Closed
            </span>
          </div>

          <!-- Branch flow -->
          <div class="flex items-center gap-2 text-sm">
            <code class="bg-gray-800 px-2 py-0.5 rounded text-gray-300">{{ mr.sourceBranch }}</code>
            <svg class="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 8l4 4m0 0l-4 4m4-4H3" />
            </svg>
            <code class="bg-gray-800 px-2 py-0.5 rounded text-gray-300">{{ mr.targetBranch }}</code>
          </div>

          <!-- CI badge -->
          <span v-if="mr.lastCiCdRunStatusName"
            :class="ciStatusClass(mr.lastCiCdRunStatusName)"
            class="text-xs px-2 py-0.5 rounded-full">
            CI: {{ mr.lastCiCdRunStatusName }}
          </span>

          <!-- Auto-merge badge -->
          <span v-if="mr.autoMergeEnabled"
            class="text-xs bg-blue-900/30 text-blue-400 px-2 py-0.5 rounded-full">
            Auto-merge
          </span>

          <!-- Action buttons -->
          <div v-if="mr.statusName === 'Open'" class="ml-auto flex items-center gap-2">
            <button @click="mergeMr"
              :disabled="actionLoading"
              class="bg-purple-700 hover:bg-purple-600 text-white text-sm px-4 py-1.5 rounded-lg transition-colors disabled:opacity-50">
              <span v-if="actionLoading === 'merge'">Merging…</span>
              <span v-else>Merge</span>
            </button>
            <button @click="closeMr"
              :disabled="!!actionLoading"
              class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 transition-colors disabled:opacity-50">
              Close
            </button>
          </div>
          <div v-else-if="mr.statusName === 'Closed'" class="ml-auto">
            <button @click="reopenMr"
              :disabled="!!actionLoading"
              class="text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 transition-colors disabled:opacity-50">
              Reopen
            </button>
          </div>
        </div>

        <!-- Description -->
        <p v-if="mr.description" class="mt-4 text-sm text-gray-400">{{ mr.description }}</p>

        <!-- Meta row -->
        <div class="mt-4 flex flex-wrap gap-x-5 gap-y-1 text-xs text-gray-500">
          <span>Created {{ formatDate(mr.createdAt) }}</span>
          <span v-if="mr.mergedAt">Merged {{ formatDate(mr.mergedAt) }}</span>
          <span v-if="mr.mergeCommitSha">
            Merge commit:
            <code class="bg-gray-800 px-1 rounded text-gray-400">{{ mr.mergeCommitSha.slice(0, 8) }}</code>
          </span>
        </div>

        <!-- Action error -->
        <div v-if="actionError" class="mt-3 p-3 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
          {{ actionError }}
        </div>
      </div>

      <!-- Diff section -->
      <div>
        <!-- Diff loading -->
        <div v-if="diffLoading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          <span class="ml-3 text-sm text-gray-400">Loading diff…</span>
        </div>

        <!-- Diff error -->
        <div v-else-if="diffError" class="p-4 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
          {{ diffError }}
        </div>

        <!-- No diff (branches identical or no changes) -->
        <div v-else-if="diffLoaded && diff.length === 0"
          class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
          <p class="text-gray-500 text-sm">No differences found between
            <code class="text-gray-400">{{ mr.targetBranch }}</code> and
            <code class="text-gray-400">{{ mr.sourceBranch }}</code>.
          </p>
        </div>

        <!-- Diff content -->
        <template v-else-if="diff.length">
          <!-- Diff summary bar -->
          <div class="mb-4 flex items-center gap-4 text-sm text-gray-400">
            <span>{{ diff.length }} file{{ diff.length !== 1 ? 's' : '' }} changed</span>
            <span class="text-green-400">+{{ totalAdded }}</span>
            <span class="text-red-400">-{{ totalRemoved }}</span>
            <div class="ml-auto flex items-center gap-2">
              <button v-if="collapsedFiles.size > 0" @click="collapsedFiles = new Set()"
                class="text-xs text-gray-500 hover:text-gray-300 transition-colors">
                Expand all
              </button>
              <button v-if="collapsedFiles.size < diff.length" @click="collapsedFiles = new Set(diff.map(f => f.newPath))"
                class="text-xs text-gray-500 hover:text-gray-300 transition-colors">
                Collapse all
              </button>
            </div>
          </div>

          <!-- Diff files -->
          <div v-for="file in diff" :key="file.newPath" :id="`file-${fileId(file.newPath)}`" class="mb-4">
            <!-- File header -->
            <div class="flex items-center gap-2 bg-gray-900 border border-gray-800 px-4 py-2.5 cursor-pointer"
              :class="collapsedFiles.has(file.newPath) ? 'rounded-xl' : 'rounded-t-xl'"
              @click="toggleFileCollapse(file.newPath)">
              <svg class="w-3.5 h-3.5 text-gray-500 shrink-0 transition-transform"
                :class="collapsedFiles.has(file.newPath) ? '-rotate-90' : ''"
                fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
              </svg>
              <span :class="statusColor(file.status)" class="shrink-0 font-mono text-xs font-bold">
                {{ statusLetter(file.status) }}
              </span>
              <span class="font-mono text-sm text-gray-200 flex-1 truncate">{{ displayPath(file) }}</span>
              <span v-if="file.isBinary" class="text-xs text-gray-500 ml-2">binary</span>
              <span v-else class="text-xs text-gray-500 ml-2">
                <span class="text-green-400">+{{ file.addedLines }}</span>
                <span class="mx-1 text-gray-600">/</span>
                <span class="text-red-400">-{{ file.removedLines }}</span>
              </span>
              <button v-if="originallyTooLarge.has(file.newPath) && !expandedFiles.has(file.newPath)"
                @click.stop="expandFile(file)"
                class="text-xs text-brand-400 hover:text-brand-300 ml-2 transition-colors">
                Load diff
              </button>
            </div>

            <!-- Binary notice -->
            <div v-if="!collapsedFiles.has(file.newPath) && file.isBinary"
              class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4 text-center text-sm text-gray-500">
              Binary file — preview not available
            </div>

            <!-- Too-large skeleton -->
            <div v-else-if="!collapsedFiles.has(file.newPath) && originallyTooLarge.has(file.newPath) && !expandedFiles.has(file.newPath)"
              class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4">
              <div class="space-y-2">
                <div v-for="n in 6" :key="n" class="h-4 bg-gray-800 rounded animate-pulse"
                  :style="{ width: `${30 + (n * 13 % 55)}%` }"></div>
              </div>
              <p class="text-xs text-gray-500 mt-3 text-center">
                Large file — click "Load diff" to view
              </p>
            </div>

            <!-- Loading expanded file -->
            <div v-else-if="!collapsedFiles.has(file.newPath) && expandingFiles.has(file.newPath)"
              class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4 text-center text-sm text-gray-400">
              <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
            </div>

            <!-- Diff hunks -->
            <div v-else-if="!collapsedFiles.has(file.newPath)"
              class="border border-t-0 border-gray-800 rounded-b-xl overflow-x-auto">
              <table class="w-full border-collapse text-xs font-mono">
                <tbody>
                  <template v-for="(hunk, hunkIdx) in file.hunks" :key="hunkIdx">
                    <!-- Hunk header -->
                    <tr class="bg-blue-950/30">
                      <td colspan="3" class="px-3 py-1 text-blue-400/70 select-none text-left">
                        @@ -{{ hunk.oldStart }},{{ hunk.oldCount }} +{{ hunk.newStart }},{{ hunk.newCount }} @@
                        <span class="text-gray-500 ml-2">{{ hunk.header }}</span>
                      </td>
                    </tr>
                    <!-- Lines -->
                    <tr v-for="(line, lineIdx) in hunk.lines" :key="lineIdx"
                      :class="lineRowClass(line.lineType)">
                      <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top">
                        {{ line.oldLineNumber ?? '' }}
                      </td>
                      <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top">
                        {{ line.newLineNumber ?? '' }}
                      </td>
                      <td class="pl-2 pr-3 whitespace-pre leading-relaxed"
                        v-html="highlightLine(file.newPath, line)"></td>
                    </tr>
                  </template>
                  <tr v-if="!file.hunks.length">
                    <td colspan="3" class="p-4 text-center text-gray-500 text-xs">No changes</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </template>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import DOMPurify from 'dompurify'
import hljs from 'highlight.js'
import type { GitDiffFile } from '~/types'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const mrId = route.params.mrId as string
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
  autoMergeEnabled: boolean
  lastKnownSourceSha: string | null
  lastCiCdRunId: string | null
  lastCiCdRunStatus: number | null
  lastCiCdRunStatusName: string | null
  createdAt: string
  updatedAt: string
  mergedAt: string | null
  mergeCommitSha: string | null
}

const mr = ref<MergeRequestDto | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const actionLoading = ref<string | null>(null)
const actionError = ref<string | null>(null)

// Diff state
const diff = ref<GitDiffFile[]>([])
const diffLoading = ref(false)
const diffError = ref<string | null>(null)
const diffLoaded = ref(false)
const collapsedFiles = ref(new Set<string>())
const expandedFiles = ref(new Set<string>())
const expandingFiles = ref(new Set<string>())
const originallyTooLarge = ref(new Set<string>())

const COLLAPSE_THRESHOLD = 5

const totalAdded = computed(() => diff.value.reduce((s, f) => s + f.addedLines, 0))
const totalRemoved = computed(() => diff.value.reduce((s, f) => s + f.removedLines, 0))

async function fetchMr() {
  loading.value = true
  error.value = null
  try {
    mr.value = await api.get<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mrId}`)
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load merge request'
  } finally {
    loading.value = false
  }
}

async function loadDiff() {
  if (!mr.value) return
  diffLoading.value = true
  diffError.value = null
  diffLoaded.value = false
  try {
    const params = new URLSearchParams({
      base_: mr.value.targetBranch,
      compare: mr.value.sourceBranch,
      context: '3',
    })
    diff.value = await api.get<GitDiffFile[]>(`/api/projects/${id}/git/diff?${params}`)
    originallyTooLarge.value = new Set(diff.value.filter(f => f.isTooLarge).map(f => f.newPath))
    if (diff.value.length > COLLAPSE_THRESHOLD) {
      collapsedFiles.value = new Set(diff.value.map(f => f.newPath))
    } else {
      collapsedFiles.value = new Set()
    }
    diffLoaded.value = true
  } catch (e: unknown) {
    diffError.value = e instanceof Error ? e.message : 'Failed to load diff'
  } finally {
    diffLoading.value = false
  }
}

async function expandFile(file: GitDiffFile) {
  if (!mr.value) return
  expandingFiles.value = new Set([...expandingFiles.value, file.newPath])
  try {
    const params = new URLSearchParams({
      base_: mr.value.targetBranch,
      compare: mr.value.sourceBranch,
      context: '3',
      noLimit: 'true',
    })
    const fullDiff = await api.get<GitDiffFile[]>(`/api/projects/${id}/git/diff?${params}`)
    const updated = fullDiff.find(f => f.newPath === file.newPath)
    if (updated) {
      const idx = diff.value.findIndex(f => f.newPath === file.newPath)
      if (idx !== -1) diff.value[idx] = updated
    }
    expandedFiles.value = new Set([...expandedFiles.value, file.newPath])
  } finally {
    expandingFiles.value.delete(file.newPath)
    expandingFiles.value = new Set(expandingFiles.value)
  }
}

function toggleFileCollapse(filePath: string) {
  const next = new Set(collapsedFiles.value)
  if (next.has(filePath)) {
    next.delete(filePath)
  } else {
    next.add(filePath)
  }
  collapsedFiles.value = next
}

async function mergeMr() {
  actionLoading.value = 'merge'
  actionError.value = null
  try {
    mr.value = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mrId}/merge`, {})
  } catch (e: unknown) {
    actionError.value = e instanceof Error ? e.message : 'Merge failed'
  } finally {
    actionLoading.value = null
  }
}

async function closeMr() {
  actionLoading.value = 'close'
  actionError.value = null
  try {
    mr.value = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mrId}/close`, {})
  } catch (e: unknown) {
    actionError.value = e instanceof Error ? e.message : 'Failed to close MR'
  } finally {
    actionLoading.value = null
  }
}

async function reopenMr() {
  actionLoading.value = 'reopen'
  actionError.value = null
  try {
    mr.value = await api.post<MergeRequestDto>(`/api/projects/${id}/merge-requests/${mrId}/reopen`, {})
  } catch (e: unknown) {
    actionError.value = e instanceof Error ? e.message : 'Failed to reopen MR'
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

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

// ── Diff rendering helpers ────────────────────────────────────

function lineRowClass(type: string) {
  if (type === 'added') return 'bg-green-950/40'
  if (type === 'removed') return 'bg-red-950/40'
  return 'bg-transparent'
}

function statusColor(status: string) {
  switch (status.toLowerCase()) {
    case 'added': return 'text-green-400'
    case 'deleted': return 'text-red-400'
    case 'renamed': return 'text-yellow-400'
    case 'modified': return 'text-blue-400'
    default: return 'text-gray-400'
  }
}

function statusLetter(status: string) {
  switch (status.toLowerCase()) {
    case 'added': return 'A'
    case 'deleted': return 'D'
    case 'renamed': return 'R'
    case 'modified': return 'M'
    default: return '?'
  }
}

function displayPath(file: GitDiffFile) {
  if (file.status.toLowerCase() === 'renamed' && file.oldPath !== file.newPath)
    return `${file.oldPath} → ${file.newPath}`
  return file.newPath
}

function fileId(path: string) {
  return path.replace(/[^a-zA-Z0-9]/g, '-')
}

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

function highlightLine(filePath: string, line: { content: string }): string {
  const ext = (filePath.split('/').pop() ?? '').split('.').pop() ?? ''
  const lang = EXT_TO_LANG[ext.toLowerCase()]
  const raw = line.content
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
  try {
    if (lang && hljs.getLanguage(lang)) {
      const highlighted = hljs.highlight(line.content, { language: lang }).value
      return DOMPurify.sanitize(highlighted)
    }
  } catch {
    // fall through to plain
  }
  return raw
}

onMounted(async () => {
  projectsStore.fetchProject(id)
  await fetchMr()
  if (mr.value) {
    await loadDiff()
  }
})
</script>

<style>@import 'highlight.js/styles/github-dark.css';</style>
