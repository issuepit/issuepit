<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-3">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
          { label: 'Issues', to: `/projects/${id}/issues`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' },
        ]" />
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">
          {{ store.filteredIssues.length }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <button @click="showVoiceCreate = true"
          @dragover.prevent="voiceDragOver = true"
          @dragleave="voiceDragOver = false"
          @drop.prevent="handleVoiceFileDrop"
          :class="[
            'flex items-center gap-2 text-gray-300 text-sm font-medium px-4 py-2 rounded-lg transition-colors',
            voiceDragOver ? 'bg-brand-700 ring-2 ring-brand-400' : 'bg-gray-800 hover:bg-gray-700'
          ]"
          title="Create issue from voice (or drop an audio file here)">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
          </svg>
          Voice
        </button>
        <button @click="showCreate = true"
          class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          New Issue
        </button>
      </div>
    </div>

    <!-- Filters -->
    <div class="flex flex-wrap items-center gap-2 mb-5">
      <input v-model="search" type="text" placeholder="Search issues..."
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 w-56" />

      <select v-model="filterStatus"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Status</option>
        <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
      </select>

      <select v-model="filterPriority"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Priority</option>
        <option v-for="p in priorities" :key="p.value" :value="p.value">{{ p.label }}</option>
      </select>

      <select v-model="filterType"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Types</option>
        <option v-for="t in types" :key="t.value" :value="t.value">{{ t.label }}</option>
      </select>

      <select v-model="filterMilestone"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Milestones</option>
        <option v-for="m in milestonesStore.milestones" :key="m.id" :value="m.id">{{ m.title }}</option>
      </select>

      <button v-if="hasFilters" @click="clearFilters"
        class="text-xs text-gray-400 hover:text-gray-200 px-2 py-1.5">Clear</button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Issues Table -->
    <div v-else class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
      <div v-if="store.filteredIssues.length === 0" class="py-16 text-center">
        <p class="text-gray-400">No issues found</p>
        <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
          Create the first issue →
        </button>
      </div>

      <table v-else class="w-full">
        <thead>
          <tr class="border-b border-gray-800">
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 w-8"></th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3">Title</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden md:table-cell">Status</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden lg:table-cell">Priority</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden lg:table-cell">Type</th>
            <th class="text-left text-xs font-medium text-gray-500 px-4 py-3 hidden xl:table-cell">Updated</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="issue in store.filteredIssues" :key="issue.id"
            class="border-b border-gray-800/50 hover:bg-gray-800/40 cursor-pointer transition-colors"
            @click="$router.push(`/projects/${id}/issues/${issue.number}`)">
            <td class="px-4 py-3">
              <span :class="statusIcon(issue.status).color" class="w-3.5 h-3.5 rounded-full block"></span>
            </td>
            <td class="px-4 py-3">
              <div class="flex items-center gap-2">
                <span class="text-xs text-gray-600">{{ formatIssueId(issue.number, projectsStore.currentProject) }}</span>
                <span class="text-sm text-gray-200 hover:text-white">{{ issue.title }}</span>
              </div>
            </td>
            <td class="px-4 py-3 hidden md:table-cell">
              <StatusBadge :status="issue.status" />
            </td>
            <td class="px-4 py-3 hidden lg:table-cell">
              <PriorityBadge :priority="issue.priority" />
            </td>
            <td class="px-4 py-3 hidden lg:table-cell">
              <span class="text-xs text-gray-400 capitalize">{{ issue.type }}</span>
            </td>
            <td class="px-4 py-3 hidden xl:table-cell">
              <span class="text-xs text-gray-500">{{ formatDate(issue.updatedAt) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="form.title" type="text" placeholder="Issue title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.body" rows="4" placeholder="Describe the issue..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Status</label>
              <select v-model="form.status"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Priority</label>
              <select v-model="form.priority"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="p in priorities" :key="p.value" :value="p.value">{{ p.label }}</option>
              </select>
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Type</label>
            <select v-model="form.type"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="t in types" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
    <!-- Voice Create Modal -->
    <div v-if="showVoiceCreate"
      class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue from Voice</h2>

        <!-- Recording controls -->
        <div class="flex flex-col items-center gap-4 mb-5">
          <!-- Mic button (also a drop zone for audio files) -->
          <button
            v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            @click="startVoiceRecording"
            @dragover.prevent="modalDragOver = true"
            @dragleave="modalDragOver = false"
            @drop.prevent="handleModalVoiceFileDrop"
            :class="[
              'w-16 h-16 rounded-full flex items-center justify-center transition-all shadow-lg',
              modalDragOver ? 'bg-brand-500 ring-4 ring-brand-300 scale-110' : 'bg-brand-600 hover:bg-brand-700'
            ]"
            title="Click to record or drop an audio file">
            <svg class="w-7 h-7 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
          </button>
          <p v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            class="text-sm text-gray-400">Click to start recording or drop an audio file</p>

          <!-- Recording indicator -->
          <div v-if="voice.recording.value" class="flex flex-col items-center gap-3">
            <div class="relative w-16 h-16">
              <div class="absolute inset-0 rounded-full bg-red-500/20 animate-ping"></div>
              <button @click="stopVoiceRecording"
                class="relative w-16 h-16 rounded-full bg-red-600 hover:bg-red-700 flex items-center justify-center transition-colors shadow-lg">
                <svg class="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 24 24">
                  <rect x="6" y="6" width="12" height="12" rx="1" />
                </svg>
              </button>
            </div>
            <p class="text-sm text-red-400 font-medium">Recording… click to stop</p>
          </div>

          <!-- Uploading / transcribing indicator -->
          <div v-if="voice.uploading.value" class="flex flex-col items-center gap-2">
            <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
            <p class="text-sm text-gray-400">Transcribing…</p>
          </div>
        </div>

        <!-- Transcription result -->
        <div v-if="voice.transcription.value || voiceRecordingDone" class="space-y-3 mb-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Transcription (editable)</label>
            <textarea v-model="voice.transcription.value" rows="4" placeholder="Transcription will appear here…"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <!-- Transcription warning (no model configured, no speech detected, or error from backend) -->
          <p v-if="voice.transcriptionWarning.value && !voice.transcription.value"
            class="text-xs text-amber-400">
            {{ voice.transcriptionWarning.value }}
          </p>
        </div>

        <!-- Error -->
        <p v-if="voice.error.value" class="text-sm text-red-400 mb-4">{{ voice.error.value }}</p>

        <!-- Actions -->
        <div class="flex gap-3">
          <button v-if="voiceRecordingDone && !voice.uploading.value" @click="submitVoiceCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="closeVoiceModal"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority, IssueType } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const id = route.params.id as string
const store = useIssuesStore()
const milestonesStore = useMilestonesStore()
const projectsStore = useProjectsStore()

const showCreate = ref(false)
const showVoiceCreate = ref(false)
const voiceRecordingDone = ref(false)
const voiceDragOver = ref(false)
const modalDragOver = ref(false)
const search = ref('')
const filterStatus = ref<IssueStatus | ''>('')
const filterPriority = ref<IssuePriority | ''>('')
const filterType = ref<IssueType | ''>('')
const filterMilestone = ref<string>('')

const voice = useVoiceRecorder()

async function startVoiceRecording() {
  voiceRecordingDone.value = false
  await voice.startRecording()
}

async function uploadVoiceFile(file: File | Blob) {
  voiceRecordingDone.value = true
  try {
    await voice.uploadRecording(file)
  } catch {
    // voice.error is already set by uploadRecording; the modal stays open so the user sees it
  }
}

async function handleVoiceFileDrop(e: DragEvent) {
  voiceDragOver.value = false
  const file = e.dataTransfer?.files[0]
  if (!file) return
  if (!file.type.startsWith('audio/') && !file.name.toLowerCase().endsWith('.wav')) return
  showVoiceCreate.value = true
  await uploadVoiceFile(file)
}

async function handleModalVoiceFileDrop(e: DragEvent) {
  modalDragOver.value = false
  const file = e.dataTransfer?.files[0]
  if (!file) return
  if (!file.type.startsWith('audio/') && !file.name.toLowerCase().endsWith('.wav')) return
  await uploadVoiceFile(file)
}

async function stopVoiceRecording() {
  const wavBlob = voice.stopRecording()
  if (wavBlob) {
    await uploadVoiceFile(wavBlob)
  } else {
    voiceRecordingDone.value = true
  }
}

async function submitVoiceCreate() {
  const title = `Voice Issue - ${new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })}`
  const newIssue = await store.createIssue(id, {
    title,
    body: voice.transcription.value,
    status: IssueStatus.Todo,
    priority: IssuePriority.Medium,
    type: IssueType.Issue,
  })
  // Attach the voice recording (private — only visible to the creator)
  if (newIssue && voice.lastWavBlob.value) {
    try {
      const audioFile = new File([voice.lastWavBlob.value], 'recording.wav', { type: 'audio/wav' })
      await store.addAttachment(newIssue.id, audioFile, true, false)
    } catch (e) {
      console.warn('Could not attach voice file to new issue', e)
    }
  }
  closeVoiceModal()
}

function closeVoiceModal() {
  voice.reset()
  voiceRecordingDone.value = false
  showVoiceCreate.value = false
}

const form = reactive({
  title: '',
  body: '',
  status: IssueStatus.Todo,
  priority: IssuePriority.Medium,
  type: IssueType.Issue
})

const statuses = [
  { value: IssueStatus.Backlog, label: 'Backlog' },
  { value: IssueStatus.Todo, label: 'Todo' },
  { value: IssueStatus.InProgress, label: 'In Progress' },
  { value: IssueStatus.InReview, label: 'In Review' },
  { value: IssueStatus.Done, label: 'Done' },
  { value: IssueStatus.Cancelled, label: 'Cancelled' }
]

const { priorities } = usePriority()

const types = [
  { value: IssueType.Issue, label: '📋 Issue' },
  { value: IssueType.Bug, label: '🐛 Bug' },
  { value: IssueType.Feature, label: '✨ Feature' },
  { value: IssueType.Task, label: '✅ Task' },
  { value: IssueType.Epic, label: '⚡ Epic' }
]

const hasFilters = computed(() => search.value || filterStatus.value || filterPriority.value || filterType.value || filterMilestone.value)

watch([search, filterStatus, filterPriority, filterType, filterMilestone], () => {
  store.setFilters({
    search: search.value || undefined,
    status: filterStatus.value || undefined,
    priority: filterPriority.value || undefined,
    type: filterType.value || undefined,
    milestoneId: filterMilestone.value || undefined,
  })
})

onMounted(() => {
  store.fetchIssues(id)
  milestonesStore.fetchMilestones(id)
  projectsStore.fetchProject(id)
})

function clearFilters() {
  search.value = ''
  filterStatus.value = ''
  filterPriority.value = ''
  filterType.value = ''
  filterMilestone.value = ''
  store.clearFilters()
}

async function submitCreate() {
  if (!form.title) return
  await store.createIssue(id, form)
  showCreate.value = false
  Object.assign(form, { title: '', body: '', status: IssueStatus.Todo, priority: IssuePriority.Medium, type: IssueType.Issue })
}

function statusIcon(status: IssueStatus) {
  const map: Record<IssueStatus, { color: string }> = {
    [IssueStatus.Backlog]: { color: 'bg-gray-500' },
    [IssueStatus.Todo]: { color: 'bg-blue-400' },
    [IssueStatus.InProgress]: { color: 'bg-yellow-400' },
    [IssueStatus.InReview]: { color: 'bg-purple-400' },
    [IssueStatus.Done]: { color: 'bg-green-400' },
    [IssueStatus.Cancelled]: { color: 'bg-red-400' }
  }
  return map[status] ?? { color: 'bg-gray-500' }
}

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

// Inline badge components
const StatusBadge = defineComponent({
  props: { status: String },
  setup(props) {
    const map: Record<string, string> = {
      backlog: 'bg-gray-800 text-gray-400',
      todo: 'bg-blue-900/40 text-blue-300',
      in_progress: 'bg-yellow-900/40 text-yellow-300',
      in_review: 'bg-purple-900/40 text-purple-300',
      done: 'bg-green-900/40 text-green-300',
      cancelled: 'bg-red-900/40 text-red-400'
    }
    const labels: Record<string, string> = {
      backlog: 'Backlog', todo: 'Todo', in_progress: 'In Progress',
      in_review: 'In Review', done: 'Done', cancelled: 'Cancelled'
    }
    return () => h('span', {
      class: `text-xs px-2 py-0.5 rounded-full font-medium ${map[props.status!] ?? 'bg-gray-800 text-gray-400'}`
    }, labels[props.status!] ?? props.status)
  }
})

const PriorityBadge = defineComponent({
  props: { priority: String },
  setup(props) {
    const { priorityIcon, priorityLabel } = usePriority()
    return () => h('span', { class: 'text-xs text-gray-400' },
      `${priorityIcon(props.priority ?? '')} ${priorityLabel(props.priority ?? '')}`
    )
  }
})
</script>
