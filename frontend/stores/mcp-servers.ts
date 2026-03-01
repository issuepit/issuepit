import { defineStore } from 'pinia'
import type { McpServer, McpServerTemplate } from '~/types'

export const useMcpServersStore = defineStore('mcp-servers', () => {
  const { get, post, put, del } = useApi()

  const mcpServers = ref<McpServer[]>([])
  const templates = ref<McpServerTemplate[]>([])
  const loading = ref(false)
  const templatesLoading = ref(false)
  const error = ref<string | null>(null)

  async function fetchMcpServers() {
    loading.value = true
    error.value = null
    try {
      const data = await get<McpServer[]>('/api/mcp-servers')
      mcpServers.value = data.map(s => ({
        ...s,
        allowedTools: typeof s.allowedTools === 'string' ? JSON.parse(s.allowedTools) : s.allowedTools,
      }))
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch MCP servers'
    } finally {
      loading.value = false
    }
  }

  async function fetchTemplates() {
    templatesLoading.value = true
    try {
      const data = await get<McpServerTemplate[]>('/api/mcp-servers/templates')
      templates.value = data.map(t => ({
        ...t,
        allowedTools: typeof t.allowedTools === 'string' ? JSON.parse(t.allowedTools as unknown as string) : t.allowedTools,
      }))
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch MCP templates'
    } finally {
      templatesLoading.value = false
    }
  }

  async function createMcpServer(payload: {
    orgId: string
    name: string
    description?: string
    url: string
    allowedTools: string[]
    configuration: string
  }) {
    loading.value = true
    error.value = null
    try {
      const body = { ...payload, allowedTools: JSON.stringify(payload.allowedTools) }
      const data = await post<McpServer>('/api/mcp-servers', body)
      const normalized = { ...data, allowedTools: typeof data.allowedTools === 'string' ? JSON.parse(data.allowedTools) : data.allowedTools }
      mcpServers.value.push(normalized)
      return normalized
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create MCP server'
    } finally {
      loading.value = false
    }
  }

  async function updateMcpServer(id: string, payload: {
    orgId: string
    name: string
    description?: string
    url: string
    allowedTools: string[]
    configuration: string
  }) {
    loading.value = true
    error.value = null
    try {
      const body = { ...payload, allowedTools: JSON.stringify(payload.allowedTools) }
      const data = await put<McpServer>(`/api/mcp-servers/${id}`, body)
      const normalized = { ...data, allowedTools: typeof data.allowedTools === 'string' ? JSON.parse(data.allowedTools) : data.allowedTools }
      const idx = mcpServers.value.findIndex(s => s.id === id)
      if (idx !== -1) mcpServers.value[idx] = normalized
      return normalized
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update MCP server'
    } finally {
      loading.value = false
    }
  }

  async function deleteMcpServer(id: string) {
    loading.value = true
    error.value = null
    try {
      await del(`/api/mcp-servers/${id}`)
      mcpServers.value = mcpServers.value.filter(s => s.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete MCP server'
    } finally {
      loading.value = false
    }
  }

  async function linkToAgent(agentId: string, mcpServerId: string) {
    await post(`/api/agents/${agentId}/mcp-servers/${mcpServerId}`, {})
  }

  async function unlinkFromAgent(agentId: string, mcpServerId: string) {
    await del(`/api/agents/${agentId}/mcp-servers/${mcpServerId}`)
  }

  return {
    mcpServers,
    templates,
    loading,
    templatesLoading,
    error,
    fetchMcpServers,
    fetchTemplates,
    createMcpServer,
    updateMcpServer,
    deleteMcpServer,
    linkToAgent,
    unlinkFromAgent,
  }
})
