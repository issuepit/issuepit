import { defineStore } from 'pinia'
import type { Tenant } from '~/types'

export const useTenantsStore = defineStore('tenants', () => {
  const tenants = ref<Tenant[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchTenants() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Tenant[]>('/api/admin/tenants')
      tenants.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch tenants'
    } finally {
      loading.value = false
    }
  }

  async function createTenant(payload: { name: string; hostname: string; provisionDatabase: boolean }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Tenant>('/api/admin/tenants', payload)
      tenants.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create tenant'
    } finally {
      loading.value = false
    }
  }

  async function updateTenant(id: string, payload: { name: string; hostname: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Tenant>(`/api/admin/tenants/${id}`, payload)
      const idx = tenants.value.findIndex(t => t.id === id)
      if (idx !== -1) tenants.value[idx] = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update tenant'
    } finally {
      loading.value = false
    }
  }

  async function deleteTenant(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/admin/tenants/${id}`)
      tenants.value = tenants.value.filter(t => t.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete tenant'
    } finally {
      loading.value = false
    }
  }

  return {
    tenants,
    loading,
    error,
    fetchTenants,
    createTenant,
    updateTenant,
    deleteTenant
  }
})
