<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <h1 class="text-2xl font-bold text-white">Organizations</h1>
        <p class="text-gray-400 mt-1">{{ store.orgs.length }} organization{{ store.orgs.length === 1 ? '' : 's' }}</p>
      </div>
      <button
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Organization
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Table -->
    <div v-else-if="store.orgs.length" class="rounded-xl border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Slug</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Description</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="org in store.orgs" :key="org.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3">
              <NuxtLink :to="`/orgs/${org.id}`" class="font-medium text-white hover:text-brand-300 transition-colors">
                {{ org.name }}
              </NuxtLink>
            </td>
            <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ org.slug }}</td>
            <td class="px-4 py-3 text-gray-400 text-sm">{{ org.description || '—' }}</td>
            <td class="px-4 py-3 text-gray-400">{{ formatDate(org.createdAt) }}</td>
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-2">
                <button
                  class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                  @click="openEdit(org)"
                >
                  Edit
                </button>
                <button
                  class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                  @click="confirmDelete(org.id, org.name)"
                >
                  Delete
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Empty state -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <div class="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
        <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
            d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
      </div>
      <p class="text-gray-400 font-medium">No organizations yet</p>
      <p class="text-gray-600 text-sm mt-1">Create your first organization to get started</p>
      <button
        class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        Create Organization
      </button>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="mt-4 bg-red-900/20 border border-red-800 rounded-lg px-4 py-3 text-sm text-red-400">
      {{ store.error }}
    </div>

    <!-- Create / Edit Modal -->
    <div v-if="showModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingId ? 'Edit Organization' : 'New Organization' }}</h2>
        <form class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input
              v-model="form.name"
              type="text"
              required
              placeholder="Acme Corp"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Slug</label>
            <input
              v-model="form.slug"
              type="text"
              required
              placeholder="acme-corp"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea
              v-model="form.description"
              rows="3"
              placeholder="Optional description..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"
            />
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="saving"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ saving ? 'Saving…' : editingId ? 'Update' : 'Create' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeModal"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useOrgsStore } from '~/stores/orgs'
import type { Organization } from '~/types'

const store = useOrgsStore()

onMounted(() => store.fetchOrgs())

const showModal = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)

const form = reactive({ name: '', slug: '', description: '' })

watch(() => form.name, (val) => {
  if (!editingId.value) {
    form.slug = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  }
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', slug: '', description: '' })
  showModal.value = true
}

function openEdit(org: Organization) {
  editingId.value = org.id
  Object.assign(form, { name: org.name, slug: org.slug, description: org.description || '' })
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  editingId.value = null
}

async function handleSubmit() {
  if (!form.name || !form.slug) return
  saving.value = true
  try {
    if (editingId.value) {
      await store.updateOrg(editingId.value, { name: form.name, slug: form.slug })
    } else {
      await store.createOrg({ name: form.name, slug: form.slug, description: form.description || undefined })
    }
    closeModal()
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete organization "${name}"? This cannot be undone.`)) {
    store.deleteOrg(id)
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
