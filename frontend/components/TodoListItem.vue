<template>
  <div class="border-b border-gray-800/50 hover:bg-gray-800/30 transition-colors group">
    <div class="flex items-center gap-3 px-4 py-3">
      <!-- Checkbox -->
      <button @click.stop="$emit('toggle')"
        :class="['w-4 h-4 rounded border flex-shrink-0 flex items-center justify-center transition-colors',
          todo.isCompleted ? 'bg-green-600 border-green-600' : 'border-gray-600 hover:border-green-500']">
        <svg v-if="todo.isCompleted" class="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
        </svg>
      </button>

      <!-- Title -->
      <span class="flex-1 text-sm text-gray-200 truncate" :class="todo.isCompleted ? 'line-through text-gray-500' : ''">
        {{ todo.title }}
      </span>

      <!-- Meta -->
      <div class="flex items-center gap-3 shrink-0">
        <span v-if="todo.priority !== TodoPriority.NoPriority"
          :class="['text-xs px-1.5 py-0.5 rounded', priorityClass]">
          {{ TodoPriorityLabels[todo.priority] }}
        </span>
        <span v-if="todo.recurringInterval !== TodoRecurringInterval.None"
          class="text-xs text-gray-500" :title="TodoRecurringIntervalLabels[todo.recurringInterval]">
          ↻
        </span>
        <span v-if="todo.dueDate"
          :class="['text-xs', isOverdue ? 'text-red-400' : 'text-gray-500']">
          {{ formatDate(todo.dueDate) }}
        </span>
      </div>

      <!-- Actions -->
      <div class="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
        <button @click.stop="$emit('edit')" class="p-1 text-gray-600 hover:text-gray-300 transition-colors">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
          </svg>
        </button>
        <button @click.stop="$emit('delete')" class="p-1 text-gray-600 hover:text-red-400 transition-colors">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
          </svg>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Todo } from '~/types'
import { TodoPriority, TodoPriorityLabels, TodoRecurringInterval, TodoRecurringIntervalLabels } from '~/types'

const props = defineProps<{ todo: Todo }>()
defineEmits<{ toggle: []; edit: []; delete: [] }>()

const isOverdue = computed(() => {
  if (!props.todo.dueDate || props.todo.isCompleted) return false
  return new Date(props.todo.dueDate) < new Date()
})

const priorityClass = computed(() => {
  switch (props.todo.priority) {
    case TodoPriority.Urgent: return 'bg-red-900/60 text-red-300'
    case TodoPriority.High: return 'bg-orange-900/60 text-orange-300'
    case TodoPriority.Medium: return 'bg-yellow-900/60 text-yellow-300'
    case TodoPriority.Low: return 'bg-blue-900/60 text-blue-300'
    default: return 'bg-gray-800 text-gray-400'
  }
})

function formatDate(dateStr: string): string {
  const d = new Date(dateStr)
  const now = new Date()
  const diffDays = Math.floor((d.getTime() - now.getTime()) / (1000 * 60 * 60 * 24))
  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return 'Tomorrow'
  if (diffDays === -1) return 'Yesterday'
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}
</script>
