import { defineStore } from 'pinia'

export interface AdminUser {
  id: string
  username: string
  email: string
  isAdmin: boolean
  hasPassword: boolean
  createdAt: string
}

export const useUsersStore = defineStore('users', () => {
  const users = ref<AdminUser[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchUsers() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<AdminUser[]>('/api/admin/users')
      users.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch users'
    } finally {
      loading.value = false
    }
  }

  async function createUser(payload: { username: string; email?: string; password?: string; isAdmin?: boolean }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<AdminUser>('/api/admin/users', payload)
      users.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create user'
    } finally {
      loading.value = false
    }
  }

  async function updateUser(id: string, payload: { username?: string; email?: string; password?: string; isAdmin?: boolean }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<AdminUser>(`/api/admin/users/${id}`, payload)
      const idx = users.value.findIndex(u => u.id === id)
      if (idx !== -1) users.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update user'
    } finally {
      loading.value = false
    }
  }

  async function deleteUser(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/admin/users/${id}`)
      users.value = users.value.filter(u => u.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete user'
    } finally {
      loading.value = false
    }
  }

  return {
    users,
    loading,
    error,
    fetchUsers,
    createUser,
    updateUser,
    deleteUser,
  }
})
