<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <PageBreadcrumb :items="[
        { label: 'Admin', to: '/admin/users', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4' },
        { label: 'Users', to: '/admin/users', icon: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z' },
      ]" />
      <button
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New User
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Users table -->
    <div v-else-if="store.users.length" class="rounded-xl border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Username</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Email</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Role</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Auth</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="user in store.users" :key="user.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ user.username }}</td>
            <td class="px-4 py-3 text-gray-300 text-xs">{{ user.email }}</td>
            <td class="px-4 py-3">
              <span
                v-if="user.isAdmin"
                class="inline-flex items-center text-xs bg-brand-900/30 text-brand-400 px-2 py-0.5 rounded-full"
              >
                Admin
              </span>
              <span v-else class="text-xs text-gray-500">Member</span>
            </td>
            <td class="px-4 py-3">
              <span
                v-if="user.hasPassword"
                class="inline-flex items-center gap-1 text-xs bg-green-900/30 text-green-400 px-2 py-0.5 rounded-full"
              >
                Password
              </span>
              <span v-else class="text-xs text-gray-500">SSO only</span>
            </td>
            <td class="px-4 py-3 text-gray-400">{{ formatDate(user.createdAt) }}</td>
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-2">
                <button
                  class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                  @click="openEdit(user)"
                >
                  Edit
                </button>
                <button
                  class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                  @click="confirmDelete(user.id, user.username)"
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
      <p class="text-gray-400 font-medium">No users yet</p>
      <p class="text-gray-600 text-sm mt-1">Create the first user to get started</p>
      <button
        class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
        @click="openCreate"
      >
        Create User
      </button>
    </div>

    <!-- Create / Edit Modal -->
    <div v-if="showModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingId ? 'Edit User' : 'New User' }}</h2>
        <form class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Username</label>
            <input
              v-model="form.username"
              type="text"
              required
              placeholder="username"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Email</label>
            <input
              v-model="form.email"
              type="email"
              placeholder="user@example.com"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">
              Password <span v-if="editingId" class="text-gray-500">(leave blank to keep current)</span>
            </label>
            <input
              v-model="form.password"
              type="password"
              :required="!editingId"
              placeholder="••••••••"
              autocomplete="new-password"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div class="flex items-center gap-3 rounded-lg bg-gray-800/50 border border-gray-700 p-3">
            <input
              id="is-admin"
              v-model="form.isAdmin"
              type="checkbox"
              class="mt-0.5 w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
            />
            <label for="is-admin" class="text-sm text-gray-300 cursor-pointer">
              <span class="font-medium">System administrator</span>
              <span class="block text-xs text-gray-500 mt-0.5">Grants access to admin panels and user management.</span>
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
import { useUsersStore, type AdminUser } from '~/stores/users'

const store = useUsersStore()

onMounted(() => store.fetchUsers())

const showModal = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)

const form = reactive({
  username: '',
  email: '',
  password: '',
  isAdmin: false,
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { username: '', email: '', password: '', isAdmin: false })
  showModal.value = true
}

function openEdit(user: AdminUser) {
  editingId.value = user.id
  Object.assign(form, { username: user.username, email: user.email, password: '', isAdmin: user.isAdmin })
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  editingId.value = null
}

async function handleSubmit() {
  if (!form.username) return
  saving.value = true
  try {
    if (editingId.value) {
      const payload: { username?: string; email?: string; password?: string; isAdmin?: boolean } = {
        username: form.username,
        email: form.email || undefined,
        isAdmin: form.isAdmin,
      }
      if (form.password) payload.password = form.password
      await store.updateUser(editingId.value, payload)
    } else {
      await store.createUser({
        username: form.username,
        email: form.email || undefined,
        password: form.password,
        isAdmin: form.isAdmin,
      })
    }
    closeModal()
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, username: string) {
  if (confirm(`Delete user "${username}"? This cannot be undone.`)) {
    store.deleteUser(id)
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
