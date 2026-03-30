<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <div class="flex items-center gap-3">
        <NuxtLink to="/notes" class="text-gray-400 hover:text-white transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <h1 class="text-2xl font-bold">{{ workspace?.name || 'Workspace' }}</h1>
        <span v-if="store.notes.length" class="text-sm text-gray-400">
          {{ store.notes.length }} note{{ store.notes.length !== 1 ? 's' : '' }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <!-- View Toggle -->
        <div class="flex bg-gray-800 rounded-lg p-0.5 border border-gray-700">
          <button
            :class="view === 'list' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-white'"
            class="px-3 py-1 rounded-md text-xs font-medium transition-colors"
            @click="view = 'list'"
          >
            List
          </button>
          <button
            :class="view === 'graph' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-white'"
            class="px-3 py-1 rounded-md text-xs font-medium transition-colors"
            @click="view = 'graph'; loadGraph()"
          >
            Graph
          </button>
        </div>
        <button
          class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium transition-colors"
          @click="showCreateNote = true"
        >
          + Note
        </button>
      </div>
    </div>

    <!-- Search -->
    <div v-if="view === 'list'" class="mb-4 shrink-0">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Search notes…"
        class="w-full max-w-md px-3 py-2 bg-gray-800 border border-gray-700 rounded-lg text-sm focus:outline-none focus:border-brand-500"
        @input="debouncedSearch"
      >
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.notes.length" class="flex-1 flex items-center justify-center">
      <span class="text-gray-400">Loading notes…</span>
    </div>

    <!-- List View -->
    <div v-else-if="view === 'list'" class="overflow-y-auto flex-1">
      <!-- Empty State -->
      <div
        v-if="!store.notes.length"
        class="flex flex-col items-center justify-center h-full text-gray-400"
      >
        <p class="text-lg mb-2">No notes yet</p>
        <p class="text-sm mb-4">Create your first note in this workspace.</p>
        <button
          class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium"
          @click="showCreateNote = true"
        >
          Create First Note
        </button>
      </div>

      <!-- Notes List -->
      <div v-else class="space-y-2">
        <NuxtLink
          v-for="note in store.notes"
          :key="note.id"
          :to="`/notes/${workspaceId}/note/${note.id}`"
          class="block bg-gray-800 border border-gray-700 rounded-lg p-4 hover:border-brand-500 transition-colors group"
        >
          <div class="flex items-center justify-between">
            <h3 class="font-medium group-hover:text-brand-400 transition-colors">
              {{ note.title }}
            </h3>
            <div class="flex items-center gap-3 text-xs text-gray-500">
              <span>v{{ note.version }}</span>
              <DateDisplay :date="note.updatedAt" mode="relative" />
              <button
                class="text-gray-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-opacity"
                title="Delete note"
                @click.prevent="confirmDeleteNote(note)"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                  />
                </svg>
              </button>
            </div>
          </div>
        </NuxtLink>
      </div>
    </div>

    <!-- Graph View -->
    <div v-else-if="view === 'graph'" class="overflow-hidden flex-1 bg-gray-900 rounded-lg border border-gray-700">
      <div v-if="!store.graphData" class="flex items-center justify-center h-full text-gray-400">
        Loading graph…
      </div>
      <div v-else-if="!store.graphData.nodes.length" class="flex items-center justify-center h-full text-gray-400">
        No notes to visualize. Create some notes with [[wiki links]] to see the graph.
      </div>
      <canvas
        v-else
        ref="graphCanvas"
        class="w-full h-full"
        @mousedown="onGraphMouseDown"
        @mousemove="onGraphMouseMove"
        @mouseup="onGraphMouseUp"
      />
    </div>

    <!-- Create Note Modal -->
    <div
      v-if="showCreateNote"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      @click.self="showCreateNote = false"
    >
      <div class="bg-gray-800 border border-gray-700 rounded-xl p-6 w-full max-w-md">
        <h2 class="text-lg font-semibold mb-4">Create Note</h2>
        <form @submit.prevent="handleCreateNote">
          <div class="mb-4">
            <label class="block text-sm text-gray-300 mb-1">Title</label>
            <input
              v-model="newNoteTitle"
              type="text"
              required
              class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-sm focus:outline-none focus:border-brand-500"
              placeholder="Note title"
            >
          </div>
          <div class="flex justify-end gap-2">
            <button
              type="button"
              class="px-4 py-2 text-sm text-gray-400 hover:text-white"
              @click="showCreateNote = false"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium"
            >
              Create
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <div
      v-if="noteToDelete"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      @click.self="noteToDelete = null"
    >
      <div class="bg-gray-800 border border-gray-700 rounded-xl p-6 w-full max-w-md">
        <h2 class="text-lg font-semibold mb-2">Delete Note</h2>
        <p class="text-sm text-gray-400 mb-4">
          Are you sure you want to delete <strong class="text-white">{{ noteToDelete.title }}</strong>?
          This action cannot be undone.
        </p>
        <div class="flex justify-end gap-2">
          <button
            type="button"
            class="px-4 py-2 text-sm text-gray-400 hover:text-white"
            @click="noteToDelete = null"
          >
            Cancel
          </button>
          <button
            class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg text-sm font-medium"
            @click="handleDeleteNote"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { NoteListItem } from '~/types'

const route = useRoute()
const router = useRouter()
const store = useNotesStore()

const workspaceId = computed(() => route.params.id as string)
const workspace = computed(() => store.workspaces.find(w => w.id === workspaceId.value))

const view = ref<'list' | 'graph'>('list')
const searchQuery = ref('')
const showCreateNote = ref(false)
const newNoteTitle = ref('')
const noteToDelete = ref<NoteListItem | null>(null)
const graphCanvas = ref<HTMLCanvasElement | null>(null)

let searchTimeout: ReturnType<typeof setTimeout> | null = null

onMounted(async () => {
  if (!store.workspaces.length) await store.fetchWorkspaces()
  await store.fetchNotes(workspaceId.value)
})

function debouncedSearch() {
  if (searchTimeout) clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    store.fetchNotes(workspaceId.value, searchQuery.value || undefined)
  }, 300)
}

async function handleCreateNote() {
  const note = await store.createNote({
    workspaceId: workspaceId.value,
    title: newNoteTitle.value,
  })
  newNoteTitle.value = ''
  showCreateNote.value = false
  router.push(`/notes/${workspaceId.value}/note/${note.id}`)
}

function confirmDeleteNote(note: NoteListItem) {
  noteToDelete.value = note
}

async function handleDeleteNote() {
  if (!noteToDelete.value) return
  await store.deleteNote(noteToDelete.value.id)
  noteToDelete.value = null
}

async function loadGraph() {
  await store.fetchGraphData(workspaceId.value)
  await nextTick()
  renderGraph()
}

// ── Simple Canvas Graph Rendering ───────────────────────────────────

interface NodePosition { x: number; y: number; id: string; title: string }
const nodePositions = ref<NodePosition[]>([])
let dragNode: NodePosition | null = null

function renderGraph() {
  const canvas = graphCanvas.value
  if (!canvas || !store.graphData) return

  const rect = canvas.getBoundingClientRect()
  canvas.width = rect.width * window.devicePixelRatio
  canvas.height = rect.height * window.devicePixelRatio
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  ctx.scale(window.devicePixelRatio, window.devicePixelRatio)

  // Position nodes in a circle if not already positioned
  if (nodePositions.value.length !== store.graphData.nodes.length) {
    const cx = rect.width / 2
    const cy = rect.height / 2
    const radius = Math.min(rect.width, rect.height) * 0.35
    nodePositions.value = store.graphData.nodes.map((node, i) => {
      const angle = (2 * Math.PI * i) / store.graphData!.nodes.length - Math.PI / 2
      return { x: cx + radius * Math.cos(angle), y: cy + radius * Math.sin(angle), id: node.id, title: node.title }
    })
  }

  // Clear
  ctx.clearRect(0, 0, rect.width, rect.height)

  // Draw edges
  ctx.strokeStyle = '#4b5563'
  ctx.lineWidth = 1.5
  for (const edge of store.graphData.edges) {
    const src = nodePositions.value.find(n => n.id === edge.sourceId)
    const tgt = edge.targetNoteId ? nodePositions.value.find(n => n.id === edge.targetNoteId) : null
    if (src && tgt) {
      ctx.beginPath()
      ctx.moveTo(src.x, src.y)
      ctx.lineTo(tgt.x, tgt.y)
      ctx.stroke()
    }
  }

  // Draw nodes
  for (const node of nodePositions.value) {
    ctx.beginPath()
    ctx.arc(node.x, node.y, 20, 0, 2 * Math.PI)
    ctx.fillStyle = '#6366f1'
    ctx.fill()
    ctx.strokeStyle = '#818cf8'
    ctx.lineWidth = 2
    ctx.stroke()

    ctx.fillStyle = '#e5e7eb'
    ctx.font = '12px sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(node.title.substring(0, 15), node.x, node.y + 35)
  }
}

function onGraphMouseDown(e: MouseEvent) {
  const canvas = graphCanvas.value
  if (!canvas) return
  const rect = canvas.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  dragNode = nodePositions.value.find(n => Math.hypot(n.x - x, n.y - y) < 20) || null
}

function onGraphMouseMove(e: MouseEvent) {
  if (!dragNode) return
  const canvas = graphCanvas.value
  if (!canvas) return
  const rect = canvas.getBoundingClientRect()
  dragNode.x = e.clientX - rect.left
  dragNode.y = e.clientY - rect.top
  renderGraph()
}

function onGraphMouseUp() {
  dragNode = null
}
</script>
