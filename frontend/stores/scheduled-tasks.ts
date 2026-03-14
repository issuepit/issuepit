import { defineStore } from 'pinia'
import type { ScheduledTaskRun, ScheduledTaskProject } from '~/types'

export const useScheduledTasksStore = defineStore('scheduledTasks', () => {
  const runs = ref<ScheduledTaskRun[]>([])
  const projects = ref<ScheduledTaskProject[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchRuns(opts: {
    projectId?: string
    status?: string
    take?: number
  } = {}) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (opts.projectId) params.set('projectId', opts.projectId)
      if (opts.status) params.set('status', opts.status)
      if (opts.take) params.set('take', String(opts.take))
      const qs = params.toString()
      const data = await api.get<ScheduledTaskRun[]>(
        `/api/scheduled-tasks/runs${qs ? `?${qs}` : ''}`,
      )
      runs.value = data
    }
    catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch runs'
    }
    finally {
      loading.value = false
    }
  }

  async function fetchProjects() {
    try {
      const data = await api.get<ScheduledTaskProject[]>('/api/scheduled-tasks/projects')
      projects.value = data
    }
    catch {
      // non-critical — filters just won't show project names
    }
  }

  function $reset() {
    runs.value = []
    projects.value = []
    loading.value = false
    error.value = null
  }

  return {
    runs,
    projects,
    loading,
    error,
    fetchRuns,
    fetchProjects,
    $reset,
  }
})
