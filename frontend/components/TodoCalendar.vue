<template>
  <div class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden flex flex-col h-full">
    <!-- Calendar Header -->
    <div class="flex items-center justify-between px-4 py-3 border-b border-gray-800 shrink-0">
      <button @click="prev"
        class="text-gray-400 hover:text-gray-200 transition-colors p-1 rounded">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </button>
      <div class="flex items-center gap-3">
        <h2 class="text-sm font-semibold text-white">
          {{ headerLabel }}
        </h2>
        <!-- View toggle -->
        <div class="flex bg-gray-800 rounded-lg p-0.5 border border-gray-700">
          <button @click="calendarMode = 'month'"
            :class="['px-2.5 py-0.5 text-xs rounded-md transition-colors', calendarMode === 'month' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300']">
            Month
          </button>
          <button @click="calendarMode = 'week'"
            :class="['px-2.5 py-0.5 text-xs rounded-md transition-colors', calendarMode === 'week' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300']">
            Week
          </button>
        </div>
      </div>
      <button @click="next"
        class="text-gray-400 hover:text-gray-200 transition-colors p-1 rounded">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
        </svg>
      </button>
    </div>

    <!-- ── Monthly View ── -->
    <template v-if="calendarMode === 'month'">
      <!-- Day names -->
      <div class="grid grid-cols-7 border-b border-gray-800 shrink-0">
        <div v-for="day in dayNames" :key="day"
          class="py-2 text-center text-xs font-medium text-gray-500">
          {{ day }}
        </div>
      </div>

      <!-- Calendar Grid -->
      <div class="grid grid-cols-7 flex-1 overflow-auto">
        <div v-for="(cell, idx) in calendarCells" :key="idx"
          :class="['min-h-20 border-b border-r border-gray-800/60 p-1.5 relative cursor-pointer',
            cell.isToday ? 'bg-brand-900/20' : 'hover:bg-gray-800/30',
            cell.isCurrentMonth ? '' : 'opacity-40']"
          @click="cell.date && onMonthCellClick(cell.date)">

          <span v-if="cell.day"
            :class="['text-xs font-medium w-5 h-5 flex items-center justify-center rounded-full mb-1',
              cell.isToday ? 'bg-brand-600 text-white' : 'text-gray-400']">
            {{ cell.day }}
          </span>

          <!-- Todos for this day -->
          <div class="space-y-0.5">
            <div v-for="todo in (cell.todos ?? [])" :key="todo.id"
              draggable="true"
              @dragstart.stop="onDragStart(todo)"
              @dragend.stop="onDragEnd"
              @click.stop="$emit('select', todo)"
              :class="['text-xs px-1.5 py-0.5 rounded truncate cursor-grab active:cursor-grabbing transition-colors',
                todo.isCompleted ? 'line-through text-gray-600 bg-gray-800/40' : 'bg-brand-900/60 text-brand-300 hover:bg-brand-900']">
              {{ todo.title }}
            </div>
            <div v-if="(cell.extraCount ?? 0) > 0"
              class="text-xs text-gray-500 px-1">
              +{{ cell.extraCount }} more
            </div>
          </div>

          <!-- Drop target overlay -->
          <div v-if="draggingTodo && cell.date"
            @dragover.prevent="monthHoverDate = cell.date"
            @dragleave="monthHoverDate = null"
            @drop.prevent="onMonthDrop(cell.date)"
            :class="['absolute inset-0 rounded transition-colors z-10',
              monthHoverDate && isSameDay(monthHoverDate, cell.date) ? 'bg-brand-500/20 ring-1 ring-brand-500' : 'bg-transparent']">
          </div>
        </div>
      </div>
    </template>

    <!-- ── Weekly View ── -->
    <template v-else-if="calendarMode === 'week'">
      <!-- Day headers -->
      <div class="grid shrink-0 border-b border-gray-800 bg-gray-900"
        style="grid-template-columns: 52px repeat(7, 1fr)">
        <div class="border-r border-gray-800 py-2" />
        <div v-for="day in weekDays" :key="day.key"
          :class="['py-2 text-center border-r border-gray-800/60 last:border-r-0',
            day.isToday ? 'bg-brand-900/20' : '']">
          <div class="text-xs text-gray-500 font-medium">{{ day.shortName }}</div>
          <div :class="['text-sm font-semibold mx-auto w-7 h-7 flex items-center justify-center rounded-full',
            day.isToday ? 'bg-brand-600 text-white' : 'text-gray-300']">
            {{ day.dayNum }}
          </div>
        </div>
      </div>

      <!-- Time grid (scrollable) -->
      <div class="flex-1 overflow-y-auto" ref="weekScrollContainer">
        <div class="grid"
          style="grid-template-columns: 52px repeat(7, 1fr)">
          <template v-for="slot in timeSlots" :key="slot.minutes">
            <!-- Time label -->
            <div :class="['border-r border-b border-gray-800/40 px-1 text-right shrink-0',
              slot.isHour ? 'border-b-gray-800' : 'border-b-gray-800/20']"
              style="height: 32px">
              <span v-if="slot.isHour" class="text-xs text-gray-600 leading-none block mt-0.5">
                {{ slot.label }}
              </span>
            </div>

            <!-- Day cells for this slot -->
            <div v-for="day in weekDays" :key="day.key + slot.minutes"
              :class="['border-r border-b border-gray-800/40 last:border-r-0 relative',
                slot.isHour ? 'border-b-gray-800' : 'border-b-gray-800/20',
                weekHoverSlot?.dayKey === day.key && weekHoverSlot?.minutes === slot.minutes
                  ? 'bg-brand-500/10'
                  : day.isToday ? 'bg-brand-900/10' : 'hover:bg-gray-800/20 cursor-pointer']"
              style="height: 32px; min-width: 0"
              @click="!draggingTodo && onWeekSlotClick(day.date, slot.minutes)"
              @dragover.prevent="weekHoverSlot = { dayKey: day.key, minutes: slot.minutes }"
              @dragleave="onWeekDragLeave($event)"
              @drop.prevent="onWeekSlotDrop(day.date, slot.minutes)">

              <!-- Todos in this slot -->
              <template v-for="todo in (weekTodos[day.key]?.[slot.minutes] ?? [])" :key="todo.id">
                <div
                  draggable="true"
                  @dragstart.stop="onDragStart(todo)"
                  @dragend.stop="onDragEnd"
                  @click.stop="$emit('select', todo)"
                  :class="['absolute inset-x-0.5 top-0.5 text-xs px-1 rounded truncate cursor-grab active:cursor-grabbing transition-colors z-10',
                    todo.isCompleted
                      ? 'line-through text-gray-600 bg-gray-800/60'
                      : 'bg-brand-600/80 text-white hover:bg-brand-500/80']"
                  style="font-size: 10px; line-height: 22px; height: 22px">
                  {{ todo.title }}
                </div>
              </template>
            </div>
          </template>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import type { Todo } from '~/types'

const props = defineProps<{ todos: Todo[] }>()
const emit = defineEmits<{
  select: [todo: Todo]
  'create-on-date': [date: Date]
  reschedule: [todo: Todo, newDate: Date]
}>()

// ── View mode ──────────────────────────────────────────────────────────────
const calendarMode = ref<'month' | 'week'>('month')

// ── Shared ─────────────────────────────────────────────────────────────────
const today = new Date()
const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

// ── Monthly view state ─────────────────────────────────────────────────────
const currentYear = ref(today.getFullYear())
const currentMonth = ref(today.getMonth())

// ── Weekly view state ──────────────────────────────────────────────────────
// Track the Monday of the current week
function getMondayOf(d: Date) {
  const day = d.getDay()
  const diff = (day === 0 ? -6 : 1 - day)
  const mon = new Date(d)
  mon.setHours(0, 0, 0, 0)
  mon.setDate(d.getDate() + diff)
  return mon
}
const weekStart = ref<Date>(getMondayOf(today))
const weekScrollContainer = ref<HTMLElement | null>(null)

// ── Navigation ─────────────────────────────────────────────────────────────
function prev() {
  if (calendarMode.value === 'month') {
    if (currentMonth.value === 0) { currentMonth.value = 11; currentYear.value-- }
    else currentMonth.value--
  } else {
    const d = new Date(weekStart.value)
    d.setDate(d.getDate() - 7)
    weekStart.value = d
  }
}

function next() {
  if (calendarMode.value === 'month') {
    if (currentMonth.value === 11) { currentMonth.value = 0; currentYear.value++ }
    else currentMonth.value++
  } else {
    const d = new Date(weekStart.value)
    d.setDate(d.getDate() + 7)
    weekStart.value = d
  }
}

// When switching to week view, sync week to currently viewed month
const SLOT_HEIGHT_PX = 32
const SLOTS_PER_HOUR = 2
const DEFAULT_SCROLL_HOUR = 8

function scrollToDefaultHour() {
  if (weekScrollContainer.value) {
    weekScrollContainer.value.scrollTop = DEFAULT_SCROLL_HOUR * SLOTS_PER_HOUR * SLOT_HEIGHT_PX
  }
}

watch(calendarMode, (mode) => {
  if (mode === 'week') {
    // If the current month contains today, jump to today's week; else first week of month
    const midMonth = new Date(currentYear.value, currentMonth.value, 15)
    const todayInView = today.getFullYear() === currentYear.value && today.getMonth() === currentMonth.value
    weekStart.value = getMondayOf(todayInView ? today : midMonth)
    nextTick(() => { scrollToDefaultHour() })
  } else {
    currentYear.value = weekStart.value.getFullYear()
    currentMonth.value = weekStart.value.getMonth()
  }
})

onMounted(() => {
  nextTick(() => { scrollToDefaultHour() })
})

// ── Header label ───────────────────────────────────────────────────────────
const headerLabel = computed(() => {
  if (calendarMode.value === 'month') {
    return new Date(currentYear.value, currentMonth.value, 1).toLocaleDateString(undefined, {
      month: 'long', year: 'numeric',
    })
  }
  // Week range
  const end = new Date(weekStart.value)
  end.setDate(end.getDate() + 6)
  const startStr = weekStart.value.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
  const endStr = end.toLocaleDateString(undefined, {
    month: weekStart.value.getMonth() === end.getMonth() ? undefined : 'short',
    day: 'numeric',
    year: 'numeric',
  })
  return `${startStr} – ${endStr}`
})

// ── Monthly data ───────────────────────────────────────────────────────────
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

  const todosByDate: Record<string, Todo[]> = {}
  for (const todo of props.todos) {
    if (!todo.dueDate) continue
    const d = new Date(todo.dueDate)
    const key = `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`
    if (!todosByDate[key]) todosByDate[key] = []
    todosByDate[key].push(todo)
  }

  const cells: CalendarCell[] = []
  for (let i = 0; i < firstDay; i++) {
    cells.push({ day: null, date: null, isToday: false, isCurrentMonth: false, todos: [], extraCount: 0 })
  }
  for (let d = 1; d <= daysInMonth; d++) {
    const date = new Date(year, month, d)
    const key = `${year}-${month}-${d}`
    const isToday = d === today.getDate() && month === today.getMonth() && year === today.getFullYear()
    const allTodos = todosByDate[key] ?? []
    cells.push({
      day: d,
      date,
      isToday,
      isCurrentMonth: true,
      todos: allTodos.slice(0, MAX_VISIBLE),
      extraCount: Math.max(0, allTodos.length - MAX_VISIBLE),
    })
  }
  while (cells.length % 7 !== 0) {
    cells.push({ day: null, date: null, isToday: false, isCurrentMonth: false, todos: [], extraCount: 0 })
  }
  return cells
})

function onMonthCellClick(date: Date) {
  // Default time: noon
  const d = new Date(date)
  d.setHours(12, 0, 0, 0)
  emit('create-on-date', d)
}

// ── Weekly data ────────────────────────────────────────────────────────────
interface WeekDay {
  date: Date
  key: string   // YYYY-MM-DD
  shortName: string
  dayNum: number
  isToday: boolean
}

const weekDays = computed((): WeekDay[] => {
  const days: WeekDay[] = []
  for (let i = 0; i < 7; i++) {
    const d = new Date(weekStart.value)
    d.setDate(d.getDate() + i)
    const key = dateKey(d)
    days.push({
      date: d,
      key,
      shortName: d.toLocaleDateString(undefined, { weekday: 'short' }),
      dayNum: d.getDate(),
      isToday: isSameDay(d, today),
    })
  }
  return days
})

// 30-min time slots: 0..47 representing minutes 0, 30, 60, ... 1410
interface TimeSlot {
  minutes: number   // 0, 30, 60, ...
  label: string     // "00:00", "01:00", ...
  isHour: boolean
}

const timeSlots = computed((): TimeSlot[] => {
  const slots: TimeSlot[] = []
  for (let h = 0; h < 24; h++) {
    for (const m of [0, 30]) {
      const totalMins = h * 60 + m
      slots.push({
        minutes: totalMins,
        label: `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`,
        isHour: m === 0,
      })
    }
  }
  return slots
})

// Map: dayKey → { slotMinutes → Todo[] }
const weekTodos = computed((): Record<string, Record<number, Todo[]>> => {
  const map: Record<string, Record<number, Todo[]>> = {}
  for (const todo of props.todos) {
    if (!todo.dueDate) continue
    const d = new Date(todo.dueDate)
    const key = dateKey(d)
    if (!map[key]) map[key] = {}
    const slotMins = Math.floor((d.getHours() * 60 + d.getMinutes()) / 30) * 30
    if (!map[key][slotMins]) map[key][slotMins] = []
    map[key][slotMins].push(todo)
  }
  return map
})

function onWeekSlotClick(date: Date, slotMinutes: number) {
  const d = new Date(date)
  d.setHours(Math.floor(slotMinutes / 60), slotMinutes % 60, 0, 0)
  emit('create-on-date', d)
}

// ── Drag & Drop ────────────────────────────────────────────────────────────
const draggingTodo = ref<Todo | null>(null)
const monthHoverDate = ref<Date | null>(null)
const weekHoverSlot = ref<{ dayKey: string; minutes: number } | null>(null)

function onDragStart(todo: Todo) {
  draggingTodo.value = todo
}

function onDragEnd() {
  draggingTodo.value = null
  monthHoverDate.value = null
  weekHoverSlot.value = null
}

function onMonthDrop(date: Date) {
  if (!draggingTodo.value) return
  const todo = draggingTodo.value
  const newDate = new Date(date)
  // Preserve the existing time if available, otherwise use noon
  if (todo.dueDate) {
    const existing = new Date(todo.dueDate)
    newDate.setHours(existing.getHours(), existing.getMinutes(), 0, 0)
  } else {
    newDate.setHours(12, 0, 0, 0)
  }
  emit('reschedule', todo, newDate)
  draggingTodo.value = null
  monthHoverDate.value = null
}

function onWeekSlotDrop(date: Date, slotMinutes: number) {
  if (!draggingTodo.value) return
  const newDate = new Date(date)
  newDate.setHours(Math.floor(slotMinutes / 60), slotMinutes % 60, 0, 0)
  emit('reschedule', draggingTodo.value, newDate)
  draggingTodo.value = null
  weekHoverSlot.value = null
}

function onWeekDragLeave(event: DragEvent) {
  // Only clear the hover if the mouse is truly leaving the cell (not entering a child element)
  const relatedTarget = event.relatedTarget as Node | null
  if (relatedTarget && (event.currentTarget as Node)?.contains(relatedTarget)) return
  weekHoverSlot.value = null
}

// ── Helpers ────────────────────────────────────────────────────────────────
function dateKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}
</script>
