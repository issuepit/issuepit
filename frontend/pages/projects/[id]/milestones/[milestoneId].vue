<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="progress">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-3 mb-6">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
          { label: 'Milestones', to: `/projects/${id}/milestones`, icon: 'M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9' },
          { label: progress.title, to: `/projects/${id}/milestones/${milestoneId}`, icon: 'M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9' },
        ]" />
        <span :class="progress.status === 'open' ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
          class="text-xs px-2 py-0.5 rounded-full font-medium">
          {{ progress.status === 'open' ? 'Open' : 'Closed' }}
        </span>
      <!-- Breadcrumb + Actions -->
      <!-- <div class="flex items-start justify-between gap-4 mb-6">
        <div class="flex items-center gap-2 flex-wrap">
          <NuxtLink :to="`/projects/${id}`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">{{ projectsStore.currentProject?.name || 'Project' }}</NuxtLink>
          <span class="text-gray-600">/</span>
          <NuxtLink :to="`/projects/${id}/milestones`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">Milestones</NuxtLink>
          <span class="text-gray-600">/</span>
          <NuxtLink :to="`/projects/${id}/milestones/${milestoneId}`" class="text-xl font-bold text-white">{{ progress.title }}</NuxtLink>
          <span :class="progress.status === 'open' ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
            class="text-xs px-2 py-0.5 rounded-full font-medium">
            {{ progress.status === 'open' ? 'Open' : 'Closed' }}
          </span>
        </div> -->
        <!-- Action buttons -->
        <div class="flex items-center gap-2 shrink-0">
          <button @click="openEdit"
            class="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 border border-gray-700 transition-colors">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
            Edit
          </button>
          <button @click="toggleStatus"
            :class="progress.status === 'open'
              ? 'text-gray-400 hover:text-gray-200 border-gray-700 hover:bg-gray-800'
              : 'text-green-400 hover:text-green-300 border-green-800 hover:bg-green-900/20'"
            class="flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-lg border transition-colors">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path v-if="progress.status === 'open'" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M5 13l4 4L19 7" />
              <path v-else stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ progress.status === 'open' ? 'Close milestone' : 'Reopen milestone' }}
          </button>
        </div>
      </div>

      <!-- Sub-header: description + dates -->
      <div v-if="progress.description || progress.dueDate || progress.startDate" class="mb-6 mt-2">
        <p v-if="progress.description" class="text-gray-400">{{ progress.description }}</p>
        <div class="flex items-center gap-4 mt-1">
          <p v-if="progress.startDate" class="text-sm text-gray-500">
            Start <DateDisplay :date="progress.startDate" mode="absolute" resolution="date" />
          </p>
          <p v-if="progress.dueDate" class="text-sm text-gray-500">
            Due <DateDisplay :date="progress.dueDate" mode="absolute" resolution="date" />
          </p>
        </div>
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
        <div class="px-5 py-3 border-b border-gray-800 flex items-center justify-between">
          <h2 class="text-sm font-medium text-gray-300">Issues</h2>
          <span class="text-xs text-gray-500">{{ issues.length }}</span>
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
              @click="navigateTo(`/projects/${id}/issues/${issue.number}`)">
              <td class="px-4 py-3 w-8">
                <span :class="statusDotColor(issue.status)" class="w-3 h-3 rounded-full block"></span>
              </td>
              <td class="px-4 py-3">
                <div class="flex items-center gap-2">
                  <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.currentProject, issue.externalId, issue.externalSource) }}</span>
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

    <!-- Edit Modal -->
    <div v-if="showEdit" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Edit Milestone</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="editForm.title" type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="editForm.description" rows="3"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Start Date</label>
              <input v-model="editForm.startDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Due Date</label>
              <input v-model="editForm.dueDate" type="date"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitEdit"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Save Changes
          </button>
          <button @click="showEdit = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Issue } from '~/types'
import { IssueStatus } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useMilestonesStore } from '~/stores/milestones'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string
const milestoneId = route.params.milestoneId as string

const api = useApi()
const projectsStore = useProjectsStore()
const milestonesStore = useMilestonesStore()
const loading = ref(true)
const progress = ref<{
  id: string; title: string; description?: string; startDate?: string; dueDate?: string; status: string;
  total: number; open: number; inProgress: number; done: number; percent: number
} | null>(null)
const issues = ref<Issue[]>([])
const showEdit = ref(false)
const editForm = reactive({ title: '', description: '', startDate: '', dueDate: '' })

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

function openEdit() {
  if (!progress.value) return
  editForm.title = progress.value.title
  editForm.description = progress.value.description ?? ''
  editForm.startDate = progress.value.startDate ? progress.value.startDate.split('T')[0] : ''
  editForm.dueDate = progress.value.dueDate ? progress.value.dueDate.split('T')[0] : ''
  showEdit.value = true
}

async function submitEdit() {
  if (!progress.value || !editForm.title) return
  const updated = await milestonesStore.updateMilestone(id, milestoneId, {
    title: editForm.title,
    description: editForm.description || undefined,
    startDate: editForm.startDate || undefined,
    dueDate: editForm.dueDate || undefined,
    status: progress.value.status as 'open' | 'closed',
  })
  if (updated && progress.value) {
    progress.value.title = updated.title
    progress.value.description = updated.description
    progress.value.startDate = updated.startDate
    progress.value.dueDate = updated.dueDate
  }
  showEdit.value = false
}

async function toggleStatus() {
  if (!progress.value) return
  const newStatus = progress.value.status === 'open' ? 'closed' : 'open'
  const updated = await milestonesStore.updateMilestone(id, milestoneId, {
    title: progress.value.title,
    description: progress.value.description,
    startDate: progress.value.startDate,
    dueDate: progress.value.dueDate,
    status: newStatus,
  })
  if (updated && progress.value) {
    progress.value.status = updated.status
  }
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
