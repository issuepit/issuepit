import { defineStore } from 'pinia'
import type { MergeRequest } from '~/types'

export const useMergeRequestsStore = defineStore('mergeRequests', () => {
  const mergeRequests = ref<MergeRequest[]>([])
  const currentMr = ref<MergeRequest | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchMergeRequests(projectId: string, status?: string) {
    loading.value = true
    error.value = null
    try {
      const url = status
        ? `/api/projects/${projectId}/merge-requests?status=${status}`
        : `/api/projects/${projectId}/merge-requests`
      mergeRequests.value = await api.get<MergeRequest[]>(url)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch merge requests'
    } finally {
      loading.value = false
    }
  }

  async function fetchMergeRequest(projectId: string, id: string) {
    loading.value = true
    error.value = null
    try {
      currentMr.value = await api.get<MergeRequest>(`/api/projects/${projectId}/merge-requests/${id}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch merge request'
    } finally {
      loading.value = false
    }
  }

  async function createMergeRequest(
    projectId: string,
    title: string,
    sourceBranch: string,
    targetBranch: string,
    autoMerge: boolean,
  ) {
    loading.value = true
    error.value = null
    try {
      const mr = await api.post<MergeRequest>(`/api/projects/${projectId}/merge-requests`, {
        title,
        sourceBranch,
        targetBranch,
        autoMerge,
      })
      mergeRequests.value.unshift(mr)
      return mr
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create merge request'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function updateMergeRequest(
    projectId: string,
    id: string,
    patch: { title?: string; autoMerge?: boolean },
  ) {
    error.value = null
    try {
      const mr = await api.put<MergeRequest>(`/api/projects/${projectId}/merge-requests/${id}`, patch)
      const idx = mergeRequests.value.findIndex(m => m.id === id)
      if (idx !== -1) mergeRequests.value[idx] = mr
      if (currentMr.value?.id === id) currentMr.value = mr
      return mr
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update merge request'
      throw e
    }
  }

  async function closeMergeRequest(projectId: string, id: string) {
    error.value = null
    try {
      const mr = await api.post<MergeRequest>(`/api/projects/${projectId}/merge-requests/${id}/close`, {})
      const idx = mergeRequests.value.findIndex(m => m.id === id)
      if (idx !== -1) mergeRequests.value[idx] = mr
      if (currentMr.value?.id === id) currentMr.value = mr
      return mr
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to close merge request'
      throw e
    }
  }

  async function mergeMergeRequest(projectId: string, id: string) {
    error.value = null
    try {
      const mr = await api.post<MergeRequest>(`/api/projects/${projectId}/merge-requests/${id}/merge`, {})
      const idx = mergeRequests.value.findIndex(m => m.id === id)
      if (idx !== -1) mergeRequests.value[idx] = mr
      if (currentMr.value?.id === id) currentMr.value = mr
      return mr
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to merge merge request'
      throw e
    }
  }

  return {
    mergeRequests,
    currentMr,
    loading,
    error,
    fetchMergeRequests,
    fetchMergeRequest,
    createMergeRequest,
    updateMergeRequest,
    closeMergeRequest,
    mergeMergeRequest,
  }
})
