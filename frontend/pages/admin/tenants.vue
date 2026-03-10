<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <PageBreadcrumb :items="[
        { label: 'Admin', to: '/admin/tenants', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4' },
        { label: 'Tenants', to: '/admin/tenants', icon: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4' },
      ]" />
      <button
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Tenant
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Tenants table -->
    <div v-else-if="store.tenants.length" class="rounded-xl border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Hostname</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Database</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="tenant in store.tenants" :key="tenant.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ tenant.name }}</td>
            <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ tenant.hostname }}</td>
            <td class="px-4 py-3">
              <span v-if="tenant.databaseConnectionString"
                class="inline-flex items-center gap-1 text-xs bg-green-900/30 text-green-400 px-2 py-0.5 rounded-full">
                <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                  <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
                Provisioned
              </span>
              <span v-else class="text-xs text-gray-500">—</span>
            </td>
            <td class="px-4 py-3 text-gray-400">{{ formatDate(tenant.createdAt) }}</td>
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-2">
                <button
                  class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                  @click="openEdit(tenant)"
                >
                  Edit
                </button>
                <button
                  class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                  @click="confirmDelete(tenant.id, tenant.name)"
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
            d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
        </svg>
      </div>
      <p class="text-gray-400 font-medium">No tenants yet</p>
      <p class="text-gray-600 text-sm mt-1">Create your first tenant to get started</p>
      <button
        class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        Create Tenant
      </button>
    </div>

    <!-- Create / Edit Modal -->
    <div v-if="showModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingId ? 'Edit Tenant' : 'New Tenant' }}</h2>
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
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Hostname</label>
            <input
              v-model="form.hostname"
              type="text"
              required
              placeholder="acme.example.com"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
            <p class="text-xs text-gray-500 mt-1">Used to resolve the tenant from incoming requests.</p>
          </div>
          <div v-if="!editingId" class="flex items-start gap-3 rounded-lg bg-gray-800/50 border border-gray-700 p-3">
            <input
              id="provision-db"
              v-model="form.provisionDatabase"
              type="checkbox"
              class="mt-0.5 w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
            />
            <label for="provision-db" class="text-sm text-gray-300 cursor-pointer">
              <span class="font-medium">Auto-provision PostgreSQL database</span>
              <span class="block text-xs text-gray-500 mt-0.5">
                Creates a dedicated <code class="font-mono bg-gray-700 px-1 rounded">issuepit_&lt;name&gt;</code> database
                on the connected PostgreSQL server.
              </span>
            </label>
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
import { useTenantsStore } from '~/stores/tenants'
import type { Tenant } from '~/types'

const store = useTenantsStore()

onMounted(() => store.fetchTenants())

const showModal = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)

const form = reactive({
  name: '',
  hostname: '',
  provisionDatabase: false
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', hostname: '', provisionDatabase: false })
  showModal.value = true
}

function openEdit(tenant: Tenant) {
  editingId.value = tenant.id
  Object.assign(form, { name: tenant.name, hostname: tenant.hostname, provisionDatabase: false })
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  editingId.value = null
}

async function handleSubmit() {
  if (!form.name || !form.hostname) return
  saving.value = true
  try {
    if (editingId.value) {
      await store.updateTenant(editingId.value, { name: form.name, hostname: form.hostname })
    } else {
      await store.createTenant({ name: form.name, hostname: form.hostname, provisionDatabase: form.provisionDatabase })
    }
    closeModal()
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete tenant "${name}"? This cannot be undone.`)) {
    store.deleteTenant(id)
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
