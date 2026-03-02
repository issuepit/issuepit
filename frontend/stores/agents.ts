import { defineStore } from 'pinia'
import type { Agent } from '~/types'

export const useAgentsStore = defineStore('agents', () => {
  const agents = ref<Agent[]>([])
  const currentAgent = ref<Agent | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchAgents() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Agent[]>('/api/agents')
      agents.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agents'
    } finally {
      loading.value = false
    }
  }

  async function fetchAgent(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Agent>(`/api/agents/${id}`)
      currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch agent'
    } finally {
      loading.value = false
    }
  }

  async function createAgent(payload: Partial<Agent>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Agent>('/api/agents', payload)
      agents.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create agent'
    } finally {
      loading.value = false
    }
  }

  async function updateAgent(id: string, payload: Partial<Agent>) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Agent>(`/api/agents/${id}`, payload)
      const idx = agents.value.findIndex(a => a.id === id)
      if (idx !== -1) agents.value[idx] = data
      if (currentAgent.value?.id === id) currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update agent'
    } finally {
      loading.value = false
    }
  }

  async function deleteAgent(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/agents/${id}`)
      agents.value = agents.value.filter(a => a.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete agent'
    } finally {
      loading.value = false
    }
  }

  async function toggleAgent(id: string, isActive: boolean) {
    loading.value = true
    error.value = null
    try {
      const data = await api.patch<Agent>(`/api/agents/${id}/active`, { isActive })
      const idx = agents.value.findIndex(a => a.id === id)
      if (idx !== -1) agents.value[idx] = data
      if (currentAgent.value?.id === id) currentAgent.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to toggle agent'
    } finally {
      loading.value = false
    }
  }

  async function linkMcpServer(agentId: string, mcpServerId: string) {
    error.value = null
    try {
      await api.post(`/api/agents/${agentId}/mcp-servers/${mcpServerId}`, {})
      if (currentAgent.value?.id === agentId) await fetchAgent(agentId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link MCP server'
    }
  }

  async function unlinkMcpServer(agentId: string, mcpServerId: string) {
    error.value = null
    try {
      await api.del(`/api/agents/${agentId}/mcp-servers/${mcpServerId}`)
      if (currentAgent.value?.id === agentId) await fetchAgent(agentId)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unlink MCP server'
    }
  }

  // --- Project agents ---

  async function fetchProjectAgents(projectId: string) {
    error.value = null
    try {
      return await api.get<import('~/types').AgentProject[]>(`/api/projects/${projectId}/agents`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch project agents'
      return []
    }
  }

  async function linkAgentToProject(projectId: string, agentId: string) {
    error.value = null
    try {
      await api.post(`/api/projects/${projectId}/agents/${agentId}`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link agent to project'
      throw e
    }
  }

  async function unlinkAgentFromProject(projectId: string, agentId: string) {
    error.value = null
    try {
      await api.del(`/api/projects/${projectId}/agents/${agentId}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unlink agent from project'
      throw e
    }
  }

  async function setProjectAgentActive(projectId: string, agentId: string, isActive: boolean) {
    error.value = null
    try {
      await api.patch(`/api/projects/${projectId}/agents/${agentId}/active`, { isActive })
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update agent status'
      throw e
    }
  }

  // --- Org agents ---

  async function fetchOrgAgents(orgId: string) {
    error.value = null
    try {
      return await api.get<import('~/types').AgentOrg[]>(`/api/orgs/${orgId}/agents`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch org agents'
      return []
    }
  }

  async function linkAgentToOrg(orgId: string, agentId: string) {
    error.value = null
    try {
      await api.post(`/api/orgs/${orgId}/agents/${agentId}`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link agent to org'
      throw e
    }
  }

  async function unlinkAgentFromOrg(orgId: string, agentId: string) {
    error.value = null
    try {
      await api.del(`/api/orgs/${orgId}/agents/${agentId}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unlink agent from org'
      throw e
    }
  }

  return {
    agents,
    currentAgent,
    loading,
    error,
    fetchAgents,
    fetchAgent,
    createAgent,
    updateAgent,
    deleteAgent,
    toggleAgent,
    linkMcpServer,
    unlinkMcpServer,
    fetchProjectAgents,
    linkAgentToProject,
    unlinkAgentFromProject,
    setProjectAgentActive,
    fetchOrgAgents,
    linkAgentToOrg,
    unlinkAgentFromOrg,
  }
})
