<template>
  <div class="flex items-center gap-2 min-w-0 group">
    <!-- Display / edit toggle -->
    <template v-if="!editing">
      <span
        v-if="value"
        class="text-xs font-mono text-gray-300 truncate cursor-pointer hover:text-white transition-colors"
        :title="value"
        @click="startEdit"
      >{{ value }}</span>
      <span
        v-else
        class="text-xs text-gray-600 italic truncate cursor-pointer hover:text-gray-400 transition-colors"
        :title="placeholder"
        @click="startEdit"
      >{{ placeholder || 'not set' }}</span>
      <!-- Config source file badge -->
      <span
        v-if="configSourceFile"
        class="shrink-0 inline-flex items-center gap-1 text-xs text-blue-400 bg-blue-900/20 border border-blue-700/40 px-1.5 py-0.5 rounded font-mono"
        :title="`Set by config file: ${configSourceFile}`"
      >
        <svg class="w-3 h-3 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
        {{ configSourceFile }}
      </span>
      <!-- Warning if config repo may override -->
      <span
        v-else-if="hasConfigRepo && value"
        class="shrink-0 text-yellow-400"
        title="Config repo is active — JSON5 import may overwrite this value"
      >
        <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
          <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
        </svg>
      </span>
      <button
        class="opacity-0 group-hover:opacity-100 transition-opacity shrink-0 text-gray-600 hover:text-gray-300 p-0.5 rounded"
        title="Edit image"
        @click="startEdit"
      >
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
        </svg>
      </button>
    </template>

    <!-- Edit mode -->
    <template v-else>
      <div class="flex-1 min-w-0 flex flex-col gap-1.5">
        <div class="flex items-center gap-1">
          <input
            ref="inputRef"
            v-model="draft"
            type="text"
            :placeholder="placeholder"
            class="flex-1 min-w-0 text-xs font-mono bg-gray-800 border border-brand-500/60 rounded px-2 py-0.5 text-gray-200 placeholder-gray-600 focus:outline-none focus:border-brand-500"
            @keydown.enter="save"
            @keydown.escape="cancel"
          />
          <!-- Helper button to open image selector -->
          <button
            class="shrink-0 text-xs text-gray-400 hover:text-brand-300 transition-colors px-1 border border-gray-700 hover:border-brand-500 rounded py-0.5"
            title="Browse available images"
            @click="showPicker = true"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
            </svg>
          </button>
          <button
            class="shrink-0 text-xs text-gray-500 hover:text-red-400 transition-colors px-1"
            title="Clear (inherit from parent / use default)"
            @click="clear"
          >Clear</button>
          <button
            class="shrink-0 text-xs text-brand-400 hover:text-brand-300 transition-colors px-1"
            :disabled="saving"
            @click="save"
          >{{ saving ? '…' : 'Save' }}</button>
          <button
            class="shrink-0 text-xs text-gray-500 hover:text-gray-300 transition-colors px-1"
            @click="cancel"
          >✕</button>
        </div>
      </div>
    </template>
  </div>

  <!-- Image picker modal -->
  <Teleport to="body">
    <div
      v-if="showPicker"
      class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50"
      @click.self="showPicker = false"
    >
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl p-6 shadow-xl max-h-[80vh] overflow-y-auto">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-bold text-white">Select Docker Image</h2>
          <button class="text-gray-500 hover:text-gray-300 transition-colors" @click="showPicker = false">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <CiCdImageSelector v-model="draft" :inherited-value="value" />
        <div class="mt-4 flex justify-end gap-2">
          <button
            class="text-sm text-gray-400 hover:text-gray-200 border border-gray-700 hover:bg-gray-800 px-3 py-1.5 rounded-lg transition-colors"
            @click="showPicker = false"
          >Cancel</button>
          <button
            class="text-sm bg-brand-600 hover:bg-brand-700 text-white px-4 py-1.5 rounded-lg transition-colors"
            @click="confirmPicker"
          >Apply</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
const props = defineProps<{
  value: string | null
  placeholder: string
  label: string
  hasConfigRepo: boolean
  configSourceFile?: string | null
  required?: boolean
}>()

const emit = defineEmits<{
  save: [image: string | null]
}>()

const editing = ref(false)
const draft = ref('')
const saving = ref(false)
const showPicker = ref(false)
const inputRef = ref<HTMLInputElement | null>(null)

function startEdit() {
  draft.value = props.value ?? ''
  editing.value = true
  nextTick(() => inputRef.value?.focus())
}

async function save() {
  const newVal = draft.value.trim() || null
  saving.value = true
  try {
    emit('save', newVal)
    editing.value = false
  } finally {
    saving.value = false
  }
}

async function clear() {
  saving.value = true
  try {
    emit('save', null)
    editing.value = false
  } finally {
    saving.value = false
  }
}

function cancel() {
  editing.value = false
  showPicker.value = false
}

function confirmPicker() {
  showPicker.value = false
  // draft already updated by v-model on CiCdImageSelector
}
</script>
