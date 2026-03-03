import { defineStore } from 'pinia'
import type { Todo, TodoBoard, TodoCategory, TodoPriority, TodoRecurringInterval } from '~/types'

export const useTodosStore = defineStore('todos', () => {
  const boards = ref<TodoBoard[]>([])
  const todos = ref<Todo[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  // ── Boards ──────────────────────────────────────────────────────────────

  async function fetchBoards() {
    loading.value = true
    error.value = null
    try {
      boards.value = await api.get<TodoBoard[]>('/api/todos/boards')
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch boards'
    } finally {
      loading.value = false
    }
  }

  async function createBoard(name: string, description?: string) {
    const board = await api.post<TodoBoard>('/api/todos/boards', { name, description })
    boards.value.push(board)
    return board
  }

  async function updateBoard(id: string, name: string, description?: string) {
    const board = await api.put<TodoBoard>(`/api/todos/boards/${id}`, { name, description })
    const idx = boards.value.findIndex(b => b.id === id)
    if (idx !== -1) boards.value[idx] = board
    return board
  }

  async function deleteBoard(id: string) {
    await api.delete(`/api/todos/boards/${id}`)
    boards.value = boards.value.filter(b => b.id !== id)
  }

  // ── Categories ──────────────────────────────────────────────────────────

  async function createCategory(boardId: string, name: string, position: number, color?: string) {
    const category = await api.post<TodoCategory>(`/api/todos/boards/${boardId}/categories`, {
      name,
      position,
      color
    })
    const board = boards.value.find(b => b.id === boardId)
    if (board) board.categories.push(category)
    return category
  }

  async function updateCategory(boardId: string, id: string, name: string, position: number, color?: string) {
    const category = await api.put<TodoCategory>(`/api/todos/boards/${boardId}/categories/${id}`, {
      name,
      position,
      color
    })
    const board = boards.value.find(b => b.id === boardId)
    if (board) {
      const idx = board.categories.findIndex(c => c.id === id)
      if (idx !== -1) board.categories[idx] = category
    }
    return category
  }

  async function deleteCategory(boardId: string, id: string) {
    await api.delete(`/api/todos/boards/${boardId}/categories/${id}`)
    const board = boards.value.find(b => b.id === boardId)
    if (board) board.categories = board.categories.filter(c => c.id !== id)
  }

  // ── Todos ──────────────────────────────────────────────────────────────

  async function fetchTodos(params?: {
    boardId?: string
    categoryId?: string
    completed?: boolean
    priority?: string
  }) {
    loading.value = true
    error.value = null
    try {
      todos.value = await api.get<Todo[]>('/api/todos', { params: params ?? {} })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch todos'
    } finally {
      loading.value = false
    }
  }

  async function createTodo(payload: {
    title: string
    body?: string
    priority: TodoPriority
    dueDate?: string
    startDate?: string
    recurringInterval: TodoRecurringInterval
    boardIds?: string[]
    categoryIds?: string[]
  }) {
    const todo = await api.post<Todo>('/api/todos', payload)
    todos.value.push(todo)
    return todo
  }

  async function updateTodo(id: string, payload: {
    title: string
    body?: string
    priority: TodoPriority
    dueDate?: string
    startDate?: string
    recurringInterval: TodoRecurringInterval
    isCompleted: boolean
    boardIds?: string[]
    categoryIds?: string[]
  }) {
    const todo = await api.put<Todo>(`/api/todos/${id}`, payload)
    const idx = todos.value.findIndex(t => t.id === id)
    if (idx !== -1) todos.value[idx] = todo
    return todo
  }

  async function toggleTodo(id: string) {
    const todo = todos.value.find(t => t.id === id)
    if (!todo) return
    return updateTodo(id, {
      title: todo.title,
      body: todo.body,
      priority: todo.priority,
      dueDate: todo.dueDate,
      startDate: todo.startDate,
      recurringInterval: todo.recurringInterval,
      isCompleted: !todo.isCompleted,
      boardIds: todo.boardMemberships.map(m => m.boardId),
      categoryIds: todo.categoryMemberships.map(m => m.categoryId)
    })
  }

  async function deleteTodo(id: string) {
    await api.delete(`/api/todos/${id}`)
    todos.value = todos.value.filter(t => t.id !== id)
  }

  return {
    boards,
    todos,
    loading,
    error,
    fetchBoards,
    createBoard,
    updateBoard,
    deleteBoard,
    createCategory,
    updateCategory,
    deleteCategory,
    fetchTodos,
    createTodo,
    updateTodo,
    toggleTodo,
    deleteTodo,
  }
})
