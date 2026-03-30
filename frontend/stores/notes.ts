import { defineStore } from 'pinia'
import type { NoteWorkspace, NoteListItem, NoteDetail, GraphData, NoteStorageEngine } from '~/types'

export const useNotesStore = defineStore('notes', () => {
  const workspaces = ref<NoteWorkspace[]>([])
  const notes = ref<NoteListItem[]>([])
  const currentNote = ref<NoteDetail | null>(null)
  const graphData = ref<GraphData | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useNotesApi()

  // ── Workspaces ────────────────────────────────────────────────────────

  async function fetchWorkspaces() {
    loading.value = true
    error.value = null
    try {
      workspaces.value = await api.get<NoteWorkspace[]>('/api/notes/workspaces')
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch workspaces'
    }
    finally {
      loading.value = false
    }
  }

  async function createWorkspace(payload: {
    name: string
    description?: string
    storageEngine?: NoteStorageEngine
    linkedProjectId?: string
    gitRepositoryUrl?: string
    gitBranch?: string
  }) {
    const workspace = await api.post<NoteWorkspace>('/api/notes/workspaces', payload)
    workspaces.value.push(workspace)
    return workspace
  }

  async function updateWorkspace(id: string, payload: {
    name?: string
    description?: string
    linkedProjectId?: string
    gitRepositoryUrl?: string
    gitBranch?: string
  }) {
    const workspace = await api.put<NoteWorkspace>(`/api/notes/workspaces/${id}`, payload)
    const idx = workspaces.value.findIndex(w => w.id === id)
    if (idx !== -1) workspaces.value[idx] = workspace
    return workspace
  }

  async function deleteWorkspace(id: string) {
    await api.del(`/api/notes/workspaces/${id}`)
    workspaces.value = workspaces.value.filter(w => w.id !== id)
  }

  // ── Notes ─────────────────────────────────────────────────────────────

  async function fetchNotes(workspaceId: string, search?: string) {
    loading.value = true
    error.value = null
    try {
      const params: Record<string, string> = {}
      if (search) params.search = search
      notes.value = await api.get<NoteListItem[]>(`/api/notes/workspace/${workspaceId}`, { params })
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch notes'
    }
    finally {
      loading.value = false
    }
  }

  async function fetchNote(id: string) {
    loading.value = true
    error.value = null
    try {
      currentNote.value = await api.get<NoteDetail>(`/api/notes/${id}`)
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch note'
    }
    finally {
      loading.value = false
    }
  }

  async function createNote(payload: {
    workspaceId: string
    title: string
    content?: string
  }) {
    const note = await api.post<NoteDetail>('/api/notes', payload)
    notes.value.unshift({
      id: note.id,
      workspaceId: note.workspaceId,
      title: note.title,
      version: note.version,
      createdAt: note.createdAt,
      updatedAt: note.updatedAt,
    })
    return note
  }

  async function updateNote(id: string, payload: {
    title?: string
    content?: string
    expectedVersion?: number
  }) {
    const note = await api.put<NoteDetail>(`/api/notes/${id}`, payload)
    currentNote.value = note
    const idx = notes.value.findIndex(n => n.id === id)
    if (idx !== -1) {
      notes.value[idx] = {
        id: note.id,
        workspaceId: note.workspaceId,
        title: note.title,
        version: note.version,
        createdAt: note.createdAt,
        updatedAt: note.updatedAt,
      }
    }
    return note
  }

  async function deleteNote(id: string) {
    await api.del(`/api/notes/${id}`)
    notes.value = notes.value.filter(n => n.id !== id)
    if (currentNote.value?.id === id) currentNote.value = null
  }

  // ── Graph ─────────────────────────────────────────────────────────────

  async function fetchGraphData(workspaceId: string) {
    loading.value = true
    error.value = null
    try {
      graphData.value = await api.get<GraphData>(`/api/notes/workspace/${workspaceId}/graph`)
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch graph data'
    }
    finally {
      loading.value = false
    }
  }

  return {
    workspaces,
    notes,
    currentNote,
    graphData,
    loading,
    error,
    fetchWorkspaces,
    createWorkspace,
    updateWorkspace,
    deleteWorkspace,
    fetchNotes,
    fetchNote,
    createNote,
    updateNote,
    deleteNote,
    fetchGraphData,
  }
})
