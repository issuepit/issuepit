<script setup lang="ts">
import type { Issue, IssueEnrichment } from '~/types'
import { IssueStatus } from '~/types'

const props = defineProps<{
  issue: Issue
  enrichment?: IssueEnrichment | null
  formatIssueId: (number: number, project: unknown, externalId?: number | null, externalSource?: unknown) => string
  project: unknown
  priorityIcon: (priority: string) => string
  priorityColor: (priority: string) => string
  typeBadge: (type: string) => string
}>()

defineEmits<{
  (e: 'click'): void
}>()

const isEnriched = computed(() =>
  props.enrichment != null
  && (props.issue.status === IssueStatus.InReview || props.issue.status === IssueStatus.ReadyToMerge),
)

function ciCheckIcon(status: string): string {
  switch (status.toLowerCase()) {
    case 'success': return '✓'
    case 'failure': return '✗'
    case 'skipped': return '⊘'
    default: return '●'
  }
}

function ciCheckColor(status: string): string {
  switch (status.toLowerCase()) {
    case 'success': return 'text-green-400'
    case 'failure': return 'text-red-400'
    case 'skipped': return 'text-gray-500'
    default: return 'text-yellow-400'
  }
}

function ciRunStatusColor(status?: string | null): string {
  if (!status) return 'text-gray-500'
  switch (status.toLowerCase()) {
    case 'succeeded':
    case 'succeededwithwarnings':
      return 'text-green-400'
    case 'failed':
      return 'text-red-400'
    case 'running':
    case 'pending':
      return 'text-yellow-400'
    default:
      return 'text-gray-500'
  }
}

function ciRunStatusLabel(status?: string | null): string {
  if (!status) return 'Unknown'
  switch (status.toLowerCase()) {
    case 'succeeded': return 'CI passing'
    case 'succeededwithwarnings': return 'CI passing'
    case 'failed': return 'CI failing'
    case 'running': return 'CI running'
    case 'pending': return 'CI pending'
    default: return status
  }
}
</script>

<template>
  <div @click="$emit('click')">
    <!-- Issue ID and indicators -->
    <div class="flex items-start justify-between gap-2 mb-2">
      <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, project, issue.externalId, issue.externalSource) }}</span>
      <div class="flex items-center gap-1.5 shrink-0">
        <span v-if="issue.preventAgentMove" title="Protected from agent moves"
          aria-label="Protected from agent moves" role="img" class="text-xs text-amber-500">🔒</span>
        <span v-if="issue.hideFromAgents" title="Hidden from agents"
          aria-label="Hidden from agents" role="img" class="text-xs text-gray-500">👁</span>
        <span :class="priorityColor(issue.priority)" class="text-xs">
          {{ priorityIcon(issue.priority) }}
        </span>
      </div>
    </div>

    <!-- Title -->
    <p class="text-sm text-gray-200 leading-snug mb-3 group-hover:text-white transition-colors line-clamp-3">
      {{ issue.title }}
    </p>

    <!-- Enriched: PR info + CI status -->
    <template v-if="isEnriched && enrichment">
      <!-- PR badge + diff stats -->
      <div class="flex items-center gap-2 mb-2 flex-wrap">
        <a v-if="enrichment.gitHubPrUrl"
          :href="enrichment.gitHubPrUrl" target="_blank" rel="noopener"
          class="text-xs bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded font-medium hover:bg-gray-700"
          @click.stop>
          #{{ enrichment.gitHubPrNumber }}
        </a>
        <span v-else class="text-xs bg-gray-800 text-gray-300 px-1.5 py-0.5 rounded font-medium">
          {{ enrichment.sourceBranch }}
        </span>
        <span v-if="enrichment.linesAdded != null || enrichment.linesRemoved != null"
          class="text-xs font-mono">
          <span class="text-green-400">+{{ enrichment.linesAdded ?? 0 }}</span>
          <span class="text-red-400 ml-1">-{{ enrichment.linesRemoved ?? 0 }}</span>
        </span>
      </div>

      <!-- CI run status -->
      <div v-if="enrichment.ciCdRunStatus" class="flex items-center gap-1.5 mb-2">
        <span class="w-1.5 h-1.5 rounded-full"
          :class="ciRunStatusColor(enrichment.ciCdRunStatus).replace('text-', 'bg-')" />
        <span class="text-xs" :class="ciRunStatusColor(enrichment.ciCdRunStatus)">
          {{ ciRunStatusLabel(enrichment.ciCdRunStatus) }}
        </span>
        <span v-if="enrichment.failedTests > 0" class="text-xs text-red-400">
          · {{ enrichment.failedTests }} test{{ enrichment.failedTests > 1 ? 's' : '' }} failing
        </span>
        <span v-else-if="enrichment.totalTests > 0" class="text-xs text-green-400">
          · {{ enrichment.passedTests }}/{{ enrichment.totalTests }} tests
        </span>
      </div>

      <!-- CI Checks list (shown only for ReadyToMerge) -->
      <div v-if="issue.status === IssueStatus.ReadyToMerge && enrichment.ciChecks.length > 0"
        class="mb-2 space-y-0.5">
        <p class="text-xs text-gray-500 uppercase tracking-wider mb-1">CI Checks</p>
        <div v-for="check in enrichment.ciChecks" :key="check.name"
          class="flex items-center gap-1.5 text-xs">
          <span :class="ciCheckColor(check.status)">{{ ciCheckIcon(check.status) }}</span>
          <span class="text-gray-400 truncate">{{ check.name }}</span>
        </div>
      </div>
    </template>

    <!-- Footer: Type badge and labels -->
    <div class="flex items-center justify-between gap-1 flex-wrap">
      <span :class="typeBadge(issue.type)"
        class="text-xs px-1.5 py-0.5 rounded font-medium capitalize">
        {{ issue.type }}
      </span>
      <div v-if="issue.labels?.length" class="flex gap-1 flex-wrap">
        <span v-for="label in issue.labels.slice(0, 2)" :key="label.id"
          class="text-xs px-1.5 py-0.5 rounded-full text-white font-medium"
          :style="{ backgroundColor: label.color + '55', color: label.color }">
          {{ label.name }}
        </span>
        <span v-if="issue.labels.length > 2" class="text-xs text-gray-600">+{{ issue.labels.length - 2 }}</span>
      </div>
    </div>
  </div>
</template>
