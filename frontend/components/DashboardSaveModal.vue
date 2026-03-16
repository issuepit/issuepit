<template>
  <div class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50" @click.self="$emit('close')">
    <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
      <h2 class="text-lg font-bold text-white mb-5">Save Dashboard Layout</h2>

      <div class="space-y-4">
        <!-- Name -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Layout name</label>
          <input
            v-model="name"
            type="text"
            placeholder="e.g. Sprint view, Minimal…"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            @keyup.enter="handleSave"
          />
        </div>

        <!-- Scope -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Save as</label>
          <div class="space-y-2">
            <label class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors"
              :class="scope === 'user' ? 'border-brand-500 bg-brand-900/30' : 'border-gray-700 hover:border-gray-600'">
              <input type="radio" v-model="scope" value="user" class="mt-0.5 accent-brand-500" />
              <div>
                <p class="text-sm font-medium text-white">Personal preset</p>
                <p class="text-xs text-gray-400">Only visible to you</p>
              </div>
            </label>
            <label v-if="showProjectDefault" class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors"
              :class="scope === 'project_default' ? 'border-amber-500 bg-amber-900/20' : 'border-gray-700 hover:border-gray-600'">
              <input type="radio" v-model="scope" value="project_default" class="mt-0.5 accent-amber-500" />
              <div>
                <p class="text-sm font-medium text-white">Project default</p>
                <p class="text-xs text-gray-400">Used by all project members who haven't customised their layout</p>
              </div>
            </label>
            <label class="flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors"
              :class="scope === 'shared' ? 'border-green-500 bg-green-900/20' : 'border-gray-700 hover:border-gray-600'">
              <input type="radio" v-model="scope" value="shared" class="mt-0.5 accent-green-500" />
              <div>
                <p class="text-sm font-medium text-white">Shared template</p>
                <p class="text-xs text-gray-400">Visible to all team members as a reusable template</p>
              </div>
            </label>
          </div>
        </div>
      </div>

      <p v-if="errorMsg" class="mt-3 text-sm text-red-400">{{ errorMsg }}</p>

      <div class="flex gap-3 mt-6">
        <button
          @click="handleSave"
          :disabled="!name.trim() || saving"
          class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
        <button
          @click="$emit('close')"
          class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  layoutJson: string
  dashboardType: string
  projectId?: string
  showProjectDefault?: boolean
}>()

const emit = defineEmits<{
  (e: 'close' | 'saved'): void
}>()

const { saveTemplate, error: apiError } = useDashboardTemplates()

const name = ref('')
const scope = ref<'user' | 'project_default' | 'shared'>('user')
const saving = ref(false)
const errorMsg = ref<string | null>(null)

async function handleSave() {
  if (!name.value.trim()) return
  saving.value = true
  errorMsg.value = null
  const result = await saveTemplate(
    name.value.trim(),
    props.dashboardType,
    scope.value,
    props.layoutJson,
    props.projectId,
  )
  saving.value = false
  if (result) {
    emit('saved')
    emit('close')
  } else {
    errorMsg.value = apiError.value ?? 'Failed to save'
  }
}
</script>
