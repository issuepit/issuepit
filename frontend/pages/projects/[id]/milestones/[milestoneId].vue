<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="progress">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 mb-6">
        <NuxtLink :to="`/projects/${id}`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">{{ projectsStore.currentProject?.name || 'Project' }}</NuxtLink>
        <span class="text-gray-600">/</span>
        <NuxtLink :to="`/projects/${id}/milestones`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">Milestones</NuxtLink>
        <span class="text-gray-600">/</span>
        <NuxtLink :to="`/projects/${id}/milestones/${milestoneId}`" class="text-xl font-bold text-white">{{ progress.title }}</NuxtLink>
        <span :class="progress.status === 'open' ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
          class="text-xs px-2 py-0.5 rounded-full font-medium">
          {{ progress.status === 'open' ? 'Open' : 'Closed' }}
        </span>
      </div>

      <!-- Sub-header: description + due date -->
      <div v-if="progress.description || progress.dueDate" class="mb-6 mt-2">
        <p v-if="progress.description" class="text-gray-400">{{ progress.description }}</p>
        <p v-if="progress.dueDate" class="text-sm text-gray-500 mt-1">
          Due {{ formatDate(progress.dueDate) }}
        </p>
      </div>

      <!-- Progress Card -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 mb-6">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-sm font-medium text-gray-300">Progress</h2>
          <span class="text-2xl font-bold text-white">{{ progress.percent }}%</span>
        </div>
        <div class="w-full bg-gray-800 rounded-full h-3 mb-4 overflow-hidden">
          <div class="bg-brand-500 h-3 rounded-full transition-all duration-500"
            :style="{ width: progress.percent + '%' }"></div>
        </div>
        <div class="grid grid-cols-3 gap-4">
          <div class="text-center">
            <p class="text-xl font-bold text-gray-200">{{ progress.open }}</p>
            <p class="text-xs text-gray-500 mt-0.5">Open</p>
          </div>
          <div class="text-center">
            <p class="text-xl font-bold text-yellow-400">{{ progress.inProgress }}</p>
            <p class="text-xs text-gray-500 mt-0.5">In Progress</p>
          </div>
          <div class="text-center">
            <p class="text-xl font-bold text-green-400">{{ progress.done }}</p>
            <p class="text-xs text-gray-500 mt-0.5">Done</p>
          </div>
        </div>
        <p class="text-xs text-gray-600 text-center mt-3">{{ progress.total }} total issues</p>
      </div>

      <!-- Issues in this milestone -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-800">
          <h2 class="text-sm font-medium text-gray-300">Issues</h2>
        </div>
        <div v-if="issues.length === 0" class="py-12 text-center">
          <p class="text-gray-500 text-sm">No issues assigned to this milestone</p>
          <NuxtLink :to="`/projects/${id}/issues`" class="mt-2 text-brand-400 hover:text-brand-300 text-sm block">
            Go to Issues →
          </NuxtLink>
        </div>
        <table v-else class="w-full">
          <tbody>
            <tr v-for="issue in issues" :key="issue.id"
              class="border-b border-gray-800/50 hover:bg-gray-800/40 cursor-pointer transition-colors"
              @click="$router.push(`/projects/${id}/issues/${issue.number}`)">
              <td class="px-4 py-3 w-8">
                <span :class="statusDotColor(issue.status)" class="w-3 h-3 rounded-full block"></span>
              </td>
              <td class="px-4 py-3">
                <div class="flex items-center gap-2">
                  <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.currentProject) }}</span>
                  <span class="text-sm text-gray-200">{{ issue.title }}</span>
                </div>
              </td>
              <td class="px-4 py-3 hidden md:table-cell">
                <span :class="statusBadgeClass(issue.status)"
                  class="text-xs px-2 py-0.5 rounded-full font-medium">
                  {{ statusLabel(issue.status) }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400">Milestone not found</p>
      <NuxtLink :to="`/projects/${id}/milestones`" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
        ← Back to Milestones
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Issue } from '~/types'
import { IssueStatus } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string
const milestoneId = route.params.milestoneId as string

const api = useApi()
const projectsStore = useProjectsStore()
const loading = ref(true)
const progress = ref<{
  id: string; title: string; description?: string; dueDate?: string; status: string;
  total: number; open: number; inProgress: number; done: number; percent: number
} | null>(null)
const issues = ref<Issue[]>([])

onMounted(async () => {
  projectsStore.fetchProject(id)
  try {
    const [prog, issueList] = await Promise.all([
      api.get<typeof progress.value>(`/api/projects/${id}/milestones/${milestoneId}/progress`),
      api.get<Issue[]>('/api/issues', { params: { projectId: id } }),
    ])
    progress.value = prog
    issues.value = (issueList as Issue[]).filter((i: Issue) => i.milestoneId === milestoneId)
  } catch {
    progress.value = null
  } finally {
    loading.value = false
  }
})

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}

function statusDotColor(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400',
  }
  return map[status] ?? 'bg-gray-500'
}

function statusBadgeClass(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-800 text-gray-400',
    [IssueStatus.Todo]: 'bg-blue-900/40 text-blue-300',
    [IssueStatus.InProgress]: 'bg-yellow-900/40 text-yellow-300',
    [IssueStatus.InReview]: 'bg-purple-900/40 text-purple-300',
    [IssueStatus.Done]: 'bg-green-900/40 text-green-300',
    [IssueStatus.Cancelled]: 'bg-red-900/40 text-red-400',
  }
  return map[status] ?? 'bg-gray-800 text-gray-400'
}

function statusLabel(status: IssueStatus) {
  const labels: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'Backlog',
    [IssueStatus.Todo]: 'Todo',
    [IssueStatus.InProgress]: 'In Progress',
    [IssueStatus.InReview]: 'In Review',
    [IssueStatus.Done]: 'Done',
    [IssueStatus.Cancelled]: 'Cancelled',
  }
  return labels[status] ?? status
}
</script>
