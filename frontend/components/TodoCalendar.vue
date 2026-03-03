<template>
  <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden flex flex-col">
    <!-- Calendar Header -->
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-800">
      <button @click="prevMonth"
        class="text-gray-400 hover:text-gray-200 transition-colors p-1 rounded">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </button>
      <h2 class="text-sm font-semibold text-white">
        {{ monthYear }}
      </h2>
      <button @click="nextMonth"
        class="text-gray-400 hover:text-gray-200 transition-colors p-1 rounded">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
        </svg>
      </button>
    </div>

    <!-- Day names -->
    <div class="grid grid-cols-7 border-b border-gray-800">
      <div v-for="day in dayNames" :key="day"
        class="py-2 text-center text-xs font-medium text-gray-500">
        {{ day }}
      </div>
    </div>

    <!-- Calendar Grid -->
    <div class="grid grid-cols-7 flex-1">
      <div v-for="(cell, idx) in calendarCells" :key="idx"
        :class="['min-h-20 border-b border-r border-gray-800/60 p-1.5 relative',
          cell.isToday ? 'bg-brand-900/20' : '',
          cell.isCurrentMonth ? '' : 'opacity-40']"
        @click="cell.date && $emit('create-on-date', cell.date)">

        <span v-if="cell.day"
          :class="['text-xs font-medium w-5 h-5 flex items-center justify-center rounded-full mb-1',
            cell.isToday ? 'bg-brand-600 text-white' : 'text-gray-400']">
          {{ cell.day }}
        </span>

        <!-- Todos for this day -->
        <div class="space-y-0.5">
          <div v-for="todo in (cell.todos ?? [])" :key="todo.id"
            @click.stop="$emit('select', todo)"
            :class="['text-xs px-1.5 py-0.5 rounded truncate cursor-pointer transition-colors',
              todo.isCompleted ? 'line-through text-gray-600 bg-gray-800/40' : 'bg-brand-900/60 text-brand-300 hover:bg-brand-900']">
            {{ todo.title }}
          </div>
          <div v-if="(cell.extraCount ?? 0) > 0"
            class="text-xs text-gray-500 px-1">
            +{{ cell.extraCount }} more
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Todo } from '~/types'

const props = defineProps<{ todos: Todo[] }>()
defineEmits<{ select: [todo: Todo]; 'create-on-date': [date: Date] }>()

const today = new Date()
const currentYear = ref(today.getFullYear())
const currentMonth = ref(today.getMonth()) // 0-based

const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

const monthYear = computed(() =>
  new Date(currentYear.value, currentMonth.value, 1).toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric',
  })
)

function prevMonth() {
  if (currentMonth.value === 0) {
    currentMonth.value = 11
    currentYear.value--
  } else {
    currentMonth.value--
  }
}

function nextMonth() {
  if (currentMonth.value === 11) {
    currentMonth.value = 0
    currentYear.value++
  } else {
    currentMonth.value++
  }
}

interface CalendarCell {
  day: number | null
  date: Date | null
  isToday: boolean
  isCurrentMonth: boolean
  todos: Todo[]
  extraCount: number
}

const MAX_VISIBLE = 3

const calendarCells = computed((): CalendarCell[] => {
  const year = currentYear.value
  const month = currentMonth.value

  const firstDay = new Date(year, month, 1).getDay()
  const daysInMonth = new Date(year, month + 1, 0).getDate()

  // Build a map of date-string → todos
  const todosByDate: Record<string, Todo[]> = {}
  for (const todo of props.todos) {
    if (!todo.dueDate) continue
    const d = new Date(todo.dueDate)
    const key = `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
    if (!todosByDate[key]) todosByDate[key] = []
    todosByDate[key].push(todo)
  }

  const cells: CalendarCell[] = []

  // Leading empty cells
  for (let i = 0; i < firstDay; i++) {
    cells.push({ day: null, date: null, isToday: false, isCurrentMonth: false, todos: [], extraCount: 0 })
  }

  for (let d = 1; d <= daysInMonth; d++) {
    const date = new Date(year, month, d)
    const key = `${year}-${month}-${d}`
    const isToday =
      d === today.getDate() && month === today.getMonth() && year === today.getFullYear()
    const allTodos = todosByDate[key] ?? []
    const visible = allTodos.slice(0, MAX_VISIBLE)
    cells.push({
      day: d,
      date,
      isToday,
      isCurrentMonth: true,
      todos: visible,
      extraCount: Math.max(0, allTodos.length - MAX_VISIBLE),
    })
  }

  // Trailing cells to complete the grid (fill to multiple of 7)
  while (cells.length % 7 !== 0) {
    cells.push({ day: null, date: null, isToday: false, isCurrentMonth: false, todos: [], extraCount: 0 })
  }

  return cells
})
</script>
