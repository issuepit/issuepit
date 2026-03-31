<template>
  <div class="note-editor flex flex-col h-full overflow-hidden">
    <!-- Toolbar -->
    <div v-if="editor" class="flex items-center gap-1 p-2 bg-gray-800/50 border border-gray-700 rounded-t-lg flex-wrap shrink-0">
      <button @click="editor.chain().focus().toggleBold().run()"
        :class="toolbarBtnClass(editor.isActive('bold'))" title="Bold">
        <span class="font-bold">B</span>
      </button>
      <button @click="editor.chain().focus().toggleItalic().run()"
        :class="toolbarBtnClass(editor.isActive('italic'))" title="Italic">
        <span class="italic">I</span>
      </button>
      <button @click="editor.chain().focus().toggleStrike().run()"
        :class="toolbarBtnClass(editor.isActive('strike'))" title="Strikethrough">
        <span class="line-through">S</span>
      </button>
      <button @click="editor.chain().focus().toggleCode().run()"
        :class="toolbarBtnClass(editor.isActive('code'))" title="Inline code">
        <span class="font-mono text-xs">&lt;/&gt;</span>
      </button>

      <div class="w-px h-5 bg-gray-600 mx-1" />

      <button @click="editor.chain().focus().toggleHeading({ level: 1 }).run()"
        :class="toolbarBtnClass(editor.isActive('heading', { level: 1 }))" title="Heading 1">
        H1
      </button>
      <button @click="editor.chain().focus().toggleHeading({ level: 2 }).run()"
        :class="toolbarBtnClass(editor.isActive('heading', { level: 2 }))" title="Heading 2">
        H2
      </button>
      <button @click="editor.chain().focus().toggleHeading({ level: 3 }).run()"
        :class="toolbarBtnClass(editor.isActive('heading', { level: 3 }))" title="Heading 3">
        H3
      </button>

      <div class="w-px h-5 bg-gray-600 mx-1" />

      <button @click="editor.chain().focus().toggleBulletList().run()"
        :class="toolbarBtnClass(editor.isActive('bulletList'))" title="Bullet list">
        •
      </button>
      <button @click="editor.chain().focus().toggleOrderedList().run()"
        :class="toolbarBtnClass(editor.isActive('orderedList'))" title="Ordered list">
        1.
      </button>
      <button @click="editor.chain().focus().toggleBlockquote().run()"
        :class="toolbarBtnClass(editor.isActive('blockquote'))" title="Blockquote">
        &ldquo;
      </button>
      <button @click="editor.chain().focus().toggleCodeBlock().run()"
        :class="toolbarBtnClass(editor.isActive('codeBlock'))" title="Code block">
        <span class="font-mono text-xs">{}</span>
      </button>

      <div class="w-px h-5 bg-gray-600 mx-1" />

      <!-- Text color -->
      <div class="relative">
        <input type="color" :value="editor.getAttributes('textStyle').color || '#e5e7eb'"
          @input="(e: Event) => editor!.chain().focus().setColor((e.target as HTMLInputElement).value).run()"
          class="w-6 h-6 rounded cursor-pointer border border-gray-600 bg-transparent"
          title="Text color" />
      </div>

      <!-- Highlight color -->
      <button @click="editor.chain().focus().toggleHighlight({ color: '#fbbf24' }).run()"
        :class="toolbarBtnClass(editor.isActive('highlight'))" title="Highlight">
        <span class="bg-yellow-400/40 px-0.5 rounded">A</span>
      </button>

      <div class="w-px h-5 bg-gray-600 mx-1" />

      <button @click="editor.chain().focus().setHorizontalRule().run()"
        :class="toolbarBtnClass(false)" title="Horizontal rule">
        ―
      </button>

      <div class="flex-1" />

      <!-- Upload indicator -->
      <span v-if="uploading" class="text-xs text-gray-400 animate-pulse">Uploading...</span>
      <span v-if="uploadError" class="text-xs text-red-400">{{ uploadError }}</span>
    </div>

    <!-- Editor content -->
    <div class="flex-1 overflow-y-auto bg-gray-800/30 border border-t-0 border-gray-700 rounded-b-lg">
      <EditorContent :editor="editor"
        class="prose prose-invert prose-sm max-w-none p-4 min-h-full note-editor-content" />
    </div>

    <!-- Wiki-link autocomplete popup -->
    <div v-if="showSuggestion" ref="suggestionPopup"
      class="fixed z-50 bg-gray-800 border border-gray-600 rounded-lg shadow-xl max-h-48 overflow-y-auto min-w-[200px]"
      :style="{ top: suggestionPos.top + 'px', left: suggestionPos.left + 'px' }">
      <div v-if="suggestionLoading" class="p-3 text-xs text-gray-400">Searching...</div>
      <div v-else-if="suggestionItems.length === 0" class="p-3 text-xs text-gray-500">No notes found</div>
      <button v-for="(item, i) in suggestionItems" :key="item.id"
        @click="selectSuggestion(item)"
        :class="['block w-full text-left px-3 py-2 text-sm transition-colors', i === suggestionIndex ? 'bg-brand-600 text-white' : 'text-gray-300 hover:bg-gray-700']">
        {{ item.title }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Link from '@tiptap/extension-link'
import { TextStyle } from '@tiptap/extension-text-style'
import Color from '@tiptap/extension-color'
import Highlight from '@tiptap/extension-highlight'
import Placeholder from '@tiptap/extension-placeholder'
import Image from '@tiptap/extension-image'
import type { Note } from '~/types'

const WIKI_LINK_MAX_QUERY_LENGTH = 50

const props = defineProps<{
  modelValue: string
  notebookId?: string
  /** Note ID — when provided, enables CRDT operation-based collaborative editing. */
  noteId?: string
  /** Per-session client UUID for echo-suppression (so we don't re-apply our own ops). */
  clientId?: string
  /** Last confirmed sequence number from the server (passed in from parent). */
  lastSeq?: number
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  /** Emitted when the CRDT sync advances the sequence number. */
  (e: 'seq-updated', seq: number): void
}>()

// Image upload state
const uploading = ref(false)
const uploadError = ref<string | null>(null)

// Wiki-link suggestion state
const showSuggestion = ref(false)
const suggestionItems = ref<Note[]>([])
const suggestionIndex = ref(0)
const suggestionLoading = ref(false)
const suggestionQuery = ref('')
const suggestionPos = ref({ top: 0, left: 0 })
const suggestionPopup = ref<HTMLElement | null>(null)
let suggestionRange: { from: number; to: number } | null = null

const config = useRuntimeConfig()
const notesApiBase = config.public.notesApiBase as string

// ── CRDT / OT collaborative editing ──────────────────────────────────────────
// Uses a polling-based OT model:
//  1. When the local content changes, we compute an OT delta (retain/insert/delete ops)
//     from the last server-confirmed content and submit it via POST /operations.
//  2. A background poll fetches remote operations every 5s and applies them locally.
//  3. If a remote op arrives while we have a pending local delta, we transform the
//     pending delta against the remote op before submitting (client-side OT).

const notesApi = useNotesApi()

/** Last content confirmed by the server (HTML string). Used as the "base document" for delta computation. */
let syncedContent = props.modelValue

/**
 * Update the TipTap editor with new content received from the server and notify the parent.
 * Suppresses the internal `onUpdate` event to avoid triggering a new delta submission.
 */
function applyRemoteContent(newContent: string) {
  if (editor.value) editor.value.commands.setContent(newContent, false)
  emit('update:modelValue', newContent)
}

/** Sequence number of the last server-confirmed operation (0 = none). */
let currentSeq = props.lastSeq ?? 0

/** Whether a submit is currently in flight. */
let submitting = false

/** Remote ops buffered while submitting — applied after confirmation. */
const bufferedRemoteOps: Array<{ delta: string; clientId: string }> = []

/**
 * Compute an OT delta (list of retain/insert/delete ops) from `oldText` to `newText`.
 * Returns a JSON string in Quill delta format: [{"retain":N},{"insert":"..."},{"delete":N}].
 */
function computeDelta(oldText: string, newText: string): string {
  if (oldText === newText) return '[]'

  // Common prefix
  let prefixLen = 0
  const maxPrefix = Math.min(oldText.length, newText.length)
  while (prefixLen < maxPrefix && oldText[prefixLen] === newText[prefixLen]) prefixLen++

  // Common suffix (excluding the already-matched prefix)
  let suffixLen = 0
  const maxSuffix = Math.min(oldText.length - prefixLen, newText.length - prefixLen)
  while (
    suffixLen < maxSuffix
    && oldText[oldText.length - 1 - suffixLen] === newText[newText.length - 1 - suffixLen]
  ) suffixLen++

  type Op = { retain?: number; insert?: string; delete?: number }
  const ops: Op[] = []
  if (prefixLen > 0) ops.push({ retain: prefixLen })

  const deletedLen = oldText.length - prefixLen - suffixLen
  const insertedText = newText.slice(prefixLen, newText.length - (suffixLen || 0))

  if (deletedLen > 0) ops.push({ delete: deletedLen })
  if (insertedText.length > 0) ops.push({ insert: insertedText })
  if (suffixLen > 0) ops.push({ retain: suffixLen })

  return JSON.stringify(ops)
}

/**
 * Apply an OT delta (JSON string) to a text, returning the new text.
 * Used to reconstruct remote content after applying server-confirmed ops.
 */
function applyDelta(text: string, deltaJson: string): string {
  type Op = { retain?: number; insert?: string; delete?: number }
  const ops: Op[] = JSON.parse(deltaJson)
  let result = ''
  let pos = 0
  for (const op of ops) {
    if (op.retain !== undefined) {
      result += text.slice(pos, pos + op.retain)
      pos += op.retain
    }
    else if (op.insert !== undefined) {
      result += op.insert
    }
    else if (op.delete !== undefined) {
      pos += op.delete
    }
  }
  result += text.slice(pos)
  return result
}

/**
 * Transform `pendingDelta` (local, not yet submitted) against `remoteDelta`
 * (already applied by the server) so the pending delta can still be applied
 * correctly on top of the remote change.
 *
 * This is the client-side half of OT: keeps local edits valid after receiving
 * concurrent server operations.
 */
function transformDelta(pendingJson: string, remoteJson: string): string {
  type Op = { retain?: number; insert?: string; delete?: number }
  const pending: Op[] = JSON.parse(pendingJson)
  const remote: Op[] = JSON.parse(remoteJson)

  if (pending.length === 0) return pendingJson

  const result: Op[] = []
  let ia = 0, ib = 0
  let remA = opLen(pending[ia]), remB = opLen(remote[ib])

  function getA() { return ia < pending.length ? pending[ia] : undefined }
  function getB() { return ib < remote.length ? remote[ib] : undefined }

  function appendMerge(op: Op) {
    const last = result[result.length - 1]
    if (last?.retain !== undefined && op.retain !== undefined) { last.retain += op.retain; return }
    if (last?.delete !== undefined && op.delete !== undefined) { last.delete += op.delete; return }
    if (last?.insert !== undefined && op.insert !== undefined) { last.insert += op.insert; return }
    result.push({ ...op })
  }

  while (ia < pending.length) {
    const curA = getA()!
    let curB = getB()

    // Drain b-inserts before processing a (b inserted new chars; a must skip over them)
    while (curB?.insert !== undefined && curA.insert === undefined) {
      appendMerge({ retain: curB.insert.length })
      ib++; remB = opLen(getB())
      curB = getB()
    }

    if (curA.insert !== undefined) {
      appendMerge({ insert: curA.insert })
      ia++; remA = opLen(getA())
      continue
    }

    if (curB === undefined) {
      appendMerge(curA)
      ia++
      continue
    }

    const take = Math.min(remA, remB)

    if (curA.retain !== undefined && curB.retain !== undefined) appendMerge({ retain: take })
    else if (curA.retain !== undefined && curB.delete !== undefined) { /* b deleted it */ }
    else if (curA.delete !== undefined && curB.retain !== undefined) appendMerge({ delete: take })
    else if (curA.delete !== undefined && curB.delete !== undefined) { /* both deleted */ }

    remA -= take
    remB -= take
    if (remA <= 0) { ia++; remA = opLen(getA()) }
    if (remB <= 0) { ib++; remB = opLen(getB()) }
  }

  // Drain any trailing b-inserts
  while (ib < remote.length) {
    const curB = remote[ib]
    if (curB.insert !== undefined) appendMerge({ retain: curB.insert.length })
    ib++
  }

  return JSON.stringify(result)
}

function opLen(op: { retain?: number; insert?: string; delete?: number } | undefined): number {
  if (!op) return 0
  if (op.retain !== undefined) return op.retain
  if (op.delete !== undefined) return op.delete
  if (op.insert !== undefined) return op.insert.length
  return 0
}

/** Debounce timer for CRDT operation submission */
let crdtSubmitTimer: ReturnType<typeof setTimeout> | null = null

/**
 * Schedule a CRDT delta submission after the user pauses typing (1 second).
 * If a submission is already in-flight, the next one is queued automatically.
 */
function scheduleCrdtSubmit(currentContent: string) {
  if (!props.noteId) return
  if (crdtSubmitTimer) clearTimeout(crdtSubmitTimer)
  crdtSubmitTimer = setTimeout(() => submitCrdtDelta(currentContent), 1000)
}

async function submitCrdtDelta(currentContent: string) {
  if (!props.noteId) return
  const delta = computeDelta(syncedContent, currentContent)
  if (delta === '[]') return // no change

  if (submitting) {
    // Buffer the current content; it will be resubmitted after the in-flight op confirms
    return
  }

  submitting = true
  try {
    const confirmed = await notesApi.submitOperation(
      props.noteId,
      delta,
      currentSeq,
      props.clientId ?? 'anon',
    )
    syncedContent = currentContent
    currentSeq = confirmed.sequenceNumber
    emit('seq-updated', currentSeq)

    // Apply any remote ops that arrived while we were submitting.
    // They were computed against the old content (before our delta), so we must
    // transform them against our delta before applying to the new syncedContent.
    if (bufferedRemoteOps.length > 0) {
      let merged = syncedContent
      let evolvedLocalDelta = delta
      for (const remote of bufferedRemoteOps) {
        if (remote.clientId === (props.clientId ?? 'anon')) continue // echo-suppress
        // Transform remote op so it can be applied after our local delta
        const transformedRemote = transformDelta(remote.delta, evolvedLocalDelta)
        merged = applyDelta(merged, transformedRemote)
        // Evolve local delta for subsequent remote ops in the buffer
        evolvedLocalDelta = transformDelta(evolvedLocalDelta, remote.delta)
      }
      bufferedRemoteOps.length = 0
      if (merged !== syncedContent) {
        syncedContent = merged
        if (editor.value && editor.value.getHTML() !== merged) {
          applyRemoteContent(merged)
        }
      }
    }
  }
  catch (e) {
    console.error('[NoteEditor] CRDT submit failed:', e)
  }
  finally {
    submitting = false
  }
}

// ── SignalR real-time delivery ────────────────────────────────────────────────
// The Notes API exposes a SignalR hub at /hubs/notes.  When the editor opens a note,
// it connects, joins the note's group, and receives OperationReceived events pushed
// by the server whenever another client submits an operation.
// A fallback catchup via getOperations() runs on first connect and after reconnects
// to recover any ops missed during a disconnection window.

/** Active SignalR connection (null when noteId is not provided or SSR). */
let signalrConn: import('@microsoft/signalr').HubConnection | null = null

/** Whether the fallback polling interval is active (used only when SignalR is unavailable). */
let pollInterval: ReturnType<typeof setInterval> | null = null

/**
 * Apply a single confirmed remote operation to the local editor.
 * Shared by both the SignalR event handler and the HTTP catchup path.
 */
async function applyRemoteOp(op: import('~/composables/useNotesApi').NoteOperationResponse) {
  // Echo-suppress: skip ops we submitted ourselves
  if (op.clientId === (props.clientId ?? 'anon')) {
    currentSeq = op.sequenceNumber
    emit('seq-updated', currentSeq)
    return
  }

  if (submitting) {
    bufferedRemoteOps.push({ delta: op.delta, clientId: op.clientId })
    currentSeq = op.sequenceNumber
    emit('seq-updated', currentSeq)
    return
  }

  const current = editor.value?.getHTML() ?? syncedContent
  try {
    const updated = applyDelta(current, op.delta)
    syncedContent = updated
    currentSeq = op.sequenceNumber
    emit('seq-updated', currentSeq)
    if (editor.value && editor.value.getHTML() !== updated) {
      applyRemoteContent(updated)
    }
  }
  catch (err) {
    console.warn('[NoteEditor] Failed to apply remote op, skipping:', err)
    currentSeq = op.sequenceNumber
    emit('seq-updated', currentSeq)
  }
}

/** HTTP catchup: fetch and apply any ops we missed (used on connect/reconnect). */
async function catchUpOps() {
  if (!props.noteId) return
  try {
    const ops = await notesApi.getOperations(props.noteId, currentSeq)
    for (const op of ops) await applyRemoteOp(op)
  }
  catch {
    // Non-fatal — will retry on next reconnect
  }
}

/** Fallback polling loop (activated only when SignalR fails to connect). */
async function pollRemoteOps() {
  await catchUpOps()
}

/** Connect to the Notes SignalR hub and join the note's group. */
async function connectSignalR() {
  if (!props.noteId || typeof window === 'undefined') return

  try {
    const { HubConnectionBuilder, LogLevel, HubConnectionState } = await import('@microsoft/signalr')

    signalrConn = new HubConnectionBuilder()
      .withUrl(`${notesApiBase}/hubs/notes`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    // Real-time operation delivery
    signalrConn.on('OperationReceived', (op: import('~/composables/useNotesApi').NoteOperationResponse) => {
      applyRemoteOp(op)
    })

    // Catchup on reconnect to recover ops missed during the disconnection window
    signalrConn.onreconnected(async () => {
      await catchUpOps()
    })

    await signalrConn.start()
    await signalrConn.invoke('JoinNote', props.noteId)

    // Catchup on first connect
    await catchUpOps()

    if (signalrConn.state !== HubConnectionState.Connected) throw new Error('not connected')
  }
  catch (err) {
    console.warn('[NoteEditor] SignalR unavailable, falling back to polling:', err)
    signalrConn = null
    // Activate 5s polling fallback
    if (!pollInterval) {
      pollInterval = setInterval(pollRemoteOps, 5000)
    }
  }
}


async function handleImageUpload(file: File): Promise<string | null> {
  uploading.value = true
  uploadError.value = null
  try {
    const body = new FormData()
    body.append('file', file)
    const result = await $fetch<{ url: string }>('/api/notes/uploads/image', {
      baseURL: notesApiBase,
      method: 'POST',
      body,
      credentials: 'include',
    })
    return result.url
  } catch (e) {
    console.error('[NoteEditor] Image upload failed:', e)
    uploadError.value = e instanceof Error ? e.message : 'Image upload failed. Please try again.'
    setTimeout(() => { uploadError.value = null }, 5000)
    return null
  } finally {
    uploading.value = false
  }
}

const editor = useEditor({
  content: props.modelValue,
  extensions: [
    StarterKit,
    Link.configure({
      openOnClick: false,
      HTMLAttributes: { class: 'text-brand-400 hover:text-brand-300 underline cursor-pointer' },
    }),
    TextStyle,
    Color,
    Highlight.configure({ multicolor: true }),
    Placeholder.configure({ placeholder: 'Start writing... Use [[ to link to other notes.' }),
    Image.configure({ inline: true, allowBase64: false }),
  ],
  editorProps: {
    attributes: {
      class: 'focus:outline-none min-h-full',
    },
    handleKeyDown: (_view, event) => {
      // Handle suggestion navigation
      if (showSuggestion.value) {
        if (event.key === 'ArrowDown') {
          event.preventDefault()
          suggestionIndex.value = Math.min(suggestionIndex.value + 1, suggestionItems.value.length - 1)
          return true
        }
        if (event.key === 'ArrowUp') {
          event.preventDefault()
          suggestionIndex.value = Math.max(suggestionIndex.value - 1, 0)
          return true
        }
        if (event.key === 'Enter' && suggestionItems.value.length > 0) {
          event.preventDefault()
          selectSuggestion(suggestionItems.value[suggestionIndex.value])
          return true
        }
        if (event.key === 'Escape') {
          event.preventDefault()
          closeSuggestion()
          return true
        }
      }
      return false
    },
    handlePaste: (_view, event) => {
      const items = event.clipboardData?.items
      if (!items) return false
      for (const item of items) {
        if (item.kind === 'file' && item.type.startsWith('image/')) {
          const file = item.getAsFile()
          if (!file) continue
          event.preventDefault()
          handleImageUpload(file).then(url => {
            if (url && editor.value) {
              editor.value.chain().focus().setImage({ src: url, alt: file.name }).run()
            }
          })
          return true
        }
      }
      return false
    },
    handleDrop: (_view, event) => {
      const files = event.dataTransfer?.files
      if (!files?.length) return false
      for (const file of files) {
        if (file.type.startsWith('image/')) {
          event.preventDefault()
          handleImageUpload(file).then(url => {
            if (url && editor.value) {
              editor.value.chain().focus().setImage({ src: url, alt: file.name }).run()
            }
          })
          return true
        }
      }
      return false
    },
  },
  onUpdate: ({ editor: e }) => {
    const html = e.getHTML()
    emit('update:modelValue', html)
    checkForWikiLink(e)
    // Schedule CRDT delta submission (only when noteId is provided)
    if (props.noteId) scheduleCrdtSubmit(html)
  },
})

// Watch for external content changes (e.g., initial load or forced refresh from parent)
watch(() => props.modelValue, (newVal) => {
  if (editor.value && editor.value.getHTML() !== newVal) {
    editor.value.commands.setContent(newVal, false)
    // Reset the synced baseline to the new externally-provided content
    syncedContent = newVal
  }
})

// Watch for lastSeq prop changes (parent may advance the seq on initial load)
watch(() => props.lastSeq, (newSeq) => {
  if (newSeq !== undefined && newSeq > currentSeq) currentSeq = newSeq
})

// Connect to SignalR hub (or fall back to polling) when a noteId is provided
onMounted(() => {
  if (props.noteId) {
    connectSignalR()
  }
})

// Wiki-link detection: check if cursor is inside [[ ... ]]
function checkForWikiLink(e: ReturnType<typeof useEditor>['value']) {
  if (!e) return
  const { from } = e.state.selection
  const lookback = WIKI_LINK_MAX_QUERY_LENGTH + 2 // +2 for the [[ prefix
  const textBefore = e.state.doc.textBetween(Math.max(0, from - lookback), from, '')

  const match = textBefore.match(new RegExp(`\\[\\[([^\\]]{0,${WIKI_LINK_MAX_QUERY_LENGTH}})$`))
  if (match) {
    const query = match[1]
    suggestionQuery.value = query
    suggestionRange = { from: from - match[0].length, to: from }

    // Get cursor position for popup
    const coords = e.view.coordsAtPos(from)
    suggestionPos.value = { top: coords.bottom + 4, left: coords.left }

    // Search for matching notes
    searchNotes(query)
    showSuggestion.value = true
    suggestionIndex.value = 0
  } else {
    closeSuggestion()
  }
}

let searchDebounce: ReturnType<typeof setTimeout> | null = null

async function searchNotes(query: string) {
  if (searchDebounce) clearTimeout(searchDebounce)
  suggestionLoading.value = true

  searchDebounce = setTimeout(async () => {
    try {
      const params: Record<string, string> = {}
      if (query) params.search = query
      if (props.notebookId) params.notebookId = props.notebookId

      const notes = await $fetch<Note[]>('/api/notes', {
        baseURL: notesApiBase,
        method: 'GET',
        params,
        credentials: 'include',
      })
      suggestionItems.value = notes.slice(0, 10)
    } catch {
      suggestionItems.value = []
    } finally {
      suggestionLoading.value = false
    }
  }, 150)
}

function selectSuggestion(note: Note) {
  if (!editor.value || !suggestionRange) return

  // Replace [[query with [[Note Title]]
  editor.value
    .chain()
    .focus()
    .deleteRange(suggestionRange)
    .insertContent(`[[${note.title}]]`)
    .run()

  closeSuggestion()
}

function closeSuggestion() {
  showSuggestion.value = false
  suggestionItems.value = []
  suggestionRange = null
}

function toolbarBtnClass(active: boolean) {
  return [
    'px-2 py-1 text-xs rounded transition-colors',
    active ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-700',
  ]
}

onBeforeUnmount(() => {
  if (pollInterval) clearInterval(pollInterval)
  if (crdtSubmitTimer) clearTimeout(crdtSubmitTimer)
  if (signalrConn) {
    // HubConnectionState.Connected === 'Connected' — use the string value to avoid
    // async import inside a synchronous lifecycle hook
    if (signalrConn.state === 'Connected' && props.noteId) {
      signalrConn.invoke('LeaveNote', props.noteId).catch(() => {})
    }
    signalrConn.stop().catch(() => {})
  }
  editor.value?.destroy()
})
</script>

<style>
.note-editor-content .tiptap {
  min-height: 100%;
}

.note-editor-content .tiptap p.is-editor-empty:first-child::before {
  color: #6b7280;
  content: attr(data-placeholder);
  float: left;
  height: 0;
  pointer-events: none;
}

/* Wiki-link styling inside the editor */
.note-editor-content .tiptap .wiki-link {
  color: rgb(var(--color-brand-400));
  text-decoration: underline;
  cursor: pointer;
}

.note-editor-content .tiptap h1 { font-size: 1.5rem; font-weight: 700; margin-top: 1.5rem; margin-bottom: 0.5rem; }
.note-editor-content .tiptap h2 { font-size: 1.25rem; font-weight: 600; margin-top: 1.25rem; margin-bottom: 0.5rem; }
.note-editor-content .tiptap h3 { font-size: 1.1rem; font-weight: 600; margin-top: 1rem; margin-bottom: 0.5rem; }

.note-editor-content .tiptap ul { list-style-type: disc; padding-left: 1.5rem; }
.note-editor-content .tiptap ol { list-style-type: decimal; padding-left: 1.5rem; }
.note-editor-content .tiptap blockquote {
  border-left: 3px solid #4b5563;
  padding-left: 1rem;
  margin: 0.5rem 0;
  color: #9ca3af;
}

.note-editor-content .tiptap pre {
  background: rgba(31, 41, 55, 0.8);
  border-radius: 0.375rem;
  padding: 0.75rem;
  font-family: ui-monospace, monospace;
  font-size: 0.85rem;
  overflow-x: auto;
}

.note-editor-content .tiptap code {
  background: rgba(31, 41, 55, 0.8);
  padding: 0.125rem 0.25rem;
  border-radius: 0.25rem;
  font-size: 0.85rem;
}

.note-editor-content .tiptap img {
  max-width: 100%;
  height: auto;
  border-radius: 0.375rem;
  margin: 0.5rem 0;
}

.note-editor-content .tiptap hr {
  border-color: #374151;
  margin: 1rem 0;
}

.note-editor-content .tiptap mark {
  border-radius: 0.125rem;
  padding: 0.0625rem 0.125rem;
}
</style>
