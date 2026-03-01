<template>
  <div class="p-6 max-w-screen-xl mx-auto">
    <!-- Breadcrumb -->
    <div class="flex items-center gap-2 text-sm text-gray-500 mb-6">
      <NuxtLink :to="`/projects/${id}`" class="hover:text-gray-300">Project</NuxtLink>
      <span>/</span>
      <NuxtLink :to="`/projects/${id}/issues`" class="hover:text-gray-300">Issues</NuxtLink>
      <span>/</span>
      <NuxtLink :to="`/projects/${id}/issues/${issueId}`" class="hover:text-gray-300">
        #{{ issueStore.currentIssue?.number ?? '…' }}
      </NuxtLink>
      <span>/</span>
      <span class="text-gray-400">Code Review</span>
    </div>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-xl font-bold text-white">Code Review</h1>
        <p v-if="reviewStore.diff" class="text-sm text-gray-400 mt-0.5">
          Branch:
          <span class="font-mono text-brand-400">{{ reviewStore.diff.branch }}</span>
          &nbsp;·&nbsp;
          <span class="text-green-400">+{{ reviewStore.diff.totalAdditions }}</span>
          &nbsp;
          <span class="text-red-400">-{{ reviewStore.diff.totalDeletions }}</span>
        </p>
      </div>
      <div class="flex gap-3">
        <button v-if="pendingComments.length > 0" @click="submitReview"
          :disabled="reviewStore.loading"
          class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
          Submit Review ({{ pendingComments.length }})
        </button>
        <NuxtLink :to="`/projects/${id}/issues/${issueId}`"
          class="text-sm text-gray-400 hover:text-gray-300 px-3 py-2 rounded-lg border border-gray-700 hover:border-gray-600 transition-colors">
          ← Back to Issue
        </NuxtLink>
      </div>
    </div>

    <!-- No branch configured -->
    <div v-if="noBranch" class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
      <svg class="w-10 h-10 text-gray-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
          d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
      </svg>
      <p class="text-gray-400 font-medium">No git branch configured for this issue</p>
      <p class="text-gray-600 text-sm mt-1">Set a branch name in the issue settings to enable code review</p>
    </div>

    <!-- Loading diff -->
    <div v-else-if="reviewStore.diffLoading" class="space-y-4">
      <div v-for="i in 3" :key="i" class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div class="px-4 py-3 border-b border-gray-800 flex items-center gap-3 animate-pulse">
          <div class="h-4 bg-gray-700 rounded w-64"></div>
          <div class="h-4 bg-gray-700 rounded w-16 ml-auto"></div>
        </div>
        <div class="p-4 space-y-2 animate-pulse">
          <div v-for="j in 8" :key="j" class="h-3.5 bg-gray-800 rounded" :style="{ width: `${60 + Math.random() * 40}%` }"></div>
        </div>
      </div>
    </div>

    <!-- Diff error -->
    <div v-else-if="reviewStore.error && !reviewStore.diff" class="bg-red-950/30 border border-red-800 rounded-xl p-6 text-center">
      <p class="text-red-400 font-medium">Failed to load diff</p>
      <p class="text-red-500/70 text-sm mt-1">{{ reviewStore.error }}</p>
      <button @click="loadDiff" class="mt-4 text-sm text-brand-400 hover:text-brand-300">Try again</button>
    </div>

    <!-- Diff + comments -->
    <template v-else-if="reviewStore.diff">
      <!-- PR-level comment box -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
        <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Add Review Comment</h2>
        <textarea v-model="prComment" rows="3"
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-200 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none"
          placeholder="Leave a comment on the whole PR…"></textarea>
        <div class="flex justify-end mt-2">
          <button @click="addPrComment" :disabled="!prComment.trim() || reviewStore.loading"
            class="text-sm bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-4 py-1.5 rounded-md transition-colors">
            Comment
          </button>
        </div>
      </div>

      <!-- PR-level comments -->
      <div v-if="prLevelComments.length" class="space-y-3 mb-6">
        <div v-for="c in prLevelComments" :key="c.id"
          class="bg-gray-900 border border-blue-900/40 rounded-xl p-4 flex gap-3">
          <div class="w-7 h-7 rounded-full bg-brand-700 flex items-center justify-center text-xs text-white shrink-0">R</div>
          <div class="flex-1 min-w-0">
            <p class="text-sm text-gray-300 whitespace-pre-wrap">{{ c.body }}</p>
            <p class="text-xs text-gray-600 mt-1">{{ formatDate(c.createdAt) }}</p>
          </div>
          <button @click="removeComment(c.id)" class="text-gray-700 hover:text-red-400 transition-colors shrink-0">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      <!-- File diffs -->
      <div class="space-y-4">
        <div v-for="file in reviewStore.diff.files" :key="file.fileName"
          class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">

          <!-- File header -->
          <div class="px-4 py-3 border-b border-gray-800 flex items-center gap-3 bg-gray-900/80">
            <span :class="fileStatusClass(file.status)"
              class="text-xs font-bold px-1.5 py-0.5 rounded uppercase tracking-wide">
              {{ file.status.charAt(0).toUpperCase() }}
            </span>
            <span class="font-mono text-sm text-gray-300 flex-1 truncate">{{ file.fileName }}</span>
            <span class="text-xs text-green-500 shrink-0">+{{ file.additions }}</span>
            <span class="text-xs text-red-500 ml-1 shrink-0">-{{ file.deletions }}</span>
            <button v-if="!expandedFiles.has(file.fileName)" @click="expandedFiles.add(file.fileName)"
              class="text-xs text-gray-500 hover:text-gray-300 ml-2 shrink-0 transition-colors">
              Show diff ▾
            </button>
            <button v-else @click="expandedFiles.delete(file.fileName)"
              class="text-xs text-gray-500 hover:text-gray-300 ml-2 shrink-0 transition-colors">
              Hide diff ▴
            </button>
          </div>

          <!-- Diff content (lazy loaded) -->
          <template v-if="expandedFiles.has(file.fileName)">
            <div v-if="!file.patch" class="px-4 py-4 text-sm text-gray-600 italic">
              Binary or large file — no diff available.
            </div>
            <div v-else class="overflow-x-auto">
              <table class="w-full text-xs font-mono border-collapse">
                <tbody>
                  <template v-for="(line, lineIdx) in parsedPatches[file.fileName]" :key="lineIdx">
                    <!-- Hunk header -->
                    <tr v-if="line.type === 'hunk'" class="bg-blue-950/30">
                      <td class="px-2 py-0.5 text-blue-400/60 text-right select-none w-10 border-r border-gray-800"></td>
                      <td class="px-2 py-0.5 text-blue-400/60 text-right select-none w-10 border-r border-gray-800"></td>
                      <td class="px-3 py-0.5 text-blue-400/60 whitespace-pre" colspan="2">{{ line.content }}</td>
                    </tr>

                    <!-- Code line -->
                    <tr v-else :class="lineRowClass(line.type)"
                      class="group cursor-pointer hover:brightness-125 transition-all"
                      @click="openLineComment(file.fileName, line, lineIdx)">
                      <td class="px-2 py-0.5 text-gray-700 text-right select-none w-10 border-r border-gray-800">
                        {{ line.oldLineNumber ?? '' }}
                      </td>
                      <td class="px-2 py-0.5 text-gray-700 text-right select-none w-10 border-r border-gray-800">
                        {{ line.newLineNumber ?? '' }}
                      </td>
                      <td class="px-1 py-0.5 select-none w-4" :class="linePrefixClass(line.type)">
                        {{ line.type === 'add' ? '+' : line.type === 'del' ? '-' : ' ' }}
                      </td>
                      <td class="px-2 py-0.5 whitespace-pre text-gray-300">{{ line.content }}</td>
                    </tr>

                    <!-- Inline comment form -->
                    <tr v-if="activeComment?.file === file.fileName && activeComment?.lineIdx === lineIdx">
                      <td colspan="4" class="p-3 bg-gray-800/60 border-t border-b border-brand-800/40">
                        <div class="flex gap-3">
                          <div class="flex-1">
                            <textarea v-model="activeComment.body" rows="3"
                              class="w-full bg-gray-900 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-200 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none"
                              placeholder="Add a comment on this line…"
                              autofocus></textarea>
                            <div class="flex gap-2 mt-2">
                              <button @click="saveLineComment(file.fileName, line)"
                                :disabled="!activeComment.body.trim()"
                                class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-3 py-1 rounded-md transition-colors">
                                Add Comment
                              </button>
                              <button @click="activeComment = null"
                                class="text-xs bg-gray-700 hover:bg-gray-600 text-gray-300 px-3 py-1 rounded-md transition-colors">
                                Cancel
                              </button>
                            </div>
                          </div>
                        </div>
                      </td>
                    </tr>

                    <!-- Inline comments display -->
                    <template v-for="c in lineComments(file.fileName, line)" :key="c.id">
                      <tr class="bg-blue-950/20 border-t border-blue-900/20">
                        <td colspan="4" class="px-4 py-3">
                          <div class="flex gap-3 items-start">
                            <div class="w-6 h-6 rounded-full bg-brand-700 flex items-center justify-center text-xs text-white shrink-0">R</div>
                            <div class="flex-1 min-w-0">
                              <p class="text-sm text-gray-300 whitespace-pre-wrap">{{ c.body }}</p>
                              <p class="text-xs text-gray-600 mt-0.5">{{ formatDate(c.createdAt) }}</p>
                            </div>
                            <button @click="removeComment(c.id)" class="text-gray-700 hover:text-red-400 transition-colors shrink-0">
                              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                              </svg>
                            </button>
                          </div>
                        </td>
                      </tr>
                    </template>
                  </template>
                </tbody>
              </table>
            </div>
          </template>

          <!-- Skeleton / collapsed -->
          <template v-else>
            <div class="px-4 py-3 text-center">
              <button @click="expandedFiles.add(file.fileName)"
                class="text-xs text-gray-600 hover:text-gray-400 transition-colors">
                Click to load diff ({{ file.changes }} change{{ file.changes !== 1 ? 's' : '' }})
              </button>
            </div>
          </template>

          <!-- File-level comments for this file -->
          <template v-if="fileOnlyComments(file.fileName).length">
            <div class="border-t border-gray-800 px-4 py-3 space-y-3">
              <div v-for="c in fileOnlyComments(file.fileName)" :key="c.id"
                class="flex gap-3 items-start">
                <div class="w-6 h-6 rounded-full bg-brand-700 flex items-center justify-center text-xs text-white shrink-0">R</div>
                <div class="flex-1 min-w-0">
                  <p class="text-sm text-gray-300 whitespace-pre-wrap">{{ c.body }}</p>
                  <p class="text-xs text-gray-600 mt-0.5">{{ formatDate(c.createdAt) }}</p>
                </div>
                <button @click="removeComment(c.id)" class="text-gray-700 hover:text-red-400 transition-colors shrink-0">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
          </template>
        </div>
      </div>

      <!-- Bottom submit -->
      <div v-if="pendingComments.length === 0 && reviewStore.comments.length > 0"
        class="mt-6 bg-gray-900 border border-green-900/30 rounded-xl p-4 flex items-center gap-3">
        <svg class="w-5 h-5 text-green-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <p class="text-sm text-green-400">Review submitted — {{ reviewStore.comments.length }} comment{{ reviewStore.comments.length !== 1 ? 's' : '' }} added</p>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { DiffLine, IssueComment } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useReviewStore, parsePatch } from '~/stores/review'

const route = useRoute()
const id = route.params.id as string
const issueId = route.params.issueId as string

const issueStore = useIssuesStore()
const reviewStore = useReviewStore()

const expandedFiles = ref(new Set<string>())
const prComment = ref('')
const activeComment = ref<{ file: string; lineIdx: number; body: string } | null>(null)
const noBranch = ref(false)

// Parsed patches keyed by file name
const parsedPatches = computed(() => {
  const result: Record<string, DiffLine[]> = {}
  for (const file of reviewStore.diff?.files ?? []) {
    result[file.fileName] = parsePatch(file.patch)
  }
  return result
})

const prLevelComments = computed(() =>
  reviewStore.comments.filter(c => !c.filePath && !c.lineStart)
)

// Comments that are file-level only (have filePath but no lineStart)
function fileOnlyComments(fileName: string): IssueComment[] {
  return reviewStore.comments.filter(c => c.filePath === fileName && !c.lineStart)
}

// Comments attached to a specific line
function lineComments(fileName: string, line: DiffLine): IssueComment[] {
  return reviewStore.comments.filter(c =>
    c.filePath === fileName &&
    c.lineStart != null &&
    (line.newLineNumber === c.lineStart || line.oldLineNumber === c.lineStart)
  )
}

// Pending comments = unsaved drafts (not used here — all comments are immediately saved)
const pendingComments = computed(() => [] as IssueComment[])

async function loadDiff() {
  await reviewStore.fetchDiff(issueId)
  if (reviewStore.error?.includes('no git branch')) {
    noBranch.value = true
  }
}

onMounted(async () => {
  reviewStore.reset()
  if (!issueStore.currentIssue) {
    await issueStore.fetchIssue(id, issueId)
  }
  if (!issueStore.currentIssue?.gitBranch) {
    noBranch.value = true
    return
  }
  await Promise.all([
    loadDiff(),
    reviewStore.fetchComments(issueId)
  ])
})

onUnmounted(() => reviewStore.reset())

function openLineComment(fileName: string, line: DiffLine, lineIdx: number) {
  if (activeComment.value?.file === fileName && activeComment.value?.lineIdx === lineIdx) {
    activeComment.value = null
    return
  }
  activeComment.value = { file: fileName, lineIdx, body: '' }
}

async function saveLineComment(fileName: string, line: DiffLine) {
  if (!activeComment.value?.body.trim()) return
  await reviewStore.addComment(issueId, {
    body: activeComment.value.body,
    filePath: fileName,
    lineStart: line.newLineNumber ?? line.oldLineNumber ?? undefined,
    lineEnd: line.newLineNumber ?? line.oldLineNumber ?? undefined,
    diffSide: line.type === 'add' ? 'right' : line.type === 'del' ? 'left' : 'right',
    commitSha: reviewStore.diff?.commitSha ?? undefined,
  })
  activeComment.value = null
}

async function addPrComment() {
  if (!prComment.value.trim()) return
  await reviewStore.addComment(issueId, { body: prComment.value })
  prComment.value = ''
}

async function removeComment(commentId: string) {
  await reviewStore.deleteComment(issueId, commentId)
}

async function submitReview() {
  // All comments are already saved; this button is for future batch submit flows
}

function lineRowClass(type: DiffLine['type']): string {
  switch (type) {
    case 'add': return 'bg-green-950/30'
    case 'del': return 'bg-red-950/30'
    default: return ''
  }
}

function linePrefixClass(type: DiffLine['type']): string {
  switch (type) {
    case 'add': return 'text-green-500'
    case 'del': return 'text-red-500'
    default: return 'text-gray-700'
  }
}

function fileStatusClass(status: string): string {
  switch (status.toLowerCase()) {
    case 'added': return 'bg-green-900/40 text-green-400'
    case 'removed': return 'bg-red-900/40 text-red-400'
    case 'modified': return 'bg-yellow-900/40 text-yellow-400'
    case 'renamed': return 'bg-blue-900/40 text-blue-400'
    default: return 'bg-gray-800 text-gray-400'
  }
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}
</script>
