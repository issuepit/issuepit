import { defineStore } from 'pinia'
import type { CustomProperty } from '~/types'
import { CustomPropertyType } from '~/types'

export { CustomPropertyType }

export const useCustomPropertiesStore = defineStore('customProperties', () => {
  const properties = ref<CustomProperty[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const api = useApi()

  async function fetchProperties(projectId: string) {
    loading.value = true
    error.value = null
    try {
      properties.value = await api.get<CustomProperty[]>(`/api/projects/${projectId}/custom-properties`)
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch custom properties'
    }
    finally {
      loading.value = false
    }
  }

  async function createProperty(projectId: string, data: Omit<CustomProperty, 'id' | 'projectId' | 'position' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const prop = await api.post<CustomProperty>(`/api/projects/${projectId}/custom-properties`, data)
      properties.value.push(prop)
      return prop
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create custom property'
    }
    finally {
      loading.value = false
    }
  }

  async function updateProperty(projectId: string, propertyId: string, data: Omit<CustomProperty, 'id' | 'projectId' | 'position' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const prop = await api.put<CustomProperty>(`/api/projects/${projectId}/custom-properties/${propertyId}`, data)
      const idx = properties.value.findIndex(p => p.id === propertyId)
      if (idx !== -1) properties.value[idx] = prop
      return prop
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update custom property'
    }
    finally {
      loading.value = false
    }
  }

  async function deleteProperty(projectId: string, propertyId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/projects/${projectId}/custom-properties/${propertyId}`)
      properties.value = properties.value.filter(p => p.id !== propertyId)
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete custom property'
    }
    finally {
      loading.value = false
    }
  }

  return { properties, loading, error, fetchProperties, createProperty, updateProperty, deleteProperty }
})
