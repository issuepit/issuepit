<template>
  <div class="p-8 max-w-4xl">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentIssue" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentIssue">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <NuxtLink :to="`/projects/${id}`" class="hover:text-gray-300">Project</NuxtLink>
        <span>/</span>
        <NuxtLink :to="`/projects/${id}/issues`" class="hover:text-gray-300">Issues</NuxtLink>
        <span>/</span>
        <span class="text-gray-400">#{{ store.currentIssue.number }}</span>
      </div>

      <div class="flex gap-8">
        <!-- Main Content -->
        <div class="flex-1 min-w-0">
          <!-- Title & Status -->
          <div class="flex items-start gap-3 mb-4">
            <span :class="statusColor(store.currentIssue.status)"
              class="w-3 h-3 rounded-full mt-1.5 shrink-0"></span>
            <div class="flex-1">
              <h1 v-if="!editingTitle" @click="editingTitle = true"
                class="text-xl font-bold text-white cursor-text hover:text-brand-300 transition-colors">
                {{ store.currentIssue.title }}
              </h1>
              <input v-else v-model="titleEdit" @blur="saveTitle" @keyup.enter="saveTitle"
                class="w-full text-xl font-bold bg-transparent border-b border-brand-500 text-white focus:outline-none pb-0.5" />
            </div>
          </div>

          <!-- Body -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Description</h2>
            <div v-if="!editingBody" @click="editingBody = true"
              class="text-sm text-gray-300 cursor-text min-h-16 whitespace-pre-wrap">
              {{ store.currentIssue.body || 'Click to add description...' }}
            </div>
            <div v-else>
              <textarea v-model="bodyEdit" rows="6"
                class="w-full bg-transparent text-sm text-gray-300 focus:outline-none resize-none"
                placeholder="Describe this issue..."></textarea>
              <div class="flex gap-2 mt-3">
                <button @click="saveBody"
                  class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1 rounded-md transition-colors">Save</button>
                <button @click="editingBody = false"
                  class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1 rounded-md transition-colors">Cancel</button>
              </div>
            </div>
          </div>

          <!-- Sub-issues -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Sub-Issues</h2>
            <p class="text-sm text-gray-600">No sub-issues</p>
          </div>
        </div>

        <!-- Sidebar metadata -->
        <div class="w-64 shrink-0 space-y-4">
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 space-y-4">
            <!-- Status -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Status</p>
              <select :value="store.currentIssue.status" @change="updateStatus($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="backlog">Backlog</option>
                <option value="todo">Todo</option>
                <option value="in_progress">In Progress</option>
                <option value="in_review">In Review</option>
                <option value="done">Done</option>
                <option value="cancelled">Cancelled</option>
              </select>
            </div>

            <!-- Priority -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Priority</p>
              <select :value="store.currentIssue.priority" @change="updatePriority($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="urgent">🔴 Urgent</option>
                <option value="high">🟠 High</option>
                <option value="medium">🟡 Medium</option>
                <option value="low">🔵 Low</option>
                <option value="no_priority">⚪ No Priority</option>
              </select>
            </div>

            <!-- Type -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Type</p>
              <span class="text-sm text-gray-300 capitalize">{{ store.currentIssue.type }}</span>
            </div>

            <!-- Labels -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Labels</p>
              <p v-if="!store.currentIssue.labelIds?.length" class="text-sm text-gray-600">None</p>
              <div v-else class="flex flex-wrap gap-1">
                <span v-for="lid in store.currentIssue.labelIds" :key="lid"
                  class="text-xs bg-blue-900/40 text-blue-300 px-1.5 py-0.5 rounded">{{ lid }}</span>
              </div>
            </div>

            <!-- Dates -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Created</p>
              <p class="text-xs text-gray-400">{{ formatDate(store.currentIssue.createdAt) }}</p>
            </div>
            <div v-if="store.currentIssue.dueDate">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Due</p>
              <p class="text-xs text-gray-400">{{ formatDate(store.currentIssue.dueDate) }}</p>
            </div>
          </div>

          <!-- Delete -->
          <button @click="deleteAndGoBack"
            class="w-full text-xs text-red-400 hover:text-red-300 hover:bg-red-900/20 border border-red-900/30 rounded-lg py-2 transition-colors">
            Delete Issue
          </button>
        </div>
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400">Issue not found</p>
      <NuxtLink :to="`/projects/${id}/issues`" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Issues</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus } from '~/types'
import type { IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const issueId = route.params.issueId as string
const store = useIssuesStore()

const editingTitle = ref(false)
const editingBody = ref(false)
const titleEdit = ref('')
const bodyEdit = ref('')

onMounted(async () => {
  await store.fetchIssue(id, issueId)
  if (store.currentIssue) {
    titleEdit.value = store.currentIssue.title
    bodyEdit.value = store.currentIssue.body ?? ''
  }
})

async function saveTitle() {
  editingTitle.value = false
  if (titleEdit.value && titleEdit.value !== store.currentIssue?.title) {
    await store.updateIssue(id, issueId, { title: titleEdit.value })
  }
}

async function saveBody() {
  editingBody.value = false
  await store.updateIssue(id, issueId, { body: bodyEdit.value })
}

async function updateStatus(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssueStatus
  await store.updateIssue(id, issueId, { status: val })
}

async function updatePriority(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssuePriority
  await store.updateIssue(id, issueId, { priority: val })
}

async function deleteAndGoBack() {
  await store.deleteIssue(id, issueId)
  router.push(`/projects/${id}/issues`)
}

function statusColor(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}
</script>
