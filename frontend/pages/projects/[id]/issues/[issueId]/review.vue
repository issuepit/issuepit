<template>
  <div class="p-6 max-w-6xl">
    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-sm text-gray-500 mb-6">
      <NuxtLink :to="`/projects/${id}`" class="hover:text-gray-300">Project</NuxtLink>
      <span>/</span>
      <NuxtLink :to="`/projects/${id}/issues`" class="hover:text-gray-300">Issues</NuxtLink>
      <span>/</span>
      <NuxtLink :to="`/projects/${id}/issues/${issueId}`" class="hover:text-gray-300 text-gray-400">
        #{{ issueStore.currentIssue?.number ?? '…' }}
      </NuxtLink>
      <span>/</span>
      <span class="text-gray-300">Code Review</span>
    </div>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-xl font-bold text-white flex items-center gap-2">
          <svg class="w-5 h-5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
          </svg>
          Code Review
        </h1>
        <p v-if="diff" class="text-sm text-gray-500 mt-1">
          <span class="text-gray-400 font-mono text-xs bg-gray-800 px-1.5 py-0.5 rounded">{{ diff.baseBranch }}</span>
          <span class="mx-2 text-gray-600">←</span>
          <span class="text-gray-400 font-mono text-xs bg-gray-800 px-1.5 py-0.5 rounded">{{ diff.headBranch }}</span>
          <span class="ml-3 text-green-400 text-xs">+{{ diff.totalAdditions }}</span>
          <span class="ml-1 text-red-400 text-xs">-{{ diff.totalDeletions }}</span>
        </p>
      </div>

      <!-- Submit Review button -->
      <button v-if="pendingComments.length > 0" @click="showSubmitModal = true"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        Submit Review ({{ pendingComments.length }})
      </button>
    </div>

    <!-- No branch configured -->
    <div v-if="!issueStore.currentIssue?.gitBranch"
      class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
      <svg class="w-10 h-10 text-gray-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
      </svg>
      <p class="text-gray-400 font-medium">No git branch linked to this issue</p>
      <p class="text-sm text-gray-600 mt-1">Set a git branch on the issue to enable code review.</p>
      <NuxtLink :to="`/projects/${id}/issues/${issueId}`"
        class="mt-4 inline-block text-brand-400 hover:text-brand-300 text-sm">
        ← Back to Issue
      </NuxtLink>
    </div>

    <!-- Load Diff button -->
    <div v-else-if="!diff && !loading"
      class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
      <svg class="w-10 h-10 text-gray-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
      </svg>
      <p class="text-gray-400 font-medium">
        Branch: <span class="text-brand-300 font-mono">{{ issueStore.currentIssue.gitBranch }}</span>
      </p>
      <p class="text-sm text-gray-600 mt-1">Load the diff to start reviewing the changes.</p>
      <div class="flex items-center justify-center gap-3 mt-4">
        <label class="text-sm text-gray-400">Base branch:</label>
        <input v-model="baseBranch" type="text"
          class="bg-gray-800 border border-gray-700 rounded-md px-2 py-1 text-sm text-white font-mono w-32 focus:outline-none focus:ring-1 focus:ring-brand-500" />
        <button @click="loadDiff"
          class="bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          Load Diff
        </button>
      </div>
      <p v-if="error" class="mt-4 text-sm text-red-400">{{ error }}</p>
    </div>

    <!-- Loading -->
    <div v-else-if="loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Diff viewer -->
    <template v-else-if="diff">
      <!-- Files list summary -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-3 mb-4 flex flex-wrap gap-2 text-xs">
        <a v-for="f in diff.files" :key="f.filename" :href="`#file-${fileAnchor(f.filename)}`"
          class="flex items-center gap-1 text-gray-400 hover:text-brand-300 transition-colors font-mono">
          <span :class="statusColor(f.status)" class="w-1.5 h-1.5 rounded-full shrink-0"></span>
          {{ f.filename }}
          <span class="text-green-500">+{{ f.additions }}</span>
          <span class="text-red-500">-{{ f.deletions }}</span>
        </a>
      </div>

      <!-- File diffs -->
      <div v-for="file in diff.files" :key="file.filename" :id="`file-${fileAnchor(file.filename)}`"
        class="mb-4 border border-gray-700 rounded-xl overflow-hidden">

        <!-- File header -->
        <div class="flex items-center justify-between px-4 py-2.5 bg-gray-900 border-b border-gray-700 cursor-pointer"
          @click="toggleFile(file.filename)">
          <div class="flex items-center gap-2 min-w-0">
            <svg class="w-3.5 h-3.5 text-gray-500 transition-transform shrink-0"
              :class="{ 'rotate-90': expandedFiles.has(file.filename) }"
              fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
            <span :class="statusColor(file.status)" class="w-2 h-2 rounded-full shrink-0"></span>
            <span class="font-mono text-sm text-gray-200 truncate">{{ file.filename }}</span>
          </div>
          <div class="flex items-center gap-3 shrink-0 ml-4 text-xs">
            <span class="text-green-400">+{{ file.additions }}</span>
            <span class="text-red-400">-{{ file.deletions }}</span>
            <span v-if="file.isBig" class="bg-yellow-900/40 text-yellow-300 px-1.5 py-0.5 rounded text-xs">Large file</span>
          </div>
        </div>

        <!-- File body -->
        <div v-if="expandedFiles.has(file.filename)">
          <!-- Big file skeleton / load button -->
          <div v-if="file.isBig && !loadedBigFiles.has(file.filename)" class="bg-gray-950">
            <div class="p-6 text-center">
              <p class="text-sm text-gray-400 mb-1">This file is large and was not loaded automatically.</p>
              <p class="text-xs text-gray-600 mb-4">
                {{ file.additions + file.deletions }} changed lines
              </p>
              <!-- Skeleton lines -->
              <div class="space-y-1 mb-4 opacity-30">
                <div v-for="n in 8" :key="n" class="h-4 bg-gray-700 rounded"
                  :style="{ width: `${30 + (n * 13) % 55}%`, marginLeft: `${(n * 7) % 20}%` }"></div>
              </div>
              <p v-if="bigFileErrors.get(file.filename)" class="text-sm text-red-400 mb-3">
                {{ bigFileErrors.get(file.filename) }}
              </p>
              <button @click="loadBigFile(file)"
                class="bg-brand-600 hover:bg-brand-700 text-white text-sm px-4 py-1.5 rounded-lg transition-colors">
                Load file diff
              </button>
            </div>
          </div>

          <!-- Actual diff hunks -->
          <div v-else class="bg-gray-950 font-mono text-xs overflow-x-auto">
            <div v-if="file.hunks.length === 0" class="text-gray-600 text-center py-6">
              Binary file or no diff available
            </div>
            <template v-for="(hunk, hi) in file.hunks" :key="hi">
              <!-- Hunk header -->
              <div class="px-4 py-1 bg-blue-950/30 text-blue-400 border-y border-blue-900/30 select-none">
                {{ hunk.header }}
              </div>
              <!-- Diff lines -->
              <div v-for="(line, li) in hunk.lines" :key="li">
                <div class="group relative"
                  :class="lineRowClass(line.type)">
                  <!-- Gutter: old line no -->
                  <span class="inline-block w-12 text-right pr-3 text-gray-600 select-none border-r border-gray-800">
                    {{ line.oldLineNo ?? '' }}
                  </span>
                  <!-- Gutter: new line no -->
                  <span class="inline-block w-12 text-right pr-3 text-gray-600 select-none border-r border-gray-800">
                    {{ line.newLineNo ?? '' }}
                  </span>
                  <!-- Content -->
                  <span class="pl-2 whitespace-pre" :class="lineContentClass(line.type)">{{ line.content }}</span>

                  <!-- Add comment button -->
                  <button
                    class="absolute right-2 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity bg-gray-700 hover:bg-brand-600 text-white rounded px-1.5 py-0.5 text-xs"
                    @click.stop="openCommentForm(file.filename, line)">
                    +
                  </button>
                </div>

                <!-- Inline pending comment form -->
                <div v-if="commentForm?.file === file.filename && commentForm?.lineKey === lineKey(line)"
                  class="bg-gray-900 border-l-2 border-brand-500 px-4 py-3">
                  <textarea v-model="commentForm.body" rows="3" placeholder="Leave a comment…"
                    class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none font-sans"></textarea>
                  <div class="flex gap-2 mt-2">
                    <button @click="addPendingComment(file.filename, line)"
                      class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1 rounded-md transition-colors">
                      Add comment
                    </button>
                    <button @click="commentForm = null"
                      class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1 rounded-md transition-colors">
                      Cancel
                    </button>
                  </div>
                </div>

                <!-- Submitted inline comments -->
                <div v-for="c in submittedCommentsByLine(file.filename, line)" :key="c.id"
                  class="bg-gray-900 border-l-2 border-green-600 px-4 py-3 text-sm text-gray-300">
                  <div class="flex items-center gap-2 mb-1">
                    <div class="w-5 h-5 rounded-full bg-brand-600 flex items-center justify-center text-xs font-bold text-white shrink-0">
                      {{ (c.authorName ?? 'U').charAt(0).toUpperCase() }}
                    </div>
                    <span class="text-xs text-gray-500">{{ c.authorName ?? 'User' }} · {{ formatDate(c.createdAt) }}</span>
                  </div>
                  <p class="whitespace-pre-wrap font-sans">{{ c.body }}</p>
                </div>

                <!-- Pending inline comments preview -->
                <div v-for="(pc, pci) in pendingCommentsByLine(file.filename, line)" :key="pci"
                  class="bg-gray-900 border-l-2 border-yellow-600 px-4 py-3 text-sm text-gray-300">
                  <div class="flex items-center justify-between mb-1">
                    <span class="text-xs text-yellow-500">Pending · draft comment</span>
                    <button @click="removePendingComment(pci)"
                      class="text-xs text-gray-600 hover:text-red-400 transition-colors">✕</button>
                  </div>
                  <p class="whitespace-pre-wrap font-sans">{{ pc.body }}</p>
                </div>
              </div>
            </template>
          </div>
        </div>
      </div>

      <!-- Submitted PR-level comments -->
      <div v-if="prLevelComments.length > 0" class="mt-6">
        <h2 class="text-sm font-medium text-gray-400 mb-3 uppercase tracking-wide">Review Comments</h2>
        <div v-for="c in prLevelComments" :key="c.id"
          class="bg-gray-900 border border-gray-800 rounded-xl p-4 mb-3">
          <div class="flex items-center gap-2 mb-2">
            <div class="w-6 h-6 rounded-full bg-brand-600 flex items-center justify-center text-xs font-bold text-white shrink-0">
              {{ (c.authorName ?? 'U').charAt(0).toUpperCase() }}
            </div>
            <span class="text-sm text-gray-300 font-medium">{{ c.authorName ?? 'User' }}</span>
            <span class="text-xs text-gray-600">{{ formatDate(c.createdAt) }}</span>
            <span v-if="c.commentType === 'review'"
              class="text-xs bg-purple-900/40 text-purple-300 px-1.5 py-0.5 rounded-full">Review</span>
          </div>
          <p class="text-sm text-gray-300 whitespace-pre-wrap">{{ c.body }}</p>
        </div>
      </div>
    </template>

    <!-- Submit Review Modal -->
    <div v-if="showSubmitModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-2">Submit Review</h2>
        <p class="text-sm text-gray-500 mb-4">
          {{ pendingComments.length }} inline comment{{ pendingComments.length !== 1 ? 's' : '' }} will be included.
        </p>
        <div class="mb-4">
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Overall comment (optional)</label>
          <textarea v-model="overallComment" rows="4" placeholder="Leave an overall review comment…"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
        </div>
        <div class="flex gap-3">
          <button @click="submitReview"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Submit Review
          </button>
          <button @click="showSubmitModal = false; submitError = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
        <p v-if="submitError" class="mt-3 text-sm text-red-400">{{ submitError }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DiffFile, DiffHunk, DiffLine, PrDiff, IssueComment, PendingLineComment } from '~/types'
import { useIssuesStore } from '~/stores/issues'

const route = useRoute()
const id = route.params.id as string
const issueId = route.params.issueId as string

const issueStore = useIssuesStore()
const api = useApi()

const diff = ref<PrDiff | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const baseBranch = ref('main')

// Files that are expanded
const expandedFiles = ref<Set<string>>(new Set())
// Big files explicitly loaded by user
const loadedBigFiles = ref<Set<string>>(new Set())

// Inline comment form state
const commentForm = ref<{ file: string; lineKey: string; body: string } | null>(null)

// Pending (draft) comments not yet submitted
const pendingComments = ref<(PendingLineComment & { lineKeyStr: string })[]>([])

// Submitted comments fetched from API
const submittedComments = ref<IssueComment[]>([])

// Review submission
const showSubmitModal = ref(false)
const overallComment = ref('')
const submitError = ref<string | null>(null)
const bigFileErrors = ref<Map<string, string>>(new Map())

onMounted(async () => {
  await issueStore.fetchIssue(id, issueId)
  await fetchComments()
})

async function fetchComments() {
  try {
    const data = await api.get<IssueComment[]>(`/api/issues/${issueId}/comments`)
    submittedComments.value = data
  } catch {
    // non-fatal
  }
}

async function loadDiff() {
  loading.value = true
  error.value = null
  try {
    const data = await api.get<PrDiff>(`/api/issues/${issueId}/pr-diff`, {
      params: { base: baseBranch.value }
    })
    diff.value = data
    // Auto-expand all non-big files
    for (const f of data.files) {
      if (!f.isBig) expandedFiles.value.add(f.filename)
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load diff'
  } finally {
    loading.value = false
  }
}

async function loadBigFile(file: DiffFile) {
  bigFileErrors.value.delete(file.filename)
  try {
    const hunks = await api.get<DiffHunk[]>(`/api/issues/${issueId}/pr-diff/file`, {
      params: { base: baseBranch.value, filename: file.filename }
    })
    file.hunks = hunks
    loadedBigFiles.value.add(file.filename)
  } catch (e: unknown) {
    bigFileErrors.value.set(file.filename, e instanceof Error ? e.message : 'Failed to load file diff')
  }
}

function toggleFile(filename: string) {
  if (expandedFiles.value.has(filename)) {
    expandedFiles.value.delete(filename)
  } else {
    expandedFiles.value.add(filename)
  }
}

function lineKey(line: DiffLine): string {
  return `${line.oldLineNo ?? ''}-${line.newLineNo ?? ''}`
}

function openCommentForm(file: string, line: DiffLine) {
  const key = lineKey(line)
  if (commentForm.value?.file === file && commentForm.value?.lineKey === key) {
    commentForm.value = null
  } else {
    commentForm.value = { file, lineKey: key, body: '' }
  }
}

function addPendingComment(file: string, line: DiffLine) {
  if (!commentForm.value?.body.trim()) return
  pendingComments.value.push({
    filePath: file,
    lineNumber: line.newLineNo ?? line.oldLineNo ?? 0,
    body: commentForm.value.body,
    lineContent: line.content,
    lineKeyStr: lineKey(line)
  })
  commentForm.value = null
}

function removePendingComment(index: number) {
  pendingComments.value.splice(index, 1)
}

function pendingCommentsByLine(file: string, line: DiffLine) {
  const key = lineKey(line)
  return pendingComments.value.filter(c => c.filePath === file && c.lineKeyStr === key)
}

function submittedCommentsByLine(file: string, line: DiffLine) {
  const lineNo = line.newLineNo ?? line.oldLineNo
  return submittedComments.value.filter(
    c => c.filePath === file && c.lineNumber === lineNo && c.commentType !== 'review'
  )
}

const prLevelComments = computed(() =>
  submittedComments.value.filter(c => !c.filePath || c.commentType === 'review')
)

async function submitReview() {
  // Build a formatted review body
  const parts: string[] = ['**Code Review**\n']

  for (const pc of pendingComments.value) {
    parts.push(`\n📁 \`${pc.filePath}\` line ${pc.lineNumber}`)
    parts.push('```diff\n' + pc.lineContent + '\n```')
    parts.push(`> ${pc.body}\n`)
    parts.push('---')
  }

  if (overallComment.value.trim()) {
    parts.push(`\n**Overall:** ${overallComment.value.trim()}`)
  }

  const body = parts.join('\n')

  try {
    await api.post<IssueComment>(`/api/issues/${issueId}/comments`, {
      body,
      commentType: 'review'
    })
    pendingComments.value = []
    overallComment.value = ''
    submitError.value = null
    showSubmitModal.value = false
    await fetchComments()
  } catch (e: unknown) {
    submitError.value = e instanceof Error ? e.message : 'Failed to submit review'
  }
}

function lineRowClass(type: string) {
  if (type === 'addition') return 'flex items-stretch bg-green-950/20 border-l-2 border-green-700'
  if (type === 'deletion') return 'flex items-stretch bg-red-950/20 border-l-2 border-red-800'
  return 'flex items-stretch hover:bg-gray-900/50'
}

function lineContentClass(type: string) {
  if (type === 'addition') return 'text-green-300'
  if (type === 'deletion') return 'text-red-400'
  return 'text-gray-400'
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    added: 'bg-green-400',
    removed: 'bg-red-400',
    modified: 'bg-yellow-400',
    renamed: 'bg-blue-400'
  }
  return map[status] ?? 'bg-gray-400'
}

function fileAnchor(filename: string) {
  return filename.replace(/[^a-zA-Z0-9]/g, '-')
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}
</script>
