<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <div class="flex items-center gap-3">
        <h1 class="text-xl font-bold text-white">Todos</h1>
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full font-normal">
          {{ filteredTodos.length }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <!-- View toggle -->
        <div class="flex bg-gray-800 rounded-lg p-0.5 border border-gray-700">
          <button @click="view = 'board'"
            :class="['px-3 py-1 text-xs rounded-md transition-colors', view === 'board' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            Board
          </button>
          <button @click="view = 'calendar'"
            :class="['px-3 py-1 text-xs rounded-md transition-colors', view === 'calendar' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            Calendar
          </button>
          <button @click="view = 'list'"
            :class="['px-3 py-1 text-xs rounded-md transition-colors', view === 'list' ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200']">
            List
          </button>
        </div>

        <!-- Board selector -->
        <select v-if="store.boards.length" v-model="activeBoardId"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
          <option value="">All Boards</option>
          <option v-for="b in store.boards" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>

        <!-- Manage boards -->
        <button @click="showBoards = true"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
          Boards
        </button>

        <!-- New todo -->
        <button @click="openCreate"
          class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-3 py-1.5 rounded-lg transition-colors font-medium">
          + Todo
        </button>

        <!-- iCal export -->
        <a :href="icalUrl" download="todos.ics"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors"
          title="Export as iCal">
          iCal
        </a>
      </div>
    </div>

    <!-- Filters -->
    <div class="flex items-center gap-2 mb-4 shrink-0">
      <input v-model="search" type="text" placeholder="Search todos..."
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 w-48" />
      <select v-model="filterCompleted"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All</option>
        <option value="false">Incomplete</option>
        <option value="true">Completed</option>
      </select>
      <select v-model="filterPriority"
        class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
        <option value="">All Priorities</option>
        <option v-for="[k, v] in priorityOptions" :key="k" :value="k">{{ v }}</option>
      </select>
      <button v-if="hasFilters" @click="clearFilters"
        class="text-xs text-gray-400 hover:text-gray-200 px-2 py-1.5">Clear</button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center flex-1">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Board View -->
    <div v-if="!store.loading && view === 'board'" class="flex gap-4 overflow-x-auto flex-1 pb-4">
      <!-- Uncategorized column -->
      <div class="flex flex-col w-72 shrink-0">
        <div class="flex items-center justify-between mb-3">
          <div class="flex items-center gap-2">
            <span class="w-2.5 h-2.5 rounded-full bg-gray-500"></span>
            <h3 class="text-sm font-semibold text-gray-300">Uncategorized</h3>
            <span class="text-xs text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
              {{ uncategorizedTodos.length }}
            </span>
          </div>
          <button @click="openCreateWithCategory(null)"
            class="text-gray-600 hover:text-gray-400 transition-colors p-0.5 rounded">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60">
          <TodoCard v-for="todo in uncategorizedTodos" :key="todo.id" :todo="todo"
            @toggle="store.toggleTodo(todo.id)"
            @edit="openEdit(todo)"
            @delete="confirmDelete(todo.id)" />
          <div v-if="!uncategorizedTodos.length" class="py-4 text-center text-xs text-gray-600">
            No todos
          </div>
        </div>
      </div>

      <!-- Category columns -->
      <div v-for="cat in activeCategories" :key="cat.id" class="flex flex-col w-72 shrink-0">
        <div class="flex items-center justify-between mb-3">
          <div class="flex items-center gap-2">
            <span class="w-2.5 h-2.5 rounded-full" :style="{ background: cat.color }"></span>
            <h3 class="text-sm font-semibold text-gray-300">{{ cat.name }}</h3>
            <span class="text-xs text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
              {{ todosByCategory[cat.id]?.length ?? 0 }}
            </span>
          </div>
          <button @click="openCreateWithCategory(cat.id)"
            class="text-gray-600 hover:text-gray-400 transition-colors p-0.5 rounded">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60">
          <TodoCard v-for="todo in todosByCategory[cat.id] ?? []" :key="todo.id" :todo="todo"
            @toggle="store.toggleTodo(todo.id)"
            @edit="openEdit(todo)"
            @delete="confirmDelete(todo.id)" />
          <div v-if="!todosByCategory[cat.id]?.length" class="py-4 text-center text-xs text-gray-600">
            No todos
          </div>
        </div>
      </div>

      <!-- Add category column -->
      <div v-if="activeBoard" class="flex flex-col w-72 shrink-0">
        <button @click="showNewCategory = true"
          class="w-full py-3 rounded-xl border-2 border-dashed border-gray-800 text-gray-600 hover:border-gray-700 hover:text-gray-500 text-sm transition-colors">
          + Add Category
        </button>
      </div>
    </div>

    <!-- List View -->
    <div v-if="!store.loading && view === 'list'" class="flex-1 overflow-y-auto">
      <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div v-if="filteredTodos.length === 0" class="py-16 text-center">
          <p class="text-gray-400">No todos found</p>
          <button @click="openCreate" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
            Create your first todo →
          </button>
        </div>
        <div v-else>
          <TodoListItem v-for="todo in filteredTodos" :key="todo.id" :todo="todo"
            @toggle="store.toggleTodo(todo.id)"
            @edit="openEdit(todo)"
            @delete="confirmDelete(todo.id)" />
        </div>
      </div>
    </div>

    <!-- Calendar View -->
    <div v-if="!store.loading && view === 'calendar'" class="flex-1 overflow-auto">
      <TodoCalendar :todos="filteredTodos" @select="openEdit" @create-on-date="openCreateOnDate" />
    </div>

    <!-- ── Modals ───────────────────────────────────────── -->

    <!-- Boards Manager Modal -->
    <div v-if="showBoards" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4"
      @click.self="showBoards = false">
      <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-md p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-bold text-white">Todo Boards</h2>
          <button @click="showBoards = false" class="text-gray-500 hover:text-gray-300">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="space-y-2 mb-4 max-h-64 overflow-y-auto">
          <div v-for="b in store.boards" :key="b.id"
            class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
            <span class="text-sm text-gray-200">{{ b.name }}</span>
            <div class="flex gap-2">
              <button @click="editBoard(b)" class="text-xs text-gray-400 hover:text-gray-200">Edit</button>
              <button @click="deleteBoard(b.id)" class="text-xs text-red-400 hover:text-red-300">Delete</button>
            </div>
          </div>
          <div v-if="!store.boards.length" class="text-center text-sm text-gray-500 py-4">No boards yet</div>
        </div>
        <div class="border-t border-gray-800 pt-4">
          <h3 class="text-xs font-medium text-gray-400 mb-2">New Board</h3>
          <input v-model="newBoardName" type="text" placeholder="Board name (e.g. Work, School, Personal)"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 mb-2" />
          <input v-model="newBoardDescription" type="text" placeholder="Description (optional)"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 mb-2" />
          <button @click="createBoard" :disabled="!newBoardName.trim()"
            class="w-full bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm px-3 py-2 rounded-lg transition-colors">
            Create Board
          </button>
        </div>
      </div>
    </div>

    <!-- New Category Modal -->
    <div v-if="showNewCategory" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4"
      @click.self="showNewCategory = false">
      <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-sm p-6">
        <h2 class="text-lg font-bold text-white mb-4">Add Category</h2>
        <input v-model="newCategoryName" type="text" placeholder="Category name"
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 mb-3" />
        <div class="flex items-center gap-2 mb-4">
          <label class="text-xs text-gray-400">Color</label>
          <input v-model="newCategoryColor" type="color"
            class="w-8 h-8 rounded cursor-pointer border border-gray-700 bg-gray-800" />
        </div>
        <div class="flex gap-2">
          <button @click="showNewCategory = false" class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm px-3 py-2 rounded-lg transition-colors">
            Cancel
          </button>
          <button @click="createCategory" :disabled="!newCategoryName.trim()"
            class="flex-1 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm px-3 py-2 rounded-lg transition-colors">
            Create
          </button>
        </div>
      </div>
    </div>

    <!-- Create/Edit Todo Modal -->
    <div v-if="showTodoForm" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4"
      @click.self="closeTodoForm">
      <div class="bg-gray-900 border border-gray-800 rounded-xl w-full max-w-lg p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-bold text-white">{{ editingTodo ? 'Edit Todo' : 'New Todo' }}</h2>
          <button @click="closeTodoForm" class="text-gray-500 hover:text-gray-300">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div class="space-y-3">
          <input v-model="form.title" type="text" placeholder="Todo title"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
          <textarea v-model="form.body" placeholder="Description (optional)"
            rows="3"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none" />
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-xs text-gray-400 mb-1 block">Priority</label>
              <select v-model="form.priority"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option v-for="[k, v] in priorityOptions" :key="k" :value="k">{{ v }}</option>
              </select>
            </div>
            <div>
              <label class="text-xs text-gray-400 mb-1 block">Repeat</label>
              <select v-model="form.recurringInterval"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option v-for="[k, v] in recurringOptions" :key="k" :value="k">{{ v }}</option>
              </select>
            </div>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-xs text-gray-400 mb-1 block">Start Date</label>
              <input v-model="form.startDate" type="datetime-local"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
            <div>
              <label class="text-xs text-gray-400 mb-1 block">Due Date</label>
              <input v-model="form.dueDate" type="datetime-local"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            </div>
          </div>
          <!-- Board / Category selection -->
          <div v-if="store.boards.length">
            <label class="text-xs text-gray-400 mb-1 block">Boards</label>
            <div class="flex flex-wrap gap-2">
              <label v-for="b in store.boards" :key="b.id" class="flex items-center gap-1.5 cursor-pointer">
                <input type="checkbox" :value="b.id" v-model="form.boardIds"
                  class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
                <span class="text-xs text-gray-300">{{ b.name }}</span>
              </label>
            </div>
          </div>
          <div v-if="availableCategories.length">
            <label class="text-xs text-gray-400 mb-1 block">Categories</label>
            <div class="flex flex-wrap gap-2">
              <label v-for="cat in availableCategories" :key="cat.id" class="flex items-center gap-1.5 cursor-pointer">
                <input type="checkbox" :value="cat.id" v-model="form.categoryIds"
                  class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
                <span class="w-2 h-2 rounded-full" :style="{ background: cat.color }"></span>
                <span class="text-xs text-gray-300">{{ cat.name }}</span>
              </label>
            </div>
          </div>
        </div>
        <div class="flex gap-2 mt-5">
          <button @click="closeTodoForm" class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm px-3 py-2 rounded-lg transition-colors">
            Cancel
          </button>
          <button @click="saveTodo" :disabled="!form.title.trim() || saving"
            class="flex-1 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm px-3 py-2 rounded-lg transition-colors">
            {{ saving ? 'Saving...' : (editingTodo ? 'Update' : 'Create') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useTodosStore } from '~/stores/todos'
import type { Todo, TodoCategory } from '~/types'
import { TodoPriority, TodoPriorityLabels, TodoRecurringInterval, TodoRecurringIntervalLabels } from '~/types'

const store = useTodosStore()

// ── View state ────────────────────────────────────────────────────────────
const view = ref<'board' | 'list' | 'calendar'>('board')
const activeBoardId = ref<string>('')

// ── Filters ───────────────────────────────────────────────────────────────
const search = ref('')
const filterCompleted = ref('')
const filterPriority = ref('')

const hasFilters = computed(() => search.value || filterCompleted.value || filterPriority.value)

function clearFilters() {
  search.value = ''
  filterCompleted.value = ''
  filterPriority.value = ''
}

// ── Option lists ──────────────────────────────────────────────────────────
const priorityOptions = Object.entries(TodoPriorityLabels) as [TodoPriority, string][]
const recurringOptions = Object.entries(TodoRecurringIntervalLabels) as [TodoRecurringInterval, string][]

// ── Derived data ──────────────────────────────────────────────────────────
const activeBoard = computed(() => store.boards.find(b => b.id === activeBoardId.value) ?? null)

const activeCategories = computed((): TodoCategory[] => {
  if (!activeBoard.value) {
    // Merge categories from all boards
    const all: TodoCategory[] = []
    const seen = new Set<string>()
    for (const b of store.boards) {
      for (const c of b.categories) {
        if (!seen.has(c.id)) {
          seen.add(c.id)
          all.push(c)
        }
      }
    }
    return all.sort((a, b) => a.position - b.position)
  }
  return [...activeBoard.value.categories].sort((a, b) => a.position - b.position)
})

const availableCategories = computed((): TodoCategory[] => {
  const cats: TodoCategory[] = []
  const seen = new Set<string>()
  const selectedBoards = form.boardIds.length > 0
    ? store.boards.filter(b => form.boardIds.includes(b.id))
    : store.boards
  for (const b of selectedBoards) {
    for (const c of b.categories) {
      if (!seen.has(c.id)) {
        seen.add(c.id)
        cats.push(c)
      }
    }
  }
  return cats.sort((a, b) => a.position - b.position)
})

const filteredTodos = computed(() => {
  let result = store.todos
  if (activeBoardId.value)
    result = result.filter(t => t.boardMemberships.some(m => m.boardId === activeBoardId.value))
  if (filterCompleted.value !== '')
    result = result.filter(t => t.isCompleted === (filterCompleted.value === 'true'))
  if (filterPriority.value)
    result = result.filter(t => t.priority === filterPriority.value)
  if (search.value) {
    const q = search.value.toLowerCase()
    result = result.filter(t => t.title.toLowerCase().includes(q))
  }
  return result
})

const todosByCategory = computed(() => {
  const grouped: Record<string, Todo[]> = {}
  for (const cat of activeCategories.value) grouped[cat.id] = []
  for (const todo of filteredTodos.value) {
    for (const m of todo.categoryMemberships) {
      if (grouped[m.categoryId]) grouped[m.categoryId].push(todo)
    }
  }
  return grouped
})

const uncategorizedTodos = computed(() =>
  filteredTodos.value.filter(t => t.categoryMemberships.length === 0)
)

// ── iCal URL ──────────────────────────────────────────────────────────────
const icalUrl = computed(() => {
  const params = new URLSearchParams()
  if (activeBoardId.value) params.set('boardId', activeBoardId.value)
  if (filterCompleted.value !== '') params.set('completed', filterCompleted.value)
  const qs = params.toString()
  return `/api/todos/export.ics${qs ? '?' + qs : ''}`
})

// ── Boards modal ──────────────────────────────────────────────────────────
const showBoards = ref(false)
const newBoardName = ref('')
const newBoardDescription = ref('')
const editingBoardId = ref<string | null>(null)

async function createBoard() {
  if (!newBoardName.value.trim()) return
  if (editingBoardId.value) {
    await store.updateBoard(editingBoardId.value, newBoardName.value.trim(), newBoardDescription.value.trim() || undefined)
    editingBoardId.value = null
  } else {
    await store.createBoard(newBoardName.value.trim(), newBoardDescription.value.trim() || undefined)
  }
  newBoardName.value = ''
  newBoardDescription.value = ''
}

function editBoard(b: { id: string; name: string; description?: string }) {
  editingBoardId.value = b.id
  newBoardName.value = b.name
  newBoardDescription.value = b.description ?? ''
}

async function deleteBoard(id: string) {
  if (!confirm('Delete this board?')) return
  await store.deleteBoard(id)
  if (activeBoardId.value === id) activeBoardId.value = ''
}

// ── Category modal ────────────────────────────────────────────────────────
const showNewCategory = ref(false)
const newCategoryName = ref('')
const newCategoryColor = ref('#6b7280')

async function createCategory() {
  if (!activeBoard.value || !newCategoryName.value.trim()) return
  const position = activeBoard.value.categories.length
  await store.createCategory(activeBoard.value.id, newCategoryName.value.trim(), position, newCategoryColor.value)
  newCategoryName.value = ''
  newCategoryColor.value = '#6b7280'
  showNewCategory.value = false
}

// ── Todo modal ────────────────────────────────────────────────────────────
const showTodoForm = ref(false)
const saving = ref(false)
const editingTodo = ref<Todo | null>(null)

const defaultForm = () => ({
  title: '',
  body: '',
  priority: TodoPriority.NoPriority,
  dueDate: '',
  startDate: '',
  recurringInterval: TodoRecurringInterval.None,
  boardIds: activeBoardId.value ? [activeBoardId.value] : [] as string[],
  categoryIds: [] as string[],
})

const form = reactive(defaultForm())

function openCreate() {
  Object.assign(form, defaultForm())
  editingTodo.value = null
  showTodoForm.value = true
}

function openCreateWithCategory(categoryId: string | null) {
  Object.assign(form, defaultForm())
  if (categoryId) form.categoryIds = [categoryId]
  editingTodo.value = null
  showTodoForm.value = true
}

function openCreateOnDate(date: Date) {
  Object.assign(form, defaultForm())
  form.dueDate = formatLocalDatetime(date)
  editingTodo.value = null
  showTodoForm.value = true
}

function openEdit(todo: Todo) {
  editingTodo.value = todo
  form.title = todo.title
  form.body = todo.body ?? ''
  form.priority = todo.priority
  form.dueDate = todo.dueDate ? formatLocalDatetime(new Date(todo.dueDate)) : ''
  form.startDate = todo.startDate ? formatLocalDatetime(new Date(todo.startDate)) : ''
  form.recurringInterval = todo.recurringInterval
  form.boardIds = todo.boardMemberships.map(m => m.boardId)
  form.categoryIds = todo.categoryMemberships.map(m => m.categoryId)
  showTodoForm.value = true
}

function closeTodoForm() {
  showTodoForm.value = false
  editingTodo.value = null
}

async function saveTodo() {
  if (!form.title.trim()) return
  saving.value = true
  try {
    const payload = {
      title: form.title.trim(),
      body: form.body.trim() || undefined,
      priority: form.priority,
      dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : undefined,
      startDate: form.startDate ? new Date(form.startDate).toISOString() : undefined,
      recurringInterval: form.recurringInterval,
      isCompleted: editingTodo.value?.isCompleted ?? false,
      boardIds: form.boardIds,
      categoryIds: form.categoryIds,
    }
    if (editingTodo.value) {
      await store.updateTodo(editingTodo.value.id, payload)
    } else {
      await store.createTodo(payload)
    }
    closeTodoForm()
  } finally {
    saving.value = false
  }
}

async function confirmDelete(id: string) {
  if (!confirm('Delete this todo?')) return
  await store.deleteTodo(id)
}

// ── Helpers ───────────────────────────────────────────────────────────────
function formatLocalDatetime(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

// ── Init ──────────────────────────────────────────────────────────────────
onMounted(async () => {
  await Promise.all([store.fetchBoards(), store.fetchTodos()])
})
</script>
