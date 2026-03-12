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
          title="Download as iCal file">
          iCal ↓
        </a>
        <button @click="copyIcalUrl"
          class="text-xs bg-gray-800 hover:bg-gray-700 border border-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors"
          :title="icalCopied ? 'Copied!' : 'Copy calendar subscription URL'">
          {{ icalCopied ? '✓ Copied' : '📅 Subscribe' }}
        </button>
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
      <div class="flex flex-col w-72 shrink-0 transition-opacity duration-150"
        :class="{
          'opacity-40': boardDraggingTodoId && boardHoverCategoryId && boardHoverCategoryId !== '__uncategorized__',
        }">
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
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60 transition-colors"
          :class="{
            'border-brand-500/60 bg-brand-900/10': boardHoverCategoryId === '__uncategorized__' && boardDraggingTodoId,
            'border-gray-700/30 bg-gray-900/20': boardDraggingTodoId && boardHoverCategoryId !== '__uncategorized__',
          }"
          @dragover="onBoardColumnDragOver($event, null)"
          @dragleave="onBoardColumnDragLeave"
          @drop="onBoardColumnDrop($event, null)">
          <template v-for="(todo, idx) in uncategorizedTodos" :key="todo.id">
            <!-- Placeholder before this item -->
            <div v-if="boardDraggingTodoId && boardHoverCategoryId === '__uncategorized__' && boardHoverInsertIdx === idx"
              role="status" aria-label="Drop zone"
              class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
            </div>
            <div :class="todo.id === boardDraggingTodoId ? 'invisible' : ''"
              draggable="true"
              @dragstart="onBoardDragStart($event, todo.id)"
              @dragend="onBoardDragEnd">
              <TodoCard :todo="todo"
                @toggle="store.toggleTodo(todo.id)"
                @edit="openEdit(todo)"
                @delete="confirmDelete(todo.id)" />
            </div>
          </template>
          <!-- Drop zone placeholder at end -->
          <div v-if="boardDraggingTodoId && boardHoverCategoryId === '__uncategorized__' && boardHoverInsertIdx >= uncategorizedTodos.length"
            role="status" aria-label="Drop zone"
            class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
          </div>
          <div v-if="!uncategorizedTodos.length && !(boardDraggingTodoId && boardHoverCategoryId === '__uncategorized__')" class="py-4 text-center text-xs text-gray-600">
            No todos
          </div>
        </div>
      </div>

      <!-- Category columns -->
      <div v-for="cat in activeCategories" :key="cat.id"
        class="flex flex-col w-72 shrink-0 transition-opacity duration-150"
        :class="{
          'opacity-40': boardDraggingTodoId && boardHoverCategoryId && boardHoverCategoryId !== cat.id,
        }">
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
        <div class="flex-1 space-y-2 bg-gray-900/40 rounded-xl p-2 min-h-32 border border-gray-800/60 transition-colors"
          :class="{
            'border-brand-500/60 bg-brand-900/10': boardHoverCategoryId === cat.id && boardDraggingTodoId,
            'border-gray-700/30 bg-gray-900/20': boardDraggingTodoId && boardHoverCategoryId !== cat.id,
          }"
          @dragover="onBoardColumnDragOver($event, cat.id)"
          @dragleave="onBoardColumnDragLeave"
          @drop="onBoardColumnDrop($event, cat.id)">
          <template v-for="(todo, idx) in todosByCategory[cat.id] ?? []" :key="todo.id">
            <!-- Placeholder before this item -->
            <div v-if="boardDraggingTodoId && boardHoverCategoryId === cat.id && boardHoverInsertIdx === idx"
              role="status" aria-label="Drop zone"
              class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
            </div>
            <div :class="todo.id === boardDraggingTodoId ? 'invisible' : ''"
              draggable="true"
              @dragstart="onBoardDragStart($event, todo.id)"
              @dragend="onBoardDragEnd">
              <TodoCard :todo="todo"
                @toggle="store.toggleTodo(todo.id)"
                @edit="openEdit(todo)"
                @delete="confirmDelete(todo.id)" />
            </div>
          </template>
          <!-- Drop zone placeholder at end -->
          <div v-if="boardDraggingTodoId && boardHoverCategoryId === cat.id && boardHoverInsertIdx >= (todosByCategory[cat.id]?.length ?? 0)"
            role="status" aria-label="Drop zone"
            class="rounded-lg border-2 border-dashed border-brand-500/50 bg-brand-900/10 h-14 animate-pulse">
          </div>
          <div v-if="!todosByCategory[cat.id]?.length && !(boardDraggingTodoId && boardHoverCategoryId === cat.id)" class="py-4 text-center text-xs text-gray-600">
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
      <TodoCalendar :todos="filteredTodos" @select="openEdit" @create-on-date="openCreateOnDate" @reschedule="onReschedule" @resize="onResize" />
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
              <div class="flex gap-1">
                <input v-model="form.startDate" type="text" placeholder="YYYY-MM-DD" pattern="\d{4}-\d{2}-\d{2}"
                  class="flex-1 min-w-0 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
                <input v-model="form.startTime" type="text" placeholder="HH:MM" pattern="\d{2}:\d{2}"
                  class="w-20 bg-gray-800 border border-gray-700 rounded-lg px-2 py-2 text-sm text-gray-300 placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              </div>
            </div>
            <div>
              <label class="text-xs text-gray-400 mb-1 block">Due Date</label>
              <div class="flex gap-1">
                <input v-model="form.dueDate" type="text" placeholder="YYYY-MM-DD" pattern="\d{4}-\d{2}-\d{2}"
                  class="flex-1 min-w-0 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
                <input v-model="form.dueTime" type="text" placeholder="HH:MM" pattern="\d{2}:\d{2}"
                  class="w-20 bg-gray-800 border border-gray-700 rounded-lg px-2 py-2 text-sm text-gray-300 placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              </div>
            </div>
          </div>
          <div>
            <label class="text-xs text-gray-400 mb-1 block">Duration</label>
            <div class="flex items-center gap-2">
              <input v-model.number="durationHours" type="number" min="0" placeholder="0"
                @change="applyDuration"
                class="w-20 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <span class="text-xs text-gray-500">h</span>
              <input v-model.number="durationMinutes" type="number" min="0" max="59" placeholder="0"
                @change="applyDuration"
                class="w-20 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <span class="text-xs text-gray-500">min</span>
              <span v-if="durationHours > 0 || durationMinutes > 0" class="text-xs text-gray-600 ml-1">
                ({{ [durationHours > 0 ? durationHours + 'h' : '', durationMinutes > 0 ? durationMinutes + 'm' : ''].filter(Boolean).join(' ') }})
              </span>
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
const route = useRoute()
const router = useRouter()

const VALID_VIEWS = ['board', 'list', 'calendar'] as const
type ViewMode = typeof VALID_VIEWS[number]

const view = ref<ViewMode>(
  VALID_VIEWS.includes(route.query.view as ViewMode)
    ? (route.query.view as ViewMode)
    : 'board',
)

watch(view, (newView) => {
  if (route.query.view !== newView) {
    router.push({ query: { ...route.query, view: newView } })
  }
})

watch(() => route.query.view, (newView) => {
  if (newView && VALID_VIEWS.includes(newView as ViewMode) && newView !== view.value) {
    view.value = newView as ViewMode
  }
})

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

const calendarFeedUrl = computed(() => {
  const params = new URLSearchParams()
  if (activeBoardId.value) params.set('boardId', activeBoardId.value)
  if (filterCompleted.value !== '') params.set('completed', filterCompleted.value)
  const qs = params.toString()
  const path = `/api/todos/calendar.ics${qs ? '?' + qs : ''}`
  if (import.meta.client) return `${window.location.origin}${path}`
  return path
})

const icalCopied = ref(false)

async function copyIcalUrl() {
  try {
    await navigator.clipboard.writeText(calendarFeedUrl.value)
    icalCopied.value = true
    setTimeout(() => { icalCopied.value = false }, 2000)
  } catch {
    // fallback: show the URL in a prompt
    window.prompt('Copy this calendar subscription URL:', calendarFeedUrl.value)
  }
}

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
  startDate: '',  // YYYY-MM-DD
  startTime: '',  // HH:MM
  dueDate: '',    // YYYY-MM-DD
  dueTime: '',    // HH:MM
  recurringInterval: TodoRecurringInterval.None,
  boardIds: activeBoardId.value ? [activeBoardId.value] : [] as string[],
  categoryIds: [] as string[],
})

const form = reactive(defaultForm())

// Split an ISO datetime string into local date (YYYY-MM-DD) and time (HH:MM) parts.
function splitDatetime(isoStr: string | null | undefined): { date: string; time: string } {
  if (!isoStr) return { date: '', time: '' }
  const d = new Date(isoStr)
  if (isNaN(d.getTime())) return { date: '', time: '' }
  const pad = (n: number) => String(n).padStart(2, '0')
  return {
    date: `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`,
    time: `${pad(d.getHours())}:${pad(d.getMinutes())}`,
  }
}

// Combine local date + time parts to an ISO string for the API.
function combineDatetime(date: string, time: string): string | undefined {
  if (!date) return undefined
  const t = time || '00:00'
  const d = new Date(`${date}T${t}`)
  return isNaN(d.getTime()) ? undefined : d.toISOString()
}

// ── Duration helpers ──────────────────────────────────────────────────────
const durationHours = ref(0)
const durationMinutes = ref(0)
// Guard against circular update: when applyDuration sets form.dueDate/dueTime,
// the watcher below should not overwrite the duration values that the user just typed.
let _applyingDuration = false

watch([() => form.startDate, () => form.startTime, () => form.dueDate, () => form.dueTime], () => {
  if (_applyingDuration) return
  const startStr = form.startDate ? `${form.startDate}T${form.startTime || '00:00'}` : ''
  const dueStr = form.dueDate ? `${form.dueDate}T${form.dueTime || '00:00'}` : ''
  const diffMs = (startStr && dueStr) ? new Date(dueStr).getTime() - new Date(startStr).getTime() : 0
  if (diffMs > 0) {
    const totalMins = Math.round(diffMs / 60000)
    durationHours.value = Math.floor(totalMins / 60)
    durationMinutes.value = totalMins % 60
  } else {
    durationHours.value = 0
    durationMinutes.value = 0
  }
})

function applyDuration() {
  const startStr = form.startDate ? `${form.startDate}T${form.startTime || '00:00'}` : ''
  if (!startStr) return
  const totalMins = (durationHours.value || 0) * 60 + (durationMinutes.value || 0)
  if (totalMins <= 0) return
  const dueMs = new Date(startStr).getTime() + totalMins * 60000
  const due = new Date(dueMs)
  const pad = (n: number) => String(n).padStart(2, '0')
  _applyingDuration = true
  form.dueDate = `${due.getFullYear()}-${pad(due.getMonth() + 1)}-${pad(due.getDate())}`
  form.dueTime = `${pad(due.getHours())}:${pad(due.getMinutes())}`
  nextTick(() => { _applyingDuration = false })
}

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
  const { date: d, time: t } = splitDatetime(date.toISOString())
  form.dueDate = d
  form.dueTime = t
  editingTodo.value = null
  showTodoForm.value = true
}

function openEdit(todo: Todo) {
  editingTodo.value = todo
  form.title = todo.title
  form.body = todo.body ?? ''
  form.priority = todo.priority
  const start = splitDatetime(todo.startDate)
  const due = splitDatetime(todo.dueDate)
  form.startDate = start.date
  form.startTime = start.time
  form.dueDate = due.date
  form.dueTime = due.time
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
      dueDate: combineDatetime(form.dueDate, form.dueTime),
      startDate: combineDatetime(form.startDate, form.startTime),
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

async function onReschedule(todo: Todo, newDate: Date) {
  await store.updateTodo(todo.id, {
    title: todo.title,
    body: todo.body,
    priority: todo.priority,
    dueDate: newDate.toISOString(),
    startDate: todo.startDate,
    recurringInterval: todo.recurringInterval,
    isCompleted: todo.isCompleted,
    boardIds: todo.boardMemberships.map(m => m.boardId),
    categoryIds: todo.categoryMemberships.map(m => m.categoryId),
  })
}

async function onResize(todo: Todo, newStartDate: Date, newDueDate: Date) {
  await store.updateTodo(todo.id, {
    title: todo.title,
    body: todo.body,
    priority: todo.priority,
    dueDate: newDueDate.toISOString(),
    startDate: newStartDate.toISOString(),
    recurringInterval: todo.recurringInterval,
    isCompleted: todo.isCompleted,
    boardIds: todo.boardMemberships.map(m => m.boardId),
    categoryIds: todo.categoryMemberships.map(m => m.categoryId),
  })
}

// ── Board drag & drop ─────────────────────────────────────────────────────
const boardDraggingTodoId = ref<string | null>(null)
const boardHoverCategoryId = ref<string | null>(null)
const boardHoverInsertIdx = ref<number>(0)

function onBoardDragStart(event: DragEvent, todoId: string) {
  boardDraggingTodoId.value = todoId
  event.dataTransfer!.effectAllowed = 'move'
}

function onBoardDragEnd() {
  boardDraggingTodoId.value = null
  boardHoverCategoryId.value = null
  boardHoverInsertIdx.value = 0
}

function onBoardColumnDragOver(event: DragEvent, categoryId: string | null) {
  if (!boardDraggingTodoId.value) return
  event.preventDefault()
  boardHoverCategoryId.value = categoryId ?? '__uncategorized__'
  // Calculate insertion index from mouse position
  const container = event.currentTarget as HTMLElement
  const items = Array.from(container.querySelectorAll<HTMLElement>('[draggable="true"]'))
  let insertIdx = items.length
  for (let i = 0; i < items.length; i++) {
    const rect = items[i].getBoundingClientRect()
    if (event.clientY < rect.top + rect.height / 2) {
      insertIdx = i
      break
    }
  }
  boardHoverInsertIdx.value = insertIdx
}

function onBoardColumnDragLeave(event: DragEvent) {
  const relatedTarget = event.relatedTarget as Node | null
  if (relatedTarget && (event.currentTarget as Node)?.contains(relatedTarget)) return
  boardHoverCategoryId.value = null
}

async function onBoardColumnDrop(event: DragEvent, categoryId: string | null) {
  event.preventDefault()
  if (!boardDraggingTodoId.value) return
  const todo = store.todos.find(t => t.id === boardDraggingTodoId.value)
  if (!todo) {
    boardDraggingTodoId.value = null
    boardHoverCategoryId.value = null
    boardHoverInsertIdx.value = 0
    return
  }
  await store.updateTodo(todo.id, {
    title: todo.title,
    body: todo.body,
    priority: todo.priority,
    dueDate: todo.dueDate,
    startDate: todo.startDate,
    recurringInterval: todo.recurringInterval,
    isCompleted: todo.isCompleted,
    boardIds: todo.boardMemberships.map(m => m.boardId),
    categoryIds: categoryId ? [categoryId] : [],
  })
  boardDraggingTodoId.value = null
  boardHoverCategoryId.value = null
  boardHoverInsertIdx.value = 0
}

// ── Init ──────────────────────────────────────────────────────────────────
onMounted(async () => {
  await Promise.all([store.fetchBoards(), store.fetchTodos()])
})
</script>
