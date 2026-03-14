<template>
  <div class="p-8">
    <div class="flex items-center gap-2 mb-6">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Custom Properties', to: `/projects/${id}/settings/custom-properties`, icon: 'M4 6h16M4 10h16M4 14h16M4 18h16' },
      ]" />
    </div>

    <div class="max-w-2xl space-y-6">
      <!-- Header -->
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-xl font-bold text-white">Custom Properties</h1>
          <p class="text-sm text-gray-400 mt-1">Define additional fields for issues in this project.</p>
        </div>
        <button @click="openCreate"
          class="text-sm bg-brand-600 hover:bg-brand-700 text-white px-4 py-2 rounded-lg transition-colors">
          + Add Property
        </button>
      </div>

      <!-- Loading -->
      <div v-if="store.loading" class="flex items-center justify-center py-12">
        <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
      </div>

      <!-- Empty state -->
      <div v-else-if="!store.properties.length"
        class="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center text-gray-500 text-sm">
        No custom properties yet. Click <strong class="text-gray-400">+ Add Property</strong> to create one.
      </div>

      <!-- List -->
      <div v-else class="space-y-3">
        <div v-for="prop in store.properties" :key="prop.id"
          class="bg-gray-900 border border-gray-800 rounded-xl p-4 flex items-center justify-between gap-4">
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-white">{{ prop.name }}</span>
              <span v-if="prop.isRequired" class="text-xs bg-red-900/40 text-red-300 px-1.5 py-0.5 rounded">Required</span>
            </div>
            <div class="flex items-center gap-2 mt-1">
              <span class="text-xs text-gray-500 capitalize">{{ prop.type }}</span>
              <span v-if="prop.defaultValue" class="text-xs text-gray-600">· Default: {{ prop.defaultValue }}</span>
              <span v-if="prop.allowedValues" class="text-xs text-gray-600">· Values: {{ formatAllowedValues(prop.allowedValues) }}</span>
            </div>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <button @click="openEdit(prop)"
              class="text-xs text-gray-400 hover:text-white bg-gray-800 hover:bg-gray-700 px-2.5 py-1.5 rounded-lg transition-colors">
              Edit
            </button>
            <button @click="confirmDelete(prop)"
              class="text-xs text-gray-400 hover:text-red-400 bg-gray-800 hover:bg-gray-700 px-2.5 py-1.5 rounded-lg transition-colors">
              Delete
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Create / Edit Modal -->
    <div v-if="showForm" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editId ? 'Edit Property' : 'New Custom Property' }}</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input v-model="form.name" type="text" placeholder="e.g. Due Date"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Type</label>
            <select v-model="form.type"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="t in typeOptions" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
          <div v-if="form.type === CustomPropertyType.Enum">
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Allowed Values <span class="text-gray-500">(comma-separated)</span></label>
            <input v-model="allowedValuesInput" type="text" placeholder="e.g. frontend,backend,api"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Default Value <span class="text-gray-500">(optional)</span></label>
            <input v-model="form.defaultValue" type="text" placeholder="Leave blank for none"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div class="flex items-center gap-3">
            <input id="isRequired" v-model="form.isRequired" type="checkbox"
              class="rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
            <label for="isRequired" class="text-sm text-gray-300">Required field</label>
          </div>
        </div>
        <div v-if="store.error" class="mt-3 text-sm text-red-400">{{ store.error }}</div>
        <div class="flex gap-3 mt-6">
          <button @click="submitForm"
            :disabled="!form.name.trim() || store.loading"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ editId ? 'Save' : 'Create' }}
          </button>
          <button @click="closeForm"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Delete Confirm Modal -->
    <div v-if="deleteTarget" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-sm p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-3">Delete Property</h2>
        <p class="text-sm text-gray-400 mb-5">
          Are you sure you want to delete <strong class="text-white">{{ deleteTarget.name }}</strong>?
          All associated issue values will also be removed.
        </p>
        <div class="flex gap-3">
          <button @click="doDelete"
            :disabled="store.loading"
            class="flex-1 bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Delete
          </button>
          <button @click="deleteTarget = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { CustomPropertyType } from '~/types'
import type { CustomProperty } from '~/types'
import { useCustomPropertiesStore } from '~/stores/customProperties'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const projectsStore = useProjectsStore()
const store = useCustomPropertiesStore()

onMounted(() => {
  store.fetchProperties(id)
})

const typeOptions = [
  { value: CustomPropertyType.Text, label: 'Text' },
  { value: CustomPropertyType.Enum, label: 'Enum (dropdown)' },
  { value: CustomPropertyType.Number, label: 'Number' },
  { value: CustomPropertyType.Date, label: 'Date' },
  { value: CustomPropertyType.Person, label: 'Person' },
  { value: CustomPropertyType.Agent, label: 'Agent' },
  { value: CustomPropertyType.Bool, label: 'Boolean' },
]

// ── Form state ────────────────────────────────────────────────────────────
const showForm = ref(false)
const editId = ref<string | null>(null)
const allowedValuesInput = ref('')
const form = ref({
  name: '',
  type: CustomPropertyType.Text as CustomPropertyType,
  isRequired: false,
  defaultValue: '' as string | undefined,
  allowedValues: null as string | null | undefined,
})

function openCreate() {
  editId.value = null
  allowedValuesInput.value = ''
  form.value = { name: '', type: CustomPropertyType.Text, isRequired: false, defaultValue: '', allowedValues: null }
  showForm.value = true
}

function openEdit(prop: CustomProperty) {
  editId.value = prop.id
  const vals = prop.allowedValues ? JSON.parse(prop.allowedValues) : []
  allowedValuesInput.value = Array.isArray(vals) ? vals.join(',') : ''
  form.value = {
    name: prop.name,
    type: prop.type,
    isRequired: prop.isRequired,
    defaultValue: prop.defaultValue ?? '',
    allowedValues: prop.allowedValues,
  }
  showForm.value = true
}

function closeForm() {
  showForm.value = false
  editId.value = null
}

async function submitForm() {
  if (!form.value.name.trim()) return
  const allowedValues = form.value.type === CustomPropertyType.Enum && allowedValuesInput.value.trim()
    ? JSON.stringify(allowedValuesInput.value.split(',').map(v => v.trim()).filter(Boolean))
    : null
  const payload = {
    name: form.value.name.trim(),
    type: form.value.type,
    isRequired: form.value.isRequired,
    defaultValue: form.value.defaultValue || null,
    allowedValues,
  }
  if (editId.value) {
    await store.updateProperty(id, editId.value, payload)
  }
  else {
    await store.createProperty(id, payload)
  }
  if (!store.error) closeForm()
}

// ── Delete state ──────────────────────────────────────────────────────────
const deleteTarget = ref<CustomProperty | null>(null)

function confirmDelete(prop: CustomProperty) {
  deleteTarget.value = prop
}

async function doDelete() {
  if (!deleteTarget.value) return
  await store.deleteProperty(id, deleteTarget.value.id)
  deleteTarget.value = null
}

// ── Helpers ───────────────────────────────────────────────────────────────
function formatAllowedValues(raw: string | null | undefined) {
  if (!raw) return ''
  try {
    const vals = JSON.parse(raw)
    if (Array.isArray(vals)) return vals.join(', ')
    return raw
  }
  catch {
    return raw
  }
}
</script>
