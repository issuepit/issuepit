import { defineStore } from 'pinia'
import type {
  Note,
  Notebook,
  NoteTag,
  NoteStatus,
  NoteGraphResponse,
  StorageProvider,
} from '~/types'

export const useNotesStore = defineStore('notes', () => {
  const notebooks = ref<Notebook[]>([])
  const notes = ref<Note[]>([])
  const currentNote = ref<Note | null>(null)
  const graphData = ref<NoteGraphResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useNotesApi()

  // ── Notebooks ──────────────────────────────────────────────────────────

  async function fetchNotebooks() {
    loading.value = true
    error.value = null
    try {
      notebooks.value = await api.get<Notebook[]>('/api/notes/notebooks')
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch notebooks'
    } finally {
      loading.value = false
    }
  }

  async function createNotebook(payload: {
    name: string
    description?: string
    projectId?: string
    storageProvider: StorageProvider
    gitRepoUrl?: string
    gitBranch?: string
  }) {
    const notebook = await api.post<Notebook>('/api/notes/notebooks', payload)
    notebooks.value.push(notebook)
    return notebook
  }

  async function updateNotebook(id: string, payload: {
    name: string
    description?: string
    projectId?: string
    storageProvider: StorageProvider
    gitRepoUrl?: string
    gitBranch?: string
  }) {
    const notebook = await api.put<Notebook>(`/api/notes/notebooks/${id}`, payload)
    const idx = notebooks.value.findIndex(n => n.id === id)
    if (idx !== -1) notebooks.value[idx] = notebook
    return notebook
  }

  async function deleteNotebook(id: string) {
    await api.del(`/api/notes/notebooks/${id}`)
    notebooks.value = notebooks.value.filter(n => n.id !== id)
  }

  // ── Tags ───────────────────────────────────────────────────────────────

  async function createTag(notebookId: string, name: string, color?: string) {
    const tag = await api.post<NoteTag>(`/api/notes/notebooks/${notebookId}/tags`, { name, color })
    const notebook = notebooks.value.find(n => n.id === notebookId)
    if (notebook) notebook.tags.push(tag)
    return tag
  }

  async function updateTag(notebookId: string, id: string, name: string, color?: string) {
    const tag = await api.put<NoteTag>(`/api/notes/notebooks/${notebookId}/tags/${id}`, { name, color })
    const notebook = notebooks.value.find(n => n.id === notebookId)
    if (notebook) {
      const idx = notebook.tags.findIndex(t => t.id === id)
      if (idx !== -1) notebook.tags[idx] = tag
    }
    return tag
  }

  async function deleteTag(notebookId: string, id: string) {
    await api.del(`/api/notes/notebooks/${notebookId}/tags/${id}`)
    const notebook = notebooks.value.find(n => n.id === notebookId)
    if (notebook) notebook.tags = notebook.tags.filter(t => t.id !== id)
  }

  // ── Notes ──────────────────────────────────────────────────────────────

  async function fetchNotes(params?: {
    notebookId?: string
    status?: string
    search?: string
    tagId?: string
  }) {
    loading.value = true
    error.value = null
    try {
      notes.value = await api.get<Note[]>('/api/notes', { params: params ?? {} })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch notes'
    } finally {
      loading.value = false
    }
  }

  async function fetchNote(id: string) {
    loading.value = true
    error.value = null
    try {
      currentNote.value = await api.get<Note>(`/api/notes/${id}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch note'
      currentNote.value = null
    } finally {
      loading.value = false
    }
  }

  async function createNote(payload: {
    notebookId: string
    title: string
    content?: string
    status: NoteStatus
    tagIds?: string[]
  }) {
    const note = await api.post<Note>('/api/notes', payload)
    notes.value.push(note)
    return note
  }

  async function updateNote(id: string, payload: {
    title: string
    content?: string
    status: NoteStatus
    tagIds?: string[]
    expectedVersion?: number
  }) {
    const note = await api.put<Note>(`/api/notes/${id}`, payload)
    const idx = notes.value.findIndex(n => n.id === id)
    if (idx !== -1) notes.value[idx] = note
    if (currentNote.value?.id === id) currentNote.value = note
    return note
  }

  async function deleteNote(id: string) {
    await api.del(`/api/notes/${id}`)
    notes.value = notes.value.filter(n => n.id !== id)
    if (currentNote.value?.id === id) currentNote.value = null
  }

  // ── Graph ──────────────────────────────────────────────────────────────

  async function fetchGraph(notebookId?: string) {
    loading.value = true
    error.value = null
    try {
      graphData.value = await api.get<NoteGraphResponse>('/api/notes/graph', {
        params: notebookId ? { notebookId } : {},
      })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch graph'
    } finally {
      loading.value = false
    }
  }

  return {
    notebooks,
    notes,
    currentNote,
    graphData,
    loading,
    error,
    fetchNotebooks,
    createNotebook,
    updateNotebook,
    deleteNotebook,
    createTag,
    updateTag,
    deleteTag,
    fetchNotes,
    fetchNote,
    createNote,
    updateNote,
    deleteNote,
    fetchGraph,
  }
})
