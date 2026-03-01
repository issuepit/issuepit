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
          d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7" />
      </svg>
      <h1 class="text-xl font-bold text-white">Code Review</h1>
    </div>

    <!-- Loading initial -->
    <div v-if="store.loading && !store.repo && !repoChecked" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- No repo configured -->
    <div v-else-if="repoChecked && !store.repo">
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-8 max-w-lg">
        <p class="text-gray-400">No git repository is linked to this project. Go to
          <NuxtLink :to="`/projects/${id}/code`" class="text-brand-400 hover:underline">Code</NuxtLink>
          to link one first.
        </p>
      </div>
    </div>

    <template v-else-if="store.repo">
      <!-- Branch selectors -->
      <div class="flex flex-wrap items-center gap-3 mb-5">
        <div class="flex items-center gap-2">
          <span class="text-sm text-gray-400">Base:</span>
          <BranchSelect v-model="baseBranch" :branches="allBranches" />
        </div>
        <svg class="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7l5 5m0 0l-5 5m5-5H6" />
        </svg>
        <div class="flex items-center gap-2">
          <span class="text-sm text-gray-400">Compare:</span>
          <BranchSelect v-model="compareBranch" :branches="allBranches" />
        </div>
        <button @click="loadDiff" :disabled="store.loading || !baseBranch || !compareBranch || baseBranch === compareBranch"
          class="flex items-center gap-1.5 bg-brand-600 hover:bg-brand-700 text-white text-sm px-4 py-1.5 rounded-lg transition-colors disabled:opacity-50">
          <svg v-if="store.loading" class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Compare
        </button>
        <!-- Diff mode toggle -->
        <div v-if="store.diff.length" class="ml-auto flex items-center gap-1 bg-gray-900 border border-gray-800 rounded-lg p-0.5">
          <button @click="diffMode = 'unified'"
            :class="diffMode === 'unified' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200'"
            class="text-xs px-2.5 py-1 rounded-md transition-colors">Unified</button>
          <button @click="diffMode = 'split'"
            :class="diffMode === 'split' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200'"
            class="text-xs px-2.5 py-1 rounded-md transition-colors">Split</button>
        </div>
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
          <button @click="showFinishModal = true" :disabled="savingReview"
            class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
            Finish Review
          </button>
          <button @click="discardReview"
            class="text-xs text-gray-400 hover:text-gray-200 transition-colors">
            Discard
          </button>
        </div>
      </div>

      <!-- Error -->
      <div v-if="store.error" class="mb-4 p-3 bg-red-900/30 border border-red-800/40 rounded-lg text-sm text-red-300">
        {{ store.error }}
      </div>

      <!-- Diff summary bar -->
      <div v-if="store.diff.length" class="mb-4 flex items-center gap-4 text-sm text-gray-400">
        <span>{{ store.diff.length }} file{{ store.diff.length !== 1 ? 's' : '' }} changed</span>
        <span class="text-green-400">+{{ totalAdded }}</span>
        <span class="text-red-400">-{{ totalRemoved }}</span>
      </div>

      <!-- File list (table of contents) -->
      <div v-if="store.diff.length" class="mb-4 bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div v-for="file in store.diff" :key="file.newPath"
          class="flex items-center gap-3 px-4 py-2 border-b border-gray-800 last:border-0 hover:bg-gray-800/50 cursor-pointer transition-colors text-sm"
          @click="scrollToFile(file.newPath)">
          <span :class="statusColor(file.status)" class="shrink-0 font-mono text-xs font-bold w-4 text-center">
            {{ statusLetter(file.status) }}
          </span>
          <span class="flex-1 font-mono text-gray-300 truncate">{{ displayPath(file) }}</span>
          <span v-if="file.isTooLarge" class="text-xs text-yellow-500">large</span>
          <span v-else-if="file.isBinary" class="text-xs text-gray-500">binary</span>
          <span v-else class="text-xs text-gray-500">
            <span class="text-green-400">+{{ file.addedLines }}</span>
            <span class="mx-0.5 text-gray-600">/</span>
            <span class="text-red-400">-{{ file.removedLines }}</span>
          </span>
        </div>
      </div>

      <!-- Diff files -->
      <div v-for="file in store.diff" :key="file.newPath" :id="`file-${fileId(file.newPath)}`" class="mb-4">
        <!-- File header -->
        <div class="flex items-center gap-2 bg-gray-900 border border-gray-800 rounded-t-xl px-4 py-2.5">
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
          <!-- Toggle large/full file -->
          <button v-if="file.isTooLarge && !expandedFiles.has(file.newPath)"
            @click="expandFile(file)"
            class="text-xs text-brand-400 hover:text-brand-300 ml-2 transition-colors">
            Load diff
          </button>
          <button v-if="!file.isTooLarge && !file.isBinary && file.hunks.length"
            @click="toggleFullFile(file.newPath)"
            class="text-xs text-gray-500 hover:text-gray-300 ml-2 transition-colors">
            {{ fullFileMode.has(file.newPath) ? 'Show diff' : 'Show full file' }}
          </button>
        </div>

        <!-- Binary notice -->
        <div v-if="file.isBinary"
          class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4 text-center text-sm text-gray-500">
          Binary file — preview not available
        </div>

        <!-- Too-large skeleton (not yet loaded) -->
        <div v-else-if="file.isTooLarge && !expandedFiles.has(file.newPath)"
          class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4">
          <div class="space-y-2">
            <div v-for="n in 6" :key="n" class="h-4 bg-gray-800 rounded animate-pulse"
              :style="{ width: `${30 + (n * 13 % 55)}%` }"></div>
          </div>
          <p class="text-xs text-gray-500 mt-3 text-center">
            Large file ({{ file.addedLines + file.removedLines }} changed lines) — click "Load diff" to view
          </p>
        </div>

        <!-- Loading expanded file -->
        <div v-else-if="expandingFiles.has(file.newPath)"
          class="bg-gray-900/50 border border-t-0 border-gray-800 rounded-b-xl p-4 text-center text-sm text-gray-400">
          <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
        </div>

        <!-- Diff hunks (unified) -->
        <div v-else-if="diffMode === 'unified'"
          class="border border-t-0 border-gray-800 rounded-b-xl overflow-x-auto">
          <table class="w-full border-collapse text-xs font-mono">
            <tbody>
              <template v-for="(hunk, hunkIdx) in getHunks(file)" :key="hunkIdx">
                <!-- Hunk header -->
                <tr class="bg-blue-950/30">
                  <td colspan="3" class="px-3 py-1 text-blue-400/70 select-none text-left">
                    @@ -{{ hunk.oldStart }},{{ hunk.oldCount }} +{{ hunk.newStart }},{{ hunk.newCount }} @@
                    <span class="text-gray-500 ml-2">{{ hunk.header }}</span>
                  </td>
                </tr>
                <!-- Lines -->
                <tr v-for="(line, lineIdx) in hunk.lines" :key="lineIdx"
                  :class="lineRowClass(line.lineType)"
                  class="group hover:brightness-125 transition-all">
                  <!-- Old line number -->
                  <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top cursor-pointer"
                    :class="{ 'hover:text-brand-400': line.lineType !== 'added' }"
                    @click="onLineClick(file, hunk, line, 'old', $event)">
                    {{ line.oldLineNumber ?? '' }}
                  </td>
                  <!-- New line number -->
                  <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top cursor-pointer"
                    :class="{ 'hover:text-brand-400': line.lineType !== 'removed' }"
                    @click="onLineClick(file, hunk, line, 'new', $event)">
                    {{ line.newLineNumber ?? '' }}
                  </td>
                  <!-- Content -->
                  <td class="pl-2 pr-3 whitespace-pre leading-relaxed"
                    v-html="highlightLine(file.newPath, line)"></td>
                </tr>
                <!-- Inline comment inputs -->
                <template v-for="(comment, ci) in inlineComments[file.newPath] ?? []" :key="`ic-${ci}`">
                  <tr v-if="comment.hunkIdx === hunkIdx && comment.lineIdx === lineIdx">
                    <td colspan="3" class="bg-gray-900 border-t border-b border-brand-800/40 p-3">
                      <div class="flex items-start gap-2">
                        <textarea v-model="comment.text" rows="2"
                          placeholder="Add a comment… (Ctrl+Enter to submit)"
                          class="flex-1 bg-gray-800 border border-gray-700 rounded-lg text-sm text-white px-3 py-2 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none"
                          @keydown.ctrl.enter="submitInlineComment(file, hunk, line, comment, hunkIdx, lineIdx)"></textarea>
                        <div class="flex flex-col gap-1.5">
                          <button @click="submitInlineComment(file, hunk, line, comment, hunkIdx, lineIdx)"
                            :disabled="!comment.text.trim()"
                            class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
                            Add
                          </button>
                          <button @click="removeInlineComment(file.newPath, ci)"
                            class="text-xs bg-gray-700 hover:bg-gray-600 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
                            Cancel
                          </button>
                        </div>
                      </div>
                    </td>
                  </tr>
                </template>
              </template>
              <tr v-if="!getHunks(file).length">
                <td colspan="3" class="p-4 text-center text-gray-500 text-xs">No changes</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Diff hunks (split) -->
        <div v-else-if="diffMode === 'split'"
          class="border border-t-0 border-gray-800 rounded-b-xl overflow-x-auto">
          <table class="w-full border-collapse text-xs font-mono table-fixed">
            <tbody>
              <template v-for="(hunk, hunkIdx) in getHunks(file)" :key="hunkIdx">
                <!-- Hunk header -->
                <tr class="bg-blue-950/30">
                  <td colspan="4" class="px-3 py-1 text-blue-400/70 select-none text-left">
                    @@ -{{ hunk.oldStart }},{{ hunk.oldCount }} +{{ hunk.newStart }},{{ hunk.newCount }} @@
                    <span class="text-gray-500 ml-2">{{ hunk.header }}</span>
                  </td>
                </tr>
                <!-- Split lines (paired) -->
                <template v-for="(pair, pairIdx) in splitLines(hunk.lines)" :key="pairIdx">
                  <tr class="group">
                    <!-- Left (old) -->
                    <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top"
                      :class="pair.left ? lineRowClass(pair.left.lineType) : ''">
                      {{ pair.left?.oldLineNumber ?? '' }}
                    </td>
                    <td class="pl-2 pr-2 whitespace-pre leading-relaxed w-1/2 border-r border-gray-800/40"
                      :class="pair.left ? lineRowClass(pair.left.lineType) : ''"
                      v-html="pair.left ? highlightLine(file.newPath, pair.left) : ''"></td>
                    <!-- Right (new) -->
                    <td class="select-none text-right text-gray-600 pr-2 pl-2 w-10 border-r border-gray-800/40 align-top"
                      :class="pair.right ? lineRowClass(pair.right.lineType) : ''">
                      {{ pair.right?.newLineNumber ?? '' }}
                    </td>
                    <td class="pl-2 pr-2 whitespace-pre leading-relaxed w-1/2"
                      :class="pair.right ? lineRowClass(pair.right.lineType) : ''"
                      v-html="pair.right ? highlightLine(file.newPath, pair.right) : ''"></td>
                  </tr>
                </template>
              </template>
              <tr v-if="!getHunks(file).length">
                <td colspan="4" class="p-4 text-center text-gray-500 text-xs">No changes</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Reviewed comments summary for this file -->
        <div v-if="fileReviewComments(file.newPath).length"
          class="mt-1 bg-gray-900/50 border border-gray-800 rounded-xl p-3 space-y-2">
          <p class="text-xs text-gray-500 font-medium">{{ fileReviewComments(file.newPath).length }} pending comment{{ fileReviewComments(file.newPath).length !== 1 ? 's' : '' }}</p>
          <div v-for="(rc, rci) in fileReviewComments(file.newPath)" :key="rci"
            class="text-xs text-gray-300 bg-gray-800 rounded-lg p-2 flex items-start gap-2">
            <span class="text-gray-500 shrink-0">L{{ rc.lines.start }}{{ rc.lines.start !== rc.lines.end ? `–${rc.lines.end}` : '' }}</span>
            <span class="flex-1">{{ rc.comment }}</span>
            <button @click="removeReviewComment(rci, file.newPath)" class="text-gray-600 hover:text-red-400 transition-colors shrink-0">×</button>
          </div>
        </div>
      </div>

      <!-- General comment on whole PR -->
      <div v-if="store.diff.length" class="mt-4 bg-gray-900 border border-gray-800 rounded-xl p-4">
        <h3 class="text-sm font-medium text-gray-300 mb-2">General comment on this diff</h3>
        <textarea v-model="generalComment" rows="3"
          placeholder="Add an overall comment about this diff…"
          class="w-full bg-gray-800 border border-gray-700 rounded-lg text-sm text-white px-3 py-2 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none mb-2"></textarea>
        <div class="flex gap-2">
          <button @click="addGeneralComment" :disabled="!generalComment.trim()"
            class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50">
            Add to Review
          </button>
        </div>
      </div>

      <!-- Empty state after comparison -->
      <div v-else-if="comparedOnce && !store.loading"
        class="py-12 text-center text-gray-500 text-sm">
        No differences found between <code class="text-gray-400">{{ baseBranch }}</code> and <code class="text-gray-400">{{ compareBranch }}</code>
      </div>
    </template>

    <!-- Finish Review Modal -->
    <div v-if="showFinishModal"
      class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
      <div class="bg-gray-900 border border-gray-800 rounded-2xl p-6 w-full max-w-md shadow-2xl">
        <h2 class="text-lg font-bold text-white mb-1">Submit Review</h2>
        <p class="text-sm text-gray-400 mb-4">
          This will create an issue with your {{ reviewComments.length }} comment{{ reviewComments.length !== 1 ? 's' : '' }}.
        </p>
        <div class="space-y-3">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Issue title</label>
            <input v-model="reviewTitle" type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1">Link to existing issue <span class="text-gray-500">(optional)</span></label>
            <input v-model="targetIssueId" type="text" placeholder="Issue ID (paste from issue URL)"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
            <p class="text-xs text-gray-500 mt-1">Leave blank to create a new issue, or paste an issue ID to add the review as a comment.</p>
          </div>
        </div>
        <div class="flex gap-2 mt-5 justify-end">
          <button @click="showFinishModal = false"
            class="text-sm bg-gray-800 hover:bg-gray-700 text-gray-300 px-4 py-2 rounded-lg transition-colors">
            Cancel
          </button>
          <button @click="finishReview" :disabled="savingReview"
            class="text-sm bg-brand-600 hover:bg-brand-700 text-white px-4 py-2 rounded-lg transition-colors disabled:opacity-50">
            {{ savingReview ? 'Submitting…' : 'Submit Review' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import DOMPurify from 'dompurify'
import hljs from 'highlight.js'
import { useGitStore } from '~/stores/git'
import { useIssuesStore } from '~/stores/issues'
import { IssueType, IssuePriority, IssueStatus } from '~/types'
import type { GitDiffFile, GitDiffHunk, GitDiffLine } from '~/types'

const route = useRoute()
const id = route.params.id as string
const store = useGitStore()
const issuesStore = useIssuesStore()

const repoChecked = ref(false)
const baseBranch = ref('')
const compareBranch = ref('')
const diffMode = ref<'unified' | 'split'>('unified')
const comparedOnce = ref(false)
const expandedFiles = ref(new Set<string>())
const expandingFiles = ref(new Set<string>())
const fullFileMode = ref(new Set<string>())
const fullFileCache = ref<Record<string, string[]>>({})

// ── Review session ──────────────────────────────────────────
interface ReviewComment {
  filePath: string
  lines: { start: number; end: number }
  comment: string
  snippet: string
}

const reviewComments = ref<ReviewComment[]>([])
const generalComment = ref('')
const savingReview = ref(false)
const showFinishModal = ref(false)
const reviewTitle = ref('')
const targetIssueId = ref('')

const reviewedFilesCount = computed(() => new Set(reviewComments.value.map(c => c.filePath)).size)

function fileReviewComments(filePath: string) {
  return reviewComments.value.filter(c => c.filePath === filePath)
}

function removeReviewComment(idx: number, filePath: string) {
  const fileComments = fileReviewComments(filePath)
  const targetComment = fileComments[idx]
  const globalIdx = reviewComments.value.indexOf(targetComment)
  if (globalIdx !== -1) reviewComments.value.splice(globalIdx, 1)
}

function discardReview() {
  reviewComments.value = []
  generalComment.value = ''
}

function addGeneralComment() {
  if (!generalComment.value.trim()) return
  reviewComments.value.push({
    filePath: '(general)',
    lines: { start: 0, end: 0 },
    comment: generalComment.value.trim(),
    snippet: ''
  })
  generalComment.value = ''
}

// ── Inline comment state ────────────────────────────────────
interface InlineCommentState {
  hunkIdx: number
  lineIdx: number
  text: string
}

const inlineComments = ref<Record<string, InlineCommentState[]>>({})

function onLineClick(file: GitDiffFile, hunk: GitDiffHunk, line: GitDiffLine, side: 'old' | 'new', event: MouseEvent) {
  if (event.shiftKey) return // reserved for future multi-line selection
  const fp = file.newPath
  if (!inlineComments.value[fp]) inlineComments.value[fp] = []
  const hunkIdx = getHunks(file).indexOf(hunk)
  const lineIdx = hunk.lines.indexOf(line)
  // Toggle: remove if already present at same position
  const existing = inlineComments.value[fp].findIndex(c => c.hunkIdx === hunkIdx && c.lineIdx === lineIdx)
  if (existing !== -1) {
    inlineComments.value[fp].splice(existing, 1)
  } else {
    inlineComments.value[fp].push({ hunkIdx, lineIdx, text: '' })
  }
}

function removeInlineComment(filePath: string, idx: number) {
  inlineComments.value[filePath]?.splice(idx, 1)
}

function submitInlineComment(file: GitDiffFile, _hunk: GitDiffHunk, line: GitDiffLine, comment: InlineCommentState, _hunkIdx: number, _lineIdx: number) {
  if (!comment.text.trim()) return
  const lineNum = line.newLineNumber ?? line.oldLineNumber ?? 0
  const snippet = line.content
  reviewComments.value.push({
    filePath: file.newPath,
    lines: { start: lineNum, end: lineNum },
    comment: comment.text.trim(),
    snippet
  })
  removeInlineComment(file.newPath, (inlineComments.value[file.newPath] ?? []).indexOf(comment))
}

// ── Finish review ────────────────────────────────────────────
async function finishReview() {
  if (reviewComments.value.length === 0) return
  savingReview.value = true
  try {
    const bodyLines: string[] = []
    for (const c of reviewComments.value) {
      if (c.filePath === '(general)') {
        bodyLines.push(c.comment)
      } else {
        const lineRange = c.lines.start === c.lines.end
          ? `line ${c.lines.start}`
          : `lines ${c.lines.start}–${c.lines.end}`
        const ext = c.filePath.split('.').pop() ?? ''
        bodyLines.push(`### \`${c.filePath}\` (${lineRange})\n\`\`\`${ext}\n${c.snippet}\n\`\`\`\n\n${c.comment}`)
      }
    }
    const body = bodyLines.join('\n\n---\n\n')

    if (targetIssueId.value.trim()) {
      // Add as a comment to an existing issue
      await issuesStore.addComment(targetIssueId.value.trim(), body)
    } else {
      // Create a new issue
      const title = reviewTitle.value.trim() || `Code Review: ${compareBranch.value} → ${baseBranch.value} (${new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })})`
      await issuesStore.createIssue(id, {
        title,
        body,
        type: IssueType.Issue,
        priority: IssuePriority.Medium,
        status: IssueStatus.InReview
      })
    }
    reviewComments.value = []
    showFinishModal.value = false
    reviewTitle.value = ''
    targetIssueId.value = ''
  } finally {
    savingReview.value = false
  }
}

// ── Diff loading ─────────────────────────────────────────────
async function loadDiff() {
  if (!baseBranch.value || !compareBranch.value) return
  expandedFiles.value = new Set()
  inlineComments.value = {}
  fullFileMode.value = new Set()
  fullFileCache.value = {}
  await store.fetchDiff(id, baseBranch.value, compareBranch.value)
  comparedOnce.value = true
}

async function expandFile(file: GitDiffFile) {
  expandingFiles.value = new Set([...expandingFiles.value, file.newPath])
  await store.fetchDiff(id, baseBranch.value, compareBranch.value, 3)
  // Find updated file entry from fresh diff
  expandedFiles.value = new Set([...expandedFiles.value, file.newPath])
  expandingFiles.value.delete(file.newPath)
  expandingFiles.value = new Set(expandingFiles.value)
}

async function toggleFullFile(filePath: string) {
  if (fullFileMode.value.has(filePath)) {
    fullFileMode.value.delete(filePath)
    fullFileMode.value = new Set(fullFileMode.value)
  } else {
    // Load the full blob for the compare branch
    await store.fetchBlob(id, filePath, compareBranch.value)
    if (store.blob) {
      fullFileCache.value[filePath] = store.blob.content.split('\n')
    }
    fullFileMode.value = new Set([...fullFileMode.value, filePath])
  }
}

function getHunks(file: GitDiffFile): GitDiffHunk[] {
  if (fullFileMode.value.has(file.newPath)) {
    const lines = fullFileCache.value[file.newPath] ?? []
    // Return a synthetic single hunk with all lines as context
    return [{
      oldStart: 1,
      oldCount: lines.length,
      newStart: 1,
      newCount: lines.length,
      header: 'full file',
      lines: lines.map((content, i) => ({
        oldLineNumber: i + 1,
        newLineNumber: i + 1,
        content,
        lineType: 'context' as const
      }))
    }]
  }
  return file.hunks
}

// ── Syntax highlighting ──────────────────────────────────────
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

function highlightLine(filePath: string, line: GitDiffLine): string {
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

// ── Split diff helper ─────────────────────────────────────────
interface SplitPair {
  left: GitDiffLine | null
  right: GitDiffLine | null
}

function splitLines(lines: GitDiffLine[]): SplitPair[] {
  const pairs: SplitPair[] = []
  let i = 0
  while (i < lines.length) {
    const line = lines[i]
    if (line.lineType === 'context') {
      pairs.push({ left: line, right: line })
      i++
    } else if (line.lineType === 'removed') {
      // Look ahead for a matching added line
      const nextAdded = lines[i + 1]?.lineType === 'added' ? lines[i + 1] : null
      pairs.push({ left: line, right: nextAdded })
      i += nextAdded ? 2 : 1
    } else if (line.lineType === 'added') {
      pairs.push({ left: null, right: line })
      i++
    } else {
      i++
    }
  }
  return pairs
}

// ── UI helpers ────────────────────────────────────────────────
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

function scrollToFile(path: string) {
  const el = document.getElementById(`file-${fileId(path)}`)
  el?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

const totalAdded = computed(() => store.diff.reduce((s, f) => s + f.addedLines, 0))
const totalRemoved = computed(() => store.diff.reduce((s, f) => s + f.removedLines, 0))

const allBranches = computed(() =>
  store.branches
)

// ── Lifecycle ─────────────────────────────────────────────────
onMounted(async () => {
  store.reset()
  await store.fetchRepo(id)
  repoChecked.value = true
  if (store.repo) {
    await store.fetchBranches(id)
    const def = store.repo.defaultBranch ?? 'main'
    const found = allBranches.value.find(b => b.name === def) ?? allBranches.value[0]
    if (found) {
      baseBranch.value = found.name
      compareBranch.value = found.name
    }

    // Pre-fill from query params if present
    const q = route.query
    if (q.base) baseBranch.value = String(q.base)
    if (q.compare) compareBranch.value = String(q.compare)
    if (q.base && q.compare) await loadDiff()
  }
})

onUnmounted(() => store.reset())
</script>

<style>@import 'highlight.js/styles/github-dark.css';</style>
