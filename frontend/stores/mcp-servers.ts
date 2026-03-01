import { defineStore } from 'pinia'
import type { McpServer } from '~/types'

export const useMcpServersStore = defineStore('mcp-servers', () => {
  const { get, post, del } = useApi()

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

  async function createMcpServer(payload: { orgId: string; name: string; url: string; configuration: string }) {
    const created = await post<McpServer>('/api/mcp-servers', payload)
    mcpServers.value.push(created)
    return created
  }

  async function deleteMcpServer(id: string) {
    await del(`/api/mcp-servers/${id}`)
    mcpServers.value = mcpServers.value.filter(s => s.id !== id)
  }

  return { mcpServers, loading, fetchMcpServers, createMcpServer, deleteMcpServer }
})
