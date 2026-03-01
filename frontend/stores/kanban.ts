import { defineStore } from 'pinia'
import type { KanbanBoard, KanbanColumn, KanbanTransition, IssueStatus } from '~/types'

export const useKanbanStore = defineStore('kanban', () => {
  const boards = ref<KanbanBoard[]>([])
  const currentBoard = ref<KanbanBoard | null>(null)
  const transitions = ref<KanbanTransition[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  // ── Boards ────────────────────────────────────────────────────────────────

  async function fetchBoards(projectId: string) {
    loading.value = true
    error.value = null
    try {
      boards.value = await api.get<KanbanBoard[]>('/api/kanban/boards', { params: { projectId } })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch boards'
    } finally {
      loading.value = false
    }
  }

  async function createBoard(projectId: string, name: string) {
    loading.value = true
    error.value = null
    try {
      const board = await api.post<KanbanBoard>('/api/kanban/boards', { projectId, name })
      boards.value.push(board)
      return board
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create board'
    } finally {
      loading.value = false
    }
  }

  async function updateBoard(boardId: string, name: string) {
    loading.value = true
    error.value = null
    try {
      const board = await api.put<KanbanBoard>(`/api/kanban/boards/${boardId}`, { name })
      const idx = boards.value.findIndex(b => b.id === boardId)
      if (idx !== -1) boards.value[idx] = board
      if (currentBoard.value?.id === boardId) currentBoard.value = board
      return board
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update board'
    } finally {
      loading.value = false
    }
  }

  async function deleteBoard(boardId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/kanban/boards/${boardId}`)
      boards.value = boards.value.filter(b => b.id !== boardId)
      if (currentBoard.value?.id === boardId) currentBoard.value = null
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete board'
    } finally {
      loading.value = false
    }
  }

  function selectBoard(board: KanbanBoard) {
    currentBoard.value = board
  }

  // ── Columns ───────────────────────────────────────────────────────────────

  async function addColumn(boardId: string, name: string, position: number, issueStatus: IssueStatus) {
    loading.value = true
    error.value = null
    try {
      const col = await api.post<KanbanColumn>(`/api/kanban/boards/${boardId}/columns`, { name, position, issueStatus })
      const board = boards.value.find(b => b.id === boardId)
      if (board) {
        board.columns.push(col)
      } else if (currentBoard.value?.id === boardId) {
        currentBoard.value.columns.push(col)
      }
      return col
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to add column'
    } finally {
      loading.value = false
    }
  }

  async function updateColumn(boardId: string, columnId: string, name: string, position: number, issueStatus: IssueStatus) {
    loading.value = true
    error.value = null
    try {
      const col = await api.put<KanbanColumn>(`/api/kanban/boards/${boardId}/columns/${columnId}`, { name, position, issueStatus })
      const replaceInBoard = (b: KanbanBoard) => {
        const idx = b.columns.findIndex(c => c.id === columnId)
        if (idx !== -1) b.columns[idx] = col
      }
      boards.value.forEach(replaceInBoard)
      if (currentBoard.value) replaceInBoard(currentBoard.value)
      return col
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update column'
    } finally {
      loading.value = false
    }
  }

  async function deleteColumn(boardId: string, columnId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/kanban/boards/${boardId}/columns/${columnId}`)
      const removeFromBoard = (b: KanbanBoard) => {
        b.columns = b.columns.filter(c => c.id !== columnId)
      }
      boards.value.forEach(removeFromBoard)
      if (currentBoard.value) removeFromBoard(currentBoard.value)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete column'
    } finally {
      loading.value = false
    }
  }

  async function reorderColumns(boardId: string, columnIds: string[]) {
    loading.value = true
    error.value = null
    try {
      await api.post(`/api/kanban/boards/${boardId}/columns/reorder`, { columnIds })
      // Update positions locally to keep store in sync
      const updatePositions = (b: KanbanBoard) => {
        columnIds.forEach((id, idx) => {
          const col = b.columns.find(c => c.id === id)
          if (col) col.position = idx
        })
      }
      boards.value.forEach(updatePositions)
      if (currentBoard.value) updatePositions(currentBoard.value)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to reorder columns'
    } finally {
      loading.value = false
    }
  }

  // ── Transitions ───────────────────────────────────────────────────────────

  async function fetchTransitions(boardId: string) {
    loading.value = true
    error.value = null
    try {
      transitions.value = await api.get<KanbanTransition[]>(`/api/kanban/boards/${boardId}/transitions`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch transitions'
    } finally {
      loading.value = false
    }
  }

  async function createTransition(boardId: string, payload: Omit<KanbanTransition, 'id' | 'boardId' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const t = await api.post<KanbanTransition>(`/api/kanban/boards/${boardId}/transitions`, { ...payload, boardId })
      transitions.value.push(t)
      return t
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create transition'
    } finally {
      loading.value = false
    }
  }

  async function updateTransition(boardId: string, transitionId: string, payload: Omit<KanbanTransition, 'id' | 'boardId' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const t = await api.put<KanbanTransition>(`/api/kanban/boards/${boardId}/transitions/${transitionId}`, payload)
      const idx = transitions.value.findIndex(x => x.id === transitionId)
      if (idx !== -1) transitions.value[idx] = t
      return t
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update transition'
    } finally {
      loading.value = false
    }
  }

  async function deleteTransition(boardId: string, transitionId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/kanban/boards/${boardId}/transitions/${transitionId}`)
      transitions.value = transitions.value.filter(t => t.id !== transitionId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete transition'
    } finally {
      loading.value = false
    }
  }

  async function triggerTransition(boardId: string, transitionId: string, issueId: string) {
    loading.value = true
    error.value = null
    try {
      return await api.post(`/api/kanban/boards/${boardId}/transitions/${transitionId}/trigger`, { issueId })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to trigger transition'
    } finally {
      loading.value = false
    }
  }

  return {
    boards,
    currentBoard,
    transitions,
    loading,
    error,
    fetchBoards,
    createBoard,
    updateBoard,
    deleteBoard,
    selectBoard,
    addColumn,
    updateColumn,
    deleteColumn,
    reorderColumns,
    fetchTransitions,
    createTransition,
    updateTransition,
    deleteTransition,
    triggerTransition,
  }
})
