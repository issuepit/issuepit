/**
 * Composable for managing server-side dashboard layout templates.
 * Supports listing, saving, and deleting named layouts with three scopes:
 *  - 'user'            – personal layout for the current user
 *  - 'project_default' – default for all project members
 *  - 'shared'          – visible to all tenant members
 */

export interface DashboardLayoutTemplate {
  id: string
  name: string
  dashboardType: string
  scope: 'user' | 'project_default' | 'shared'
  projectId?: string | null
  userId?: string | null
  layoutJson: string
  createdAt: string
  updatedAt: string
}

export function useDashboardTemplates() {
  const api = useApi()

  const templates = ref<DashboardLayoutTemplate[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchTemplates(dashboardType: string, projectId?: string) {
    loading.value = true
    error.value = null
    try {
      const params: Record<string, string> = { dashboardType }
      if (projectId) params.projectId = projectId
      templates.value = await api.get<DashboardLayoutTemplate[]>('/api/dashboard/layouts', { params })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to load templates'
    } finally {
      loading.value = false
    }
  }

  async function saveTemplate(
    name: string,
    dashboardType: string,
    scope: 'user' | 'project_default' | 'shared',
    layoutJson: string,
    projectId?: string,
  ): Promise<DashboardLayoutTemplate | null> {
    loading.value = true
    error.value = null
    try {
      const result = await api.post<DashboardLayoutTemplate>('/api/dashboard/layouts', {
        name,
        dashboardType,
        scope,
        layoutJson,
        projectId: projectId ?? null,
      })
      // Refresh list
      await fetchTemplates(dashboardType, projectId)
      return result
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to save template'
      return null
    } finally {
      loading.value = false
    }
  }

  async function deleteTemplate(id: string, dashboardType: string, projectId?: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/dashboard/layouts/${id}`)
      await fetchTemplates(dashboardType, projectId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete template'
    } finally {
      loading.value = false
    }
  }

  return {
    templates,
    loading,
    error,
    fetchTemplates,
    saveTemplate,
    deleteTemplate,
  }
}
