import { defineStore } from 'pinia'
import type { Project } from '~/types'

export const useProjectsStore = defineStore('projects', () => {
  const projects = ref<Project[]>([])
  const currentProject = ref<Project | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchProjects() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Project[]>('/api/projects')
      projects.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch projects'
    } finally {
      loading.value = false
    }
  }

  async function fetchProject(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Project>(`/api/projects/${id}`)
      currentProject.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch project'
    } finally {
      loading.value = false
    }
  }

  async function createProject(payload: Partial<Project>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Project>('/api/projects', payload)
      projects.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create project'
    } finally {
      loading.value = false
    }
  }

  async function updateProject(id: string, payload: Partial<Project>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Project>(`/api/projects/${id}`, payload)
      const idx = projects.value.findIndex(p => p.id === id)
      if (idx !== -1) projects.value[idx] = data
      if (currentProject.value?.id === id) currentProject.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update project'
    } finally {
      loading.value = false
    }
  }

  async function deleteProject(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/projects/${id}`)
      projects.value = projects.value.filter(p => p.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete project'
    } finally {
      loading.value = false
    }
  }

  return {
    projects,
    currentProject,
    loading,
    error,
    fetchProjects,
    fetchProject,
    createProject,
    updateProject,
    deleteProject
  }
})
