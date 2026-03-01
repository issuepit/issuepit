<template>
  <div class="p-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-white">{{ filterTitle }}</h1>
        <p class="text-gray-400 mt-1 text-sm">{{ filterDescription }}</p>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"/>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Issue list -->
    <div v-if="!store.loading" class="space-y-2">
      <NuxtLink
        v-for="issue in store.issues"
        :key="issue.id"
        :to="`/projects/${issue.projectId}/issues/${issue.id}`"
        class="flex items-center gap-3 bg-gray-900 border border-gray-800 rounded-lg px-4 py-3 hover:border-gray-700 transition-colors group"
      >
        <!-- Status dot -->
        <span
          class="w-2.5 h-2.5 rounded-full shrink-0"
          :class="statusColor(issue.status)"
        />
        <!-- Title -->
        <span class="flex-1 text-sm text-gray-200 group-hover:text-white truncate">{{ issue.title }}</span>
        <!-- Priority badge -->
        <span class="text-xs px-1.5 py-0.5 rounded" :class="priorityClass(issue.priority)">
          {{ priorityLabel(issue.priority) }}
        </span>
        <!-- Assignees -->
        <div class="flex -space-x-1">
          <div
            v-for="assignee in issue.assignees?.slice(0, 3)"
            :key="assignee.id"
            class="w-5 h-5 rounded-full bg-brand-700 border border-gray-900 flex items-center justify-center text-[10px] font-bold text-white"
            :title="assignee.user?.username ?? assignee.agent?.name ?? 'Unknown'"
          >
            {{ (assignee.user?.username ?? assignee.agent?.name ?? '?').charAt(0).toUpperCase() }}
          </div>
        </div>
      </NuxtLink>

      <!-- Empty state -->
      <div v-if="store.issues.length === 0 && !store.loading" class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
        <p class="text-gray-400">No issues found for this filter.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useIssuesStore } from '~/stores/issues'
import { IssueStatus, IssuePriority } from '~/types'

const route = useRoute()
const store = useIssuesStore()

type FeedFilter = 'my' | 'open' | 'unassigned' | 'waiting'

const filter = computed<FeedFilter>(() => {
  const f = route.query.filter as string
  if (f === 'open' || f === 'unassigned' || f === 'waiting') return f
  return 'my'
})

const filterTitle = computed(() => {
  switch (filter.value) {
    case 'open': return 'Open Issues'
    case 'unassigned': return 'Unassigned Issues'
    case 'waiting': return 'Waiting for Human'
    default: return 'My Issues'
  }
})

const filterDescription = computed(() => {
  switch (filter.value) {
    case 'open': return 'All open issues across your projects'
    case 'unassigned': return 'Issues with no assignees'
    case 'waiting': return 'Issues assigned to an agent waiting for human input'
    default: return 'Issues assigned to you across all projects'
  }
})

watch(filter, (val) => {
  store.fetchFeed(val)
}, { immediate: true })

function statusColor(status: IssueStatus) {
  switch (status) {
    case IssueStatus.Backlog: return 'bg-gray-500'
    case IssueStatus.Todo: return 'bg-blue-500'
    case IssueStatus.InProgress: return 'bg-yellow-500'
    case IssueStatus.InReview: return 'bg-purple-500'
    case IssueStatus.Done: return 'bg-green-500'
    case IssueStatus.Cancelled: return 'bg-red-400'
    default: return 'bg-gray-500'
  }
}

function priorityLabel(priority: IssuePriority) {
  switch (priority) {
    case IssuePriority.Urgent: return 'Urgent'
    case IssuePriority.High: return 'High'
    case IssuePriority.Medium: return 'Medium'
    case IssuePriority.Low: return 'Low'
    default: return ''
  }
}

function priorityClass(priority: IssuePriority) {
  switch (priority) {
    case IssuePriority.Urgent: return 'bg-red-900/50 text-red-400'
    case IssuePriority.High: return 'bg-orange-900/50 text-orange-400'
    case IssuePriority.Medium: return 'bg-yellow-900/50 text-yellow-400'
    case IssuePriority.Low: return 'bg-blue-900/50 text-blue-400'
    default: return 'hidden'
  }
}
</script>

