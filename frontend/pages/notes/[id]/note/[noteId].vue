<template>
  <div class="h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between p-4 border-b border-gray-800 shrink-0">
      <div class="flex items-center gap-3">
        <NuxtLink
          :to="`/notes/${workspaceId}`"
          class="text-gray-400 hover:text-white transition-colors"
        >
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <input
          v-model="title"
          type="text"
          class="text-xl font-bold bg-transparent border-none outline-none focus:ring-0 w-full max-w-xl"
          placeholder="Untitled Note"
          @blur="saveNote"
        >
      </div>
      <div class="flex items-center gap-3 text-sm text-gray-400">
        <span v-if="saving" class="text-brand-400">Saving…</span>
        <span v-else-if="lastSaved">
          Saved <DateDisplay :date="lastSaved" mode="relative" />
        </span>
        <span class="text-xs bg-gray-800 px-2 py-1 rounded">v{{ version }}</span>
        <div class="flex bg-gray-800 rounded-lg p-0.5 border border-gray-700">
          <button
            :class="editorMode === 'edit' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-white'"
            class="px-3 py-1 rounded-md text-xs font-medium transition-colors"
            @click="editorMode = 'edit'"
          >
            Edit
          </button>
          <button
            :class="editorMode === 'preview' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-white'"
            class="px-3 py-1 rounded-md text-xs font-medium transition-colors"
            @click="editorMode = 'preview'"
          >
            Preview
          </button>
          <button
            :class="editorMode === 'split' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-white'"
            class="px-3 py-1 rounded-md text-xs font-medium transition-colors"
            @click="editorMode = 'split'"
          >
            Split
          </button>
        </div>
      </div>
    </div>

    <!-- Version Conflict Banner -->
    <div
      v-if="versionConflict"
      class="mx-4 mt-2 p-3 bg-yellow-900/50 border border-yellow-700 rounded-lg flex items-center justify-between shrink-0"
    >
      <span class="text-sm text-yellow-200">
        This note was modified by another user. Your changes could not be saved.
      </span>
      <button
        class="px-3 py-1 bg-yellow-700 hover:bg-yellow-600 text-white rounded text-sm font-medium"
        @click="reloadNote"
      >
        Reload Latest
      </button>
    </div>

    <!-- Editor Area -->
    <div class="flex-1 overflow-hidden flex">
      <!-- Edit Pane -->
      <div
        v-if="editorMode === 'edit' || editorMode === 'split'"
        :class="editorMode === 'split' ? 'w-1/2 border-r border-gray-800' : 'w-full'"
        class="flex flex-col"
      >
        <textarea
          v-model="content"
          class="flex-1 p-6 bg-transparent text-gray-200 font-mono text-sm resize-none outline-none leading-relaxed"
          placeholder="Start writing in markdown…&#10;&#10;Use [[Note Title]] to link to other notes.&#10;Use [[issue:ID]] or [[todo:ID]] to link to IssuePit entities."
          @input="onContentChange"
        />
      </div>

      <!-- Preview Pane -->
      <div
        v-if="editorMode === 'preview' || editorMode === 'split'"
        :class="editorMode === 'split' ? 'w-1/2' : 'w-full'"
        class="overflow-y-auto p-6"
      >
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="prose prose-invert prose-sm max-w-none" v-html="renderedContent" />
      </div>
    </div>

    <!-- Links Panel -->
    <div v-if="store.currentNote?.links?.length" class="border-t border-gray-800 p-4 shrink-0">
      <h3 class="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
        Links ({{ store.currentNote.links.length }})
      </h3>
      <div class="flex flex-wrap gap-2">
        <span
          v-for="link in store.currentNote.links"
          :key="link.id"
          class="px-2 py-1 bg-gray-800 border border-gray-700 rounded text-xs text-gray-300"
        >
          {{ link.rawLinkText }}
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import DOMPurify from 'dompurify'
import { marked } from 'marked'

const route = useRoute()
const store = useNotesStore()

const workspaceId = computed(() => route.params.id as string)
const noteId = computed(() => route.params.noteId as string)

const title = ref('')
const content = ref('')
const version = ref(1)
const editorMode = ref<'edit' | 'preview' | 'split'>('edit')
const saving = ref(false)
const lastSaved = ref<string | null>(null)
const versionConflict = ref(false)
let saveTimeout: ReturnType<typeof setTimeout> | null = null

onMounted(async () => {
  await store.fetchNote(noteId.value)
  if (store.currentNote) {
    title.value = store.currentNote.title
    content.value = store.currentNote.content
    version.value = store.currentNote.version
    lastSaved.value = store.currentNote.updatedAt
  }
})

function onContentChange() {
  versionConflict.value = false
  // Auto-save after 1.5s of idle time
  if (saveTimeout) clearTimeout(saveTimeout)
  saveTimeout = setTimeout(() => saveNote(), 1500)
}

async function saveNote() {
  if (!title.value && !content.value) return
  saving.value = true
  versionConflict.value = false
  try {
    const note = await store.updateNote(noteId.value, {
      title: title.value,
      content: content.value,
      expectedVersion: version.value,
    })
    version.value = note.version
    lastSaved.value = note.updatedAt
  }
  catch (e: unknown) {
    if (e && typeof e === 'object' && 'statusCode' in e && (e as { statusCode: number }).statusCode === 409) {
      versionConflict.value = true
    }
  }
  finally {
    saving.value = false
  }
}

async function reloadNote() {
  await store.fetchNote(noteId.value)
  if (store.currentNote) {
    title.value = store.currentNote.title
    content.value = store.currentNote.content
    version.value = store.currentNote.version
    lastSaved.value = store.currentNote.updatedAt
    versionConflict.value = false
  }
}

// Render markdown with wiki-link support, sanitized via DOMPurify
const renderedContent = computed(() => {
  // Pre-process wiki links before passing to marked: convert [[text]] to a styled span
  const preprocessed = content.value.replace(
    /\[\[([^\]]+)\]\]/g,
    '<span class="text-brand-400 bg-brand-500/10 px-1 rounded cursor-pointer hover:underline">$1</span>',
  )
  const html = marked(preprocessed, { async: false }) as string
  return DOMPurify.sanitize(html)
})
</script>
