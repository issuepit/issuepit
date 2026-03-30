<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <div class="flex items-center gap-3">
        <PageBreadcrumb :items="[
          { label: 'Notes', to: '/notes', icon: 'M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z' },
        ]" />
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full font-normal">
          {{ filteredNotes.length }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <!-- View toggle -->
        <div class="flex bg-gray-800 rounded-lg p-0.5 border border-gray-700">
          <button @click="view = 'list'"
            :class="['px-3 py-1 text-xs rounded-md transition-colors', view === 'list' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            List
          </button>
          <button @click="view = 'graph'"
            :class="['px-3 py-1 text-xs rounded-md transition-colors', view === 'graph' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            Graph
          </button>
        </div>

        <!-- Notebook selector -->
        <select v-if="store.notebooks.length" v-model="activeNotebookId"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="">All Notebooks</option>
          <option v-for="nb in store.notebooks" :key="nb.id" :value="nb.id">{{ nb.name }}</option>
        </select>

        <!-- Manage notebooks -->
        <button @click="showNotebooks = true"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
          Notebooks
        </button>

        <!-- New note -->
        <button @click="openCreate"
          class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-3 py-1.5 rounded-lg transition-colors font-medium">
          + Note
        </button>
      </div>
    </div>

    <!-- Search -->
    <div class="mb-4 shrink-0">
      <input v-model="searchQuery" type="text" placeholder="Search notes..."
        class="w-full bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-sm text-gray-200 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
    </div>

    <!-- List View -->
    <div v-if="view === 'list'" class="flex-1 overflow-y-auto space-y-2">
      <div v-if="store.loading" class="text-center text-gray-500 py-8">Loading notes...</div>
      <div v-else-if="filteredNotes.length === 0" class="text-center text-gray-500 py-8">
        No notes yet. Create your first note!
      </div>
      <NuxtLink v-for="note in filteredNotes" :key="note.id" :to="`/notes/${note.id}`"
        class="block bg-gray-800/50 hover:bg-gray-800 border border-gray-700 rounded-lg p-4 transition-colors">
        <div class="flex items-start justify-between">
          <div class="min-w-0 flex-1">
            <h3 class="text-sm font-medium text-gray-200 truncate">{{ note.title }}</h3>
            <p class="text-xs text-gray-500 mt-1 line-clamp-2">{{ note.content.substring(0, 200) }}</p>
            <div class="flex items-center gap-2 mt-2">
              <span :class="statusClass(note.status)" class="text-xs px-2 py-0.5 rounded-full">
                {{ note.status }}
              </span>
              <span v-for="tm in note.tagMappings" :key="tm.tagId"
                class="text-xs px-2 py-0.5 rounded-full border border-gray-600"
                :style="{ color: tm.tag.color, borderColor: tm.tag.color }">
                {{ tm.tag.name }}
              </span>
            </div>
          </div>
          <div class="text-xs text-gray-600 whitespace-nowrap ml-4">
            <DateDisplay :date="note.updatedAt" mode="relative" />
          </div>
        </div>
      </NuxtLink>
    </div>

    <!-- Graph View -->
    <div v-if="view === 'graph'" class="flex-1 overflow-hidden">
      <NoteGraphView :notebook-id="activeNotebookId || undefined" />
    </div>

    <!-- Notebook Management Modal -->
    <div v-if="showNotebooks" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showNotebooks = false">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Notebooks</h2>
          <button @click="showNotebooks = false" class="text-gray-400 hover:text-white text-xl">&times;</button>
        </div>
        <!-- Create notebook form -->
        <div class="flex gap-2 mb-4">
          <input v-model="newNotebookName" placeholder="Notebook name" class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-200 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          <button @click="createNotebook" :disabled="!newNotebookName.trim()"
            class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-4 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
            Create
          </button>
        </div>
        <div class="space-y-2 max-h-60 overflow-y-auto">
          <div v-for="nb in store.notebooks" :key="nb.id" class="flex items-center justify-between bg-gray-800/50 rounded-lg p-3">
            <div>
              <span class="text-sm text-gray-200">{{ nb.name }}</span>
              <span class="text-xs text-gray-500 ml-2">{{ nb.storageProvider }}</span>
            </div>
            <button @click="confirmDeleteNotebook(nb)" class="text-xs text-red-400 hover:text-red-300">Delete</button>
          </div>
          <div v-if="store.notebooks.length === 0" class="text-center text-gray-500 text-sm py-4">No notebooks</div>
        </div>
      </div>
    </div>

    <!-- Create Note Modal -->
    <div v-if="showCreate" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showCreate = false">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">New Note</h2>
          <button @click="showCreate = false" class="text-gray-400 hover:text-white text-xl">&times;</button>
        </div>
        <div class="space-y-3">
          <input v-model="createForm.title" placeholder="Note title" class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-200 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          <select v-model="createForm.notebookId"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
            <option value="" disabled>Select notebook</option>
            <option v-for="nb in store.notebooks" :key="nb.id" :value="nb.id">{{ nb.name }}</option>
          </select>
          <div class="flex justify-end gap-2 pt-2">
            <button @click="showCreate = false"
              class="text-xs bg-gray-700 hover:bg-gray-600 text-gray-300 px-4 py-2 rounded-lg transition-colors">
              Cancel
            </button>
            <button @click="submitCreate" :disabled="!createForm.title.trim() || !createForm.notebookId"
              class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-4 py-2 rounded-lg disabled:opacity-50 transition-colors font-medium">
              Create
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <ConfirmModal v-if="deleteTarget" title="Delete Notebook" :message="`Are you sure you want to delete &quot;${deleteTarget.name}&quot;? This will permanently delete all notes in this notebook. This action cannot be undone.`"
      confirm-text="Delete" @confirm="executeDeleteNotebook" @cancel="deleteTarget = null" />
  </div>
</template>

<script setup lang="ts">
import type { Notebook, NoteStatus } from '~/types'
import { NoteStatus as NoteStatusEnum, StorageProvider } from '~/types'

const store = useNotesStore()
const router = useRouter()

const view = ref<'list' | 'graph'>('list')
const activeNotebookId = ref('')
const searchQuery = ref('')
const showNotebooks = ref(false)
const showCreate = ref(false)
const newNotebookName = ref('')
const deleteTarget = ref<Notebook | null>(null)

const createForm = ref({
  title: '',
  notebookId: '',
})

const filteredNotes = computed(() => {
  let result = store.notes
  if (activeNotebookId.value) {
    result = result.filter(n => n.notebookId === activeNotebookId.value)
  }
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase()
    result = result.filter(n =>
      n.title.toLowerCase().includes(q) || n.content.toLowerCase().includes(q)
    )
  }
  return result
})

function statusClass(status: NoteStatus) {
  switch (status) {
    case NoteStatusEnum.Draft: return 'bg-yellow-900/50 text-yellow-400'
    case NoteStatusEnum.Published: return 'bg-green-900/50 text-green-400'
    case NoteStatusEnum.Archived: return 'bg-gray-700 text-gray-400'
    default: return 'bg-gray-700 text-gray-400'
  }
}

function openCreate() {
  createForm.value = { title: '', notebookId: activeNotebookId.value || (store.notebooks[0]?.id ?? '') }
  showCreate.value = true
}

async function submitCreate() {
  try {
    const note = await store.createNote({
      notebookId: createForm.value.notebookId,
      title: createForm.value.title,
      status: NoteStatusEnum.Draft,
    })
    showCreate.value = false
    router.push(`/notes/${note.id}`)
  } catch {
    // error handled by store
  }
}

async function createNotebook() {
  if (!newNotebookName.value.trim()) return
  await store.createNotebook({
    name: newNotebookName.value.trim(),
    storageProvider: StorageProvider.Postgres,
  })
  newNotebookName.value = ''
}

function confirmDeleteNotebook(nb: Notebook) {
  deleteTarget.value = nb
}

async function executeDeleteNotebook() {
  if (deleteTarget.value) {
    await store.deleteNotebook(deleteTarget.value.id)
    deleteTarget.value = null
  }
}

// Fetch data on mount
onMounted(async () => {
  await store.fetchNotebooks()
  await store.fetchNotes()
})

// Re-fetch notes when notebook filter changes
watch(activeNotebookId, async () => {
  await store.fetchNotes(activeNotebookId.value ? { notebookId: activeNotebookId.value } : undefined)
})

// Debounced search
let searchTimeout: ReturnType<typeof setTimeout> | null = null
watch(searchQuery, () => {
  if (searchTimeout) clearTimeout(searchTimeout)
  searchTimeout = setTimeout(async () => {
    await store.fetchNotes({
      notebookId: activeNotebookId.value || undefined,
      search: searchQuery.value || undefined,
    })
  }, 300)
})
</script>
