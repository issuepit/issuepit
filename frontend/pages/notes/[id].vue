<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4 shrink-0">
      <div class="flex items-center gap-3">
        <PageBreadcrumb :items="[
          { label: 'Notes', to: '/notes', icon: 'M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z' },
          { label: store.currentNote?.title ?? 'Loading...' },
        ]" />
      </div>
      <div class="flex items-center gap-2">
        <span class="text-xs text-gray-500">
          v{{ store.currentNote?.version ?? 0 }}
        </span>
        <select v-if="store.currentNote" v-model="editStatus"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="draft">Draft</option>
          <option value="published">Published</option>
          <option value="archived">Archived</option>
        </select>
        <button @click="saveNote" :disabled="saving"
          class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-4 py-1.5 rounded-lg disabled:opacity-50 transition-colors font-medium">
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
        <button @click="confirmDelete"
          class="text-xs bg-red-600/20 hover:bg-red-600/30 text-red-400 px-3 py-1.5 rounded-lg transition-colors">
          Delete
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.currentNote" class="flex-1 flex items-center justify-center text-gray-500">
      Loading note...
    </div>

    <!-- Editor -->
    <div v-else-if="store.currentNote" class="flex-1 flex flex-col overflow-hidden">
      <!-- Title -->
      <input v-model="editTitle" placeholder="Note title"
        class="bg-transparent text-2xl font-bold text-white border-none focus:outline-none mb-4 px-0" />

      <!-- Tags -->
      <div class="flex items-center gap-2 mb-4 flex-wrap">
        <span v-for="tm in store.currentNote.tagMappings" :key="tm.tagId"
          class="text-xs px-2 py-0.5 rounded-full border"
          :style="{ color: tm.tag.color, borderColor: tm.tag.color }">
          {{ tm.tag.name }}
        </span>
      </div>

      <!-- Linked notes -->
      <div v-if="store.currentNote.incomingLinks.length > 0" class="mb-4">
        <h4 class="text-xs text-gray-500 uppercase tracking-wide mb-1">Linked from</h4>
        <div class="flex flex-wrap gap-1">
          <NuxtLink v-for="link in store.currentNote.incomingLinks" :key="link.id"
            :to="link.sourceNoteId ? `/notes/${link.sourceNoteId}` : '#'"
            class="text-xs bg-gray-800 text-brand-400 hover:text-brand-300 px-2 py-1 rounded-md border border-gray-700">
            ← {{ link.linkText }}
          </NuxtLink>
        </div>
      </div>

      <!-- WYSIWYG editor (CRDT-enabled: passes noteId + clientId for operation-based sync) -->
      <div class="flex-1 overflow-hidden">
        <NoteEditor
          v-model="editContent"
          :notebook-id="store.currentNote.notebookId"
          :note-id="noteId"
          :client-id="crdtClientId"
          :last-seq="crdtLastSeq"
          @seq-updated="crdtLastSeq = $event"
        />
      </div>

      <!-- Outgoing links -->
      <div v-if="store.currentNote.outgoingLinks.length > 0" class="mt-4 shrink-0">
        <h4 class="text-xs text-gray-500 uppercase tracking-wide mb-1">Links to</h4>
        <div class="flex flex-wrap gap-1">
          <NuxtLink v-for="link in store.currentNote.outgoingLinks" :key="link.id"
            :to="link.targetNoteId ? `/notes/${link.targetNoteId}` : '#'"
            class="text-xs bg-gray-800 text-brand-400 hover:text-brand-300 px-2 py-1 rounded-md border border-gray-700">
            → {{ link.linkText }}
          </NuxtLink>
        </div>
      </div>
    </div>

    <!-- Not found -->
    <div v-else class="flex-1 flex items-center justify-center text-gray-500">
      Note not found.
    </div>

    <!-- Delete Confirmation Modal -->
    <ConfirmModal v-if="showDeleteConfirm" title="Delete Note"
      :message="`Are you sure you want to delete &quot;${store.currentNote?.title}&quot;? This action cannot be undone.`"
      confirm-text="Delete" @confirm="executeDelete" @cancel="showDeleteConfirm = false" />

    <!-- Version Conflict Modal -->
    <div v-if="versionConflict" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="versionConflict = false">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6">
        <h2 class="text-lg font-semibold text-yellow-400 mb-2">Version Conflict</h2>
        <p class="text-sm text-gray-300 mb-4">This note has been modified by another user. Please reload and try again.</p>
        <div class="flex justify-end gap-2">
          <button @click="versionConflict = false"
            class="text-xs bg-gray-700 hover:bg-gray-600 text-gray-300 px-4 py-2 rounded-lg transition-colors">
            Close
          </button>
          <button @click="reloadNote"
            class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-4 py-2 rounded-lg transition-colors font-medium">
            Reload
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { NoteStatus } from '~/types'
import { NoteStatus as NoteStatusEnum } from '~/types'

const route = useRoute()
const router = useRouter()
const store = useNotesStore()

const noteId = computed(() => route.params.id as string)

const editTitle = ref('')
const editContent = ref('')
const editStatus = ref<NoteStatus>(NoteStatusEnum.Draft)
const saving = ref(false)
const showDeleteConfirm = ref(false)
const versionConflict = ref(false)

// CRDT: stable per-tab client ID (persisted across page refreshes)
const crdtClientId = (() => {
  if (typeof window === 'undefined') return crypto.randomUUID()
  const key = 'notes-crdt-client-id'
  let id = localStorage.getItem(key)
  if (!id) { id = crypto.randomUUID(); localStorage.setItem(key, id) }
  return id
})()

/** Last confirmed CRDT sequence number; updated by NoteEditor as ops are acknowledged. */
const crdtLastSeq = ref(0)

async function saveNote() {
  if (!store.currentNote) return
  saving.value = true
  try {
    await store.updateNote(noteId.value, {
      title: editTitle.value,
      content: editContent.value,
      status: editStatus.value,
      expectedVersion: store.currentNote.version,
    })
  } catch (e: unknown) {
    if (e && typeof e === 'object' && 'statusCode' in e && (e as { statusCode: number }).statusCode === 409) {
      versionConflict.value = true
    }
  } finally {
    saving.value = false
  }
}

function confirmDelete() {
  showDeleteConfirm.value = true
}

async function executeDelete() {
  showDeleteConfirm.value = false
  if (store.currentNote) {
    await store.deleteNote(store.currentNote.id)
    router.push('/notes')
  }
}

async function reloadNote() {
  versionConflict.value = false
  await store.fetchNote(noteId.value)
  if (store.currentNote) {
    editTitle.value = store.currentNote.title
    editContent.value = store.currentNote.content
    editStatus.value = store.currentNote.status
  }
}

// Load note on mount
onMounted(async () => {
  await store.fetchNote(noteId.value)
  if (store.currentNote) {
    editTitle.value = store.currentNote.title
    editContent.value = store.currentNote.content
    editStatus.value = store.currentNote.status
  }
})

// Auto-save after idle time (3 seconds)
let saveTimeout: ReturnType<typeof setTimeout> | null = null
watch([editTitle, editContent, editStatus], () => {
  if (saveTimeout) clearTimeout(saveTimeout)
  saveTimeout = setTimeout(() => {
    if (store.currentNote && (
      editTitle.value !== store.currentNote.title ||
      editContent.value !== store.currentNote.content ||
      editStatus.value !== store.currentNote.status
    )) {
      saveNote()
    }
  }, 3000)
})
</script>
