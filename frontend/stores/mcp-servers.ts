import { defineStore } from 'pinia'
import type { McpServer } from '~/types'

export const useMcpServersStore = defineStore('mcp-servers', () => {
  const { get, post, put, del } = useApi()

  const mcpServers = ref<McpServer[]>([])
  const loading = ref(false)

  async function fetchMcpServers() {
    loading.value = true
    try {
      mcpServers.value = await get<McpServer[]>('/api/mcp-servers')
    } finally {
      loading.value = false
    }
  }

  async function createMcpServer(payload: { orgId: string; name: string; description?: string; url: string; configuration: string; allowedTools?: string[] }) {
    const created = await post<McpServer>('/api/mcp-servers', {
      ...payload,
      allowedTools: JSON.stringify(payload.allowedTools ?? []),
    })
    await fetchMcpServers()
    return created
  }

  async function updateMcpServer(id: string, payload: { name: string; description?: string; url: string; configuration: string; allowedTools?: string[] }) {
    const updated = await put<McpServer>(`/api/mcp-servers/${id}`, {
      ...payload,
      allowedTools: JSON.stringify(payload.allowedTools ?? []),
    })
    await fetchMcpServers()
    return updated
  }

  async function deleteMcpServer(id: string) {
    await del(`/api/mcp-servers/${id}`)
    mcpServers.value = mcpServers.value.filter(s => s.id !== id)
  }

  // --- Secrets ---

  async function addSecret(mcpServerId: string, key: string, value: string) {
    await post(`/api/mcp-servers/${mcpServerId}/secrets`, { key, value })
    await fetchMcpServers()
  }

  async function deleteSecret(mcpServerId: string, secretId: string) {
    await del(`/api/mcp-servers/${mcpServerId}/secrets/${secretId}`)
    await fetchMcpServers()
  }

  // --- Project links ---

  async function linkProject(mcpServerId: string, projectId: string) {
    await post(`/api/mcp-servers/${mcpServerId}/projects/${projectId}`, {})
    await fetchMcpServers()
  }

  async function unlinkProject(mcpServerId: string, projectId: string) {
    await del(`/api/mcp-servers/${mcpServerId}/projects/${projectId}`)
    await fetchMcpServers()
  }

  return {
    mcpServers,
    loading,
    fetchMcpServers,
    createMcpServer,
    updateMcpServer,
    deleteMcpServer,
    addSecret,
    deleteSecret,
    linkProject,
    unlinkProject,
  }
})
