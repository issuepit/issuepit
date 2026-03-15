import { defineStore } from 'pinia'
import type { ProjectProperty, IssuePropertyValue } from '~/types'

export const useProjectPropertiesStore = defineStore('projectProperties', () => {
  const properties = ref<ProjectProperty[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchProperties(projectId: string) {
    loading.value = true
    error.value = null
    try {
      properties.value = await api.get<ProjectProperty[]>(`/api/projects/${projectId}/properties`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch properties'
    } finally {
      loading.value = false
    }
  }

  async function createProperty(projectId: string, payload: Omit<ProjectProperty, 'id' | 'projectId' | 'position' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const prop = await api.post<ProjectProperty>(`/api/projects/${projectId}/properties`, payload)
      properties.value.push(prop)
      return prop
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create property'
    } finally {
      loading.value = false
    }
  }

  async function updateProperty(projectId: string, propertyId: string, payload: Omit<ProjectProperty, 'id' | 'projectId' | 'position' | 'createdAt'>) {
    loading.value = true
    error.value = null
    try {
      const prop = await api.put<ProjectProperty>(`/api/projects/${projectId}/properties/${propertyId}`, payload)
      const idx = properties.value.findIndex(p => p.id === propertyId)
      if (idx !== -1) properties.value[idx] = prop
      return prop
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update property'
    } finally {
      loading.value = false
    }
  }

  async function deleteProperty(projectId: string, propertyId: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/projects/${projectId}/properties/${propertyId}`)
      properties.value = properties.value.filter(p => p.id !== propertyId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete property'
    } finally {
      loading.value = false
    }
  }

  async function fetchIssuePropertyValues(projectId: string, issueId: string) {
    try {
      return await api.get<IssuePropertyValue[]>(`/api/projects/${projectId}/issues/${issueId}/property-values`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch property values'
      return []
    }
  }

  async function setIssuePropertyValue(projectId: string, issueId: string, propertyId: string, value: string | null) {
    try {
      return await api.put<IssuePropertyValue>(`/api/projects/${projectId}/issues/${issueId}/property-values/${propertyId}`, { value })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to set property value'
    }
  }

  return {
    properties,
    loading,
    error,
    fetchProperties,
    createProperty,
    updateProperty,
    deleteProperty,
    fetchIssuePropertyValues,
    setIssuePropertyValue,
  }
})
