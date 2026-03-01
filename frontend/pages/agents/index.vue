<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <h1 class="text-2xl font-bold text-white">Agents</h1>
        <p class="text-gray-400 mt-1">{{ store.agents.length }} agents configured</p>
      </div>
      <button @click="openCreate"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Agent
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Agents Grid -->
    <div v-else class="space-y-3">
      <div v-for="agent in store.agents" :key="agent.id"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors">
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 bg-indigo-900/40 rounded-lg flex items-center justify-center shrink-0">
              <svg class="w-5 h-5 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
              </svg>
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h3 class="font-semibold text-white">{{ agent.name }}</h3>
                <span :class="agent.isActive ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
                  class="text-xs px-1.5 py-0.5 rounded-full">
                  {{ agent.isActive ? 'Active' : 'Inactive' }}
                </span>
              </div>
              <p v-if="agent.description" class="text-sm text-gray-400 mt-0.5">{{ agent.description }}</p>
            </div>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <button @click="store.toggleAgent(agent.id, !agent.isActive)"
              :class="agent.isActive ? 'text-yellow-400 hover:text-yellow-300' : 'text-green-400 hover:text-green-300'"
              class="text-xs px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              {{ agent.isActive ? 'Deactivate' : 'Activate' }}
            </button>
            <button @click="openEdit(agent)"
              class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              Edit
            </button>
            <button @click="store.deleteAgent(agent.id)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors">
              Delete
            </button>
          </div>
        </div>

        <!-- Details -->
        <div class="mt-4 grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Docker Image</p>
            <code class="text-xs text-green-300 font-mono">{{ agent.dockerImage || '—' }}</code>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">Allowed Tools</p>
            <div class="flex flex-wrap gap-1">
              <span v-for="tool in (agent.allowedTools ?? []).slice(0, 4)" :key="tool"
                class="text-xs bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded font-mono">{{ tool }}</span>
              <span v-if="(agent.allowedTools ?? []).length > 4" class="text-xs text-gray-500">
                +{{ agent.allowedTools.length - 4 }} more
              </span>
              <span v-if="!agent.allowedTools?.length" class="text-xs text-gray-600">None</span>
            </div>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">System Prompt</p>
            <p class="text-xs text-gray-400 line-clamp-2">{{ agent.systemPrompt || '—' }}</p>
          </div>
        </div>

        <!-- MCP Servers -->
        <div class="mt-3 bg-gray-800/40 rounded-lg p-3">
          <div class="flex items-center justify-between mb-2">
            <p class="text-xs text-gray-500 uppercase tracking-wide">MCP Servers</p>
            <button @click="openMcpManager(agent)"
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
              Manage
            </button>
          </div>
          <div v-if="linkedMcpServers(agent).length" class="flex flex-wrap gap-1">
            <span v-for="srv in linkedMcpServers(agent)" :key="srv.id"
              class="text-xs bg-indigo-900/30 text-indigo-300 px-1.5 py-0.5 rounded">{{ srv.name }}</span>
          </div>
          <p v-else class="text-xs text-gray-600">No MCP servers linked</p>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="!store.loading && store.agents.length === 0"
        class="flex flex-col items-center justify-center py-20 text-center">
        <div class="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
          <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No agents yet</p>
        <p class="text-gray-600 text-sm mt-1">Create your first agent to automate tasks</p>
        <button @click="openCreate"
          class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          Create Agent
        </button>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div v-if="showModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingId ? 'Edit Agent' : 'Create Agent' }}</h2>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
              <input v-model="form.name" type="text" placeholder="Agent name"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
              <input v-model="form.description" type="text" placeholder="Optional description"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Docker Image</label>
              <input v-model="form.dockerImage" type="text" placeholder="ghcr.io/org/agent:latest"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">System Prompt</label>
              <textarea v-model="form.systemPrompt" rows="4"
                placeholder="You are a helpful agent that..."
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Allowed Tools
                <span class="text-gray-500 font-normal">(comma-separated)</span>
              </label>
              <input v-model="toolsInput" type="text" placeholder="read_file, write_file, execute_command"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitModal"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ editingId ? 'Update' : 'Create' }}
          </button>
          <button @click="showModal = false; resetForm()"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- MCP Server Manager Modal -->
    <div v-if="mcpManagerAgent" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h2 class="text-lg font-bold text-white mb-1">MCP Servers</h2>
        <p class="text-sm text-gray-400 mb-5">Manage MCP servers for <span class="text-white font-medium">{{ mcpManagerAgent.name }}</span></p>

        <!-- Available MCP Servers -->
        <div v-if="mcpStore.loading" class="text-gray-500 text-sm">Loading…</div>
        <div v-else-if="!mcpStore.mcpServers.length" class="text-gray-500 text-sm">
          No MCP servers configured. <NuxtLink to="/config/mcp-servers" class="text-brand-400 hover:text-brand-300">Add one →</NuxtLink>
        </div>
        <div v-else class="space-y-2">
          <div v-for="srv in mcpStore.mcpServers" :key="srv.id"
            class="flex items-center justify-between rounded-lg border border-gray-800 bg-gray-800/40 px-3 py-2.5">
            <div>
              <p class="text-sm font-medium text-white">{{ srv.name }}</p>
              <p v-if="srv.description" class="text-xs text-gray-500 mt-0.5">{{ srv.description }}</p>
            </div>
            <button
              v-if="isLinked(mcpManagerAgent, srv.id)"
              class="text-xs text-red-400 hover:text-red-300 px-2 py-1 rounded border border-red-900/30 transition-colors"
              @click="unlinkMcp(mcpManagerAgent!, srv.id)"
            >
              Unlink
            </button>
            <button
              v-else
              class="text-xs text-brand-400 hover:text-brand-300 px-2 py-1 rounded border border-brand-900/30 transition-colors"
              @click="linkMcp(mcpManagerAgent!, srv.id)"
            >
              Link
            </button>
          </div>
        </div>

        <div class="mt-6">
          <button @click="mcpManagerAgent = null"
            class="w-full bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Done
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAgentsStore } from '~/stores/agents'
import { useMcpServersStore } from '~/stores/mcp-servers'
import type { Agent, AgentMcpServerLink, McpServer } from '~/types'

const store = useAgentsStore()
const mcpStore = useMcpServersStore()
const showModal = ref(false)
const editingId = ref<string | null>(null)
const toolsInput = ref('')
const mcpManagerAgent = ref<Agent | null>(null)

// Tracks which MCP server IDs are linked to the agent being managed in the modal
const linkedMcpIds = ref<Set<string>>(new Set())

const form = reactive({
  name: '',
  description: '',
  dockerImage: '',
  systemPrompt: '',
  isActive: true
})

onMounted(() => {
  store.fetchAgents()
  mcpStore.fetchMcpServers()
})

function openCreate() {
  editingId.value = null
  resetForm()
  showModal.value = true
}

function openEdit(agent: Agent) {
  editingId.value = agent.id
  form.name = agent.name
  form.description = agent.description ?? ''
  form.dockerImage = agent.dockerImage
  form.systemPrompt = agent.systemPrompt
  form.isActive = agent.isActive
  toolsInput.value = (agent.allowedTools ?? []).join(', ')
  showModal.value = true
}

async function submitModal() {
  if (!form.name) return
  const payload = {
    ...form,
    allowedTools: toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
  }
  if (editingId.value) {
    await store.updateAgent(editingId.value, payload)
  } else {
    await store.createAgent(payload)
  }
  showModal.value = false
  resetForm()
}

function resetForm() {
  editingId.value = null
  form.name = ''
  form.description = ''
  form.dockerImage = ''
  form.systemPrompt = ''
  form.isActive = true
  toolsInput.value = ''
}

function linkedMcpServers(agent: Agent): McpServer[] {
  const ids = new Set((agent.agentMcpServers ?? []).map((l: AgentMcpServerLink) => l.mcpServerId))
  return mcpStore.mcpServers.filter(s => ids.has(s.id))
}

function isLinked(agent: Agent, mcpServerId: string): boolean {
  return linkedMcpIds.value.has(mcpServerId)
}

async function openMcpManager(agent: Agent) {
  mcpManagerAgent.value = agent
  // Rebuild the linked IDs set from the agent's current links
  linkedMcpIds.value = new Set((agent.agentMcpServers ?? []).map((l: AgentMcpServerLink) => l.mcpServerId))
}

async function linkMcp(agent: Agent, mcpServerId: string) {
  await mcpStore.linkToAgent(agent.id, mcpServerId)
  linkedMcpIds.value.add(mcpServerId)
  // Update agent's agentMcpServers list so the card reflects the change immediately
  if (!agent.agentMcpServers) agent.agentMcpServers = []
  agent.agentMcpServers.push({ agentId: agent.id, mcpServerId })
}

async function unlinkMcp(agent: Agent, mcpServerId: string) {
  await mcpStore.unlinkFromAgent(agent.id, mcpServerId)
  linkedMcpIds.value.delete(mcpServerId)
  if (agent.agentMcpServers) {
    agent.agentMcpServers = agent.agentMcpServers.filter((l: AgentMcpServerLink) => l.mcpServerId !== mcpServerId)
  }
}
</script>
