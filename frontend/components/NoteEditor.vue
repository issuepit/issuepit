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
import TextStyle from '@tiptap/extension-text-style'
import Color from '@tiptap/extension-color'
import Highlight from '@tiptap/extension-highlight'
import Placeholder from '@tiptap/extension-placeholder'
import Image from '@tiptap/extension-image'
import type { Note } from '~/types'

const props = defineProps<{
  modelValue: string
  notebookId?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const store = useNotesStore()
const router = useRouter()

// Image upload state
const uploading = ref(false)

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

async function handleImageUpload(file: File): Promise<string | null> {
  uploading.value = true
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
  },
})

// Watch for external content changes
watch(() => props.modelValue, (newVal) => {
  if (editor.value && editor.value.getHTML() !== newVal) {
    editor.value.commands.setContent(newVal, false)
  }
})

// Wiki-link detection: check if cursor is inside [[ ... ]]
function checkForWikiLink(e: ReturnType<typeof useEditor>['value']) {
  if (!e) return
  const { from } = e.state.selection
  const textBefore = e.state.doc.textBetween(Math.max(0, from - 50), from, '')

  const match = textBefore.match(/\[\[([^\]]{0,50})$/)
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
