<template>
  <div class="p-8 max-w-3xl">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentAgent" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="store.currentAgent">
      <!-- Header -->
      <div class="flex items-center justify-between gap-3 mb-6">
        <PageBreadcrumb :items="[
          { label: 'Agents', to: '/agents', icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
          { label: 'Modes', to: '/agents', icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
          { label: store.currentAgent.name, to: `/agents/${store.currentAgent.id}`, icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
        ]" />
        <button @click="store.toggleAgent(store.currentAgent.id, !store.currentAgent.isActive)"
          :class="store.currentAgent.isActive ? 'text-yellow-400 hover:text-yellow-300 border-yellow-900/40' : 'text-green-400 hover:text-green-300 border-green-900/40'"
          class="text-sm px-3 py-1.5 rounded-md border hover:bg-gray-800 transition-colors shrink-0">
          {{ store.currentAgent.isActive ? 'Deactivate' : 'Activate' }}
        </button>
      </div>

      <ErrorBox :error="store.error" />

      <!-- Agent Settings Form -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 mb-6">
        <h2 class="text-base font-semibold text-white mb-5">Agent Mode Settings</h2>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-4">
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
              <input v-model="form.name" type="text" placeholder="Agent name"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Runner</label>
              <select v-model="form.runnerType"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="opt in runnerOptions" :key="String(opt.value)" :value="opt.value">{{ opt.label }}</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Model</label>
              <input v-model="form.model" type="text" placeholder="anthropic/claude-opus-4-5"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <!-- Agent Type (opencode only): primary agents are directly interacted with; subagents are invoked by primary agents -->
            <div v-if="form.runnerType === RunnerTypeEnum.OpenCode" class="col-span-2">
              <div class="flex items-center gap-2 mb-1.5">
                <label class="block text-sm font-medium text-gray-300">Agent Type</label>
                <a href="https://opencode.ai/docs/agents#types" target="_blank" rel="noopener"
                  class="text-xs text-indigo-400 hover:text-indigo-300 transition-colors">
                  opencode docs ↗
                </a>
              </div>
              <select v-model="form.agentType"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="opt in agentTypeOptions" :key="String(opt.value)" :value="opt.value">{{ opt.label }}</option>
              </select>
              <p class="text-xs text-gray-500 mt-1">
                <strong class="text-gray-400">Primary</strong> — main agent, user interacts directly (Tab to switch). &nbsp;
                <strong class="text-gray-400">Subagent</strong> — invoked by primary agents or via @ mention.
              </p>
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Docker Image</label>
              <input v-model="form.dockerImage" type="text" placeholder="ghcr.io/org/agent:latest"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">System Prompt</label>
              <textarea v-model="form.systemPrompt" rows="5" placeholder="You are a helpful agent that..."
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
            </div>
          </div>
          <div class="flex justify-end pt-2">
            <button @click="saveSettings" :disabled="saving"
              class="px-5 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : 'Save Settings' }}
            </button>
          </div>
        </div>
      </div>

      <!-- MCP Servers -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 mb-6">
        <div class="flex items-center justify-between mb-1">
          <h2 class="text-base font-semibold text-white">MCP Servers</h2>
          <NuxtLink to="/config/mcp-servers" class="text-xs text-gray-500 hover:text-gray-300 transition-colors">
            Manage MCP Servers →
          </NuxtLink>
        </div>
        <p class="text-sm text-gray-500 mb-5">Link MCP servers to give this agent access to external tools.</p>

        <!-- Built-in IssuePit MCP (always available) -->
        <div class="flex items-center gap-3 bg-indigo-950/30 rounded-lg px-4 py-3 border border-indigo-800/40 mb-3">
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-white">IssuePit MCP</span>
              <span class="text-xs bg-indigo-900/60 text-indigo-300 px-1.5 py-0.5 rounded-full">Built-in · Auto-linked</span>
            </div>
            <p class="text-xs text-gray-500 mt-0.5">Built-in MCP server with full IssuePit API access (issues, projects, tasks, CI/CD).</p>
            <a :href="mcpBase" target="_blank" rel="noopener"
              class="text-xs text-green-400 font-mono mt-0.5 block hover:text-green-300 transition-colors truncate">
              {{ mcpBase }}/mcp
            </a>
          </div>
          <NuxtLink to="/config/mcp-playground"
            class="text-xs text-indigo-400 hover:text-indigo-300 px-3 py-1.5 rounded-md border border-indigo-900/40 hover:bg-indigo-900/20 transition-colors shrink-0">
            Playground
          </NuxtLink>
        </div>

        <div v-if="mcpStore.loading" class="text-sm text-gray-500">Loading MCP servers…</div>
        <div v-else-if="!mcpStore.mcpServers.length" class="text-sm text-gray-600">
          No additional MCP servers configured. Add them in
          <NuxtLink to="/config/mcp-servers" class="text-brand-400 hover:text-brand-300">Configuration → MCP Servers</NuxtLink>.
        </div>
        <div v-else class="space-y-2">
          <div v-for="server in mcpStore.mcpServers" :key="server.id"
            class="flex items-center gap-3 bg-gray-800/60 rounded-lg px-4 py-3 border transition-colors"
            :class="isLinked(server.id) ? 'border-indigo-700/50' : 'border-gray-700/50'">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <NuxtLink to="/config/mcp-servers" class="text-sm font-medium text-white hover:text-brand-300 transition-colors">{{ server.name }}</NuxtLink>
                <span v-if="isLinked(server.id)" class="text-xs bg-indigo-900/40 text-indigo-400 px-1.5 py-0.5 rounded-full">Linked</span>
              </div>
              <p v-if="server.description" class="text-xs text-gray-500 mt-0.5 truncate">{{ server.description }}</p>
              <p class="text-xs text-gray-600 font-mono mt-0.5 truncate">{{ server.url }}</p>
              <!-- Tools from this MCP server -->
              <div v-if="isLinked(server.id) && linkedServerTools(server.id).length" class="flex flex-wrap gap-1 mt-2">
                <span v-for="tool in linkedServerTools(server.id).slice(0, 5)" :key="tool"
                  class="text-xs bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded font-mono">{{ tool }}</span>
                <span v-if="linkedServerTools(server.id).length > 5" class="text-xs text-gray-500">
                  +{{ linkedServerTools(server.id).length - 5 }} more
                </span>
              </div>
            </div>
            <button v-if="isLinked(server.id)"
              @click="unlinkServer(server.id)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors shrink-0">
              Unlink
            </button>
            <button v-else
              @click="linkServer(server.id)"
              class="text-xs text-brand-400 hover:text-brand-300 px-3 py-1.5 rounded-md border border-brand-900/30 hover:bg-brand-900/20 transition-colors shrink-0">
              Link
            </button>
          </div>
        </div>
      </div>

      <!-- Allowed Tools -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
        <h2 class="text-base font-semibold text-white mb-1">Allowed Tools</h2>
        <p class="text-sm text-gray-500 mb-4">
          Restrict which tools the agent can invoke. Leave empty to allow all tools from linked MCP servers.
        </p>
        <!-- Hint from linked MCP servers -->
        <div v-if="allLinkedTools.length" class="mb-4">
          <p class="text-xs text-gray-500 mb-2">Available tools from linked MCP servers (click to add):</p>
          <div class="flex flex-wrap gap-1.5">
            <button v-for="tool in allLinkedTools" :key="tool"
              @click="toggleTool(tool)"
              :class="selectedTools.includes(tool) ? 'bg-blue-900/50 text-blue-200 border-blue-700/50' : 'bg-gray-800 text-gray-400 border-gray-700 hover:border-gray-500 hover:text-gray-200'"
              class="text-xs px-2 py-1 rounded border font-mono transition-colors">
              {{ tool }}
            </button>
          </div>
        </div>
        <textarea v-model="toolsInput" rows="3"
          placeholder="read_file, write_file, execute_command (comma-separated; empty = all tools)"
          class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
        <div class="flex justify-end mt-3">
          <button @click="saveTools" :disabled="savingTools"
            class="px-5 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
            {{ savingTools ? 'Saving…' : 'Save Tools' }}
          </button>
        </div>
      </div>

      <!-- Nested Agents (child agents configured for this agent) -->
      <div v-if="store.currentAgent?.childAgents?.length" class="bg-gray-900 border border-gray-800 rounded-xl p-6 mt-6">
        <div class="flex items-center justify-between mb-1">
          <h2 class="text-base font-semibold text-white">Nested Agents</h2>
          <a href="https://opencode.ai/docs/agents" target="_blank" rel="noopener"
            class="text-xs text-indigo-400 hover:text-indigo-300 transition-colors">
            opencode agent docs ↗
          </a>
        </div>
        <p class="text-sm text-gray-500 mb-4">
          These agents are injected as opencode subagents or primary agents into this agent's session.
          Configure their type in each agent's settings.
        </p>
        <div class="space-y-2">
          <div v-for="child in store.currentAgent.childAgents" :key="child.id"
            class="flex items-center gap-3 bg-gray-800/60 rounded-lg px-4 py-3 border border-gray-700/50">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <NuxtLink :to="`/agents/${child.id}`" class="text-sm font-medium text-white hover:text-brand-300 transition-colors">{{ child.name }}</NuxtLink>
                <span v-if="child.agentType != null"
                  :class="child.agentType === 1 ? 'bg-violet-900/40 text-violet-300' : 'bg-teal-900/40 text-teal-300'"
                  class="text-xs px-1.5 py-0.5 rounded-full">
                  {{ child.agentType === 1 ? 'primary' : 'subagent' }}
                </span>
                <span v-else class="text-xs bg-gray-800 text-gray-500 px-1.5 py-0.5 rounded-full">type not set</span>
                <span :class="child.isActive ? 'text-green-400' : 'text-gray-500'" class="text-xs">
                  {{ child.isActive ? '● active' : '○ inactive' }}
                </span>
              </div>
              <p v-if="child.model" class="text-xs text-gray-500 font-mono mt-0.5">{{ child.model }}</p>
            </div>
            <NuxtLink :to="`/agents/${child.id}`"
              class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors shrink-0">
              Edit
            </NuxtLink>
          </div>
        </div>
      </div>
    </template>

    <div v-else-if="!store.loading" class="text-center py-20 text-gray-500">Agent not found.</div>
  </div>
</template>

<script setup lang="ts">
import { useAgentsStore } from '~/stores/agents'
import { useMcpServersStore } from '~/stores/mcp-servers'
import type { RunnerType, OpenCodeAgentType } from '~/types'
import { RunnerTypeLabels, RunnerType as RunnerTypeEnum, OpenCodeAgentTypeLabels } from '~/types'

const route = useRoute()
const store = useAgentsStore()
const mcpStore = useMcpServersStore()
const config = useRuntimeConfig()
const mcpBase = config.public.mcpBase as string

const saving = ref(false)
const savingTools = ref(false)
const toolsInput = ref('')

const form = reactive({
  name: '',
  systemPrompt: '',
  dockerImage: '',
  isActive: true,
  runnerType: null as RunnerType | null,
  model: '',
  agentType: null as OpenCodeAgentType | null,
})

const runnerOptions = [
  { value: null, label: '— None (use container entrypoint)' },
  ...Object.entries(RunnerTypeLabels).map(([k, v]) => ({ value: Number(k) as RunnerType, label: v }))
]

const agentTypeOptions = [
  { value: null, label: '— Not set (use opencode default)' },
  ...Object.entries(OpenCodeAgentTypeLabels).map(([k, v]) => ({ value: Number(k) as OpenCodeAgentType, label: v }))
]

function parseTools(value: string | string[] | undefined): string[] {
  if (!value) return []
  if (Array.isArray(value)) return value
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed : []
  } catch (e) {
    console.warn('Failed to parse allowedTools:', e)
    return []
  }
}

const selectedTools = computed(() => {
  return toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
})

const allLinkedTools = computed(() => {
  if (!store.currentAgent?.linkedMcpServers) return []
  const tools = new Set<string>()
  for (const s of store.currentAgent.linkedMcpServers) {
    parseTools(s.allowedTools).forEach(t => tools.add(t))
  }
  return [...tools]
})

function isLinked(mcpServerId: string): boolean {
  return store.currentAgent?.linkedMcpServers?.some(s => s.id === mcpServerId) ?? false
}

function linkedServerTools(mcpServerId: string): string[] {
  const server = store.currentAgent?.linkedMcpServers?.find(s => s.id === mcpServerId)
  return server ? parseTools(server.allowedTools) : []
}

function toggleTool(tool: string) {
  const current = toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
  const idx = current.indexOf(tool)
  if (idx === -1) {
    current.push(tool)
  } else {
    current.splice(idx, 1)
  }
  toolsInput.value = current.join(', ')
}

function loadForm() {
  const agent = store.currentAgent
  if (!agent) return
  form.name = agent.name
  form.systemPrompt = agent.systemPrompt
  form.dockerImage = agent.dockerImage
  form.isActive = agent.isActive
  form.runnerType = agent.runnerType ?? null
  form.model = agent.model ?? ''
  form.agentType = agent.agentType ?? null
  toolsInput.value = parseTools(agent.allowedTools).join(', ')
}

function buildPayload(allowedTools: string[]) {
  return {
    name: form.name,
    systemPrompt: form.systemPrompt,
    dockerImage: form.dockerImage,
    isActive: form.isActive,
    runnerType: form.runnerType ?? undefined,
    model: form.model || undefined,
    agentType: form.agentType ?? undefined,
    allowedTools: JSON.stringify(allowedTools),
  }
}

async function linkServer(mcpServerId: string) {
  await store.linkMcpServer(store.currentAgent!.id, mcpServerId)
}

async function unlinkServer(mcpServerId: string) {
  await store.unlinkMcpServer(store.currentAgent!.id, mcpServerId)
}

async function saveSettings() {
  if (!store.currentAgent || !form.name) return
  saving.value = true
  try {
    const allowedTools = toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
    await store.updateAgent(store.currentAgent.id, buildPayload(allowedTools))
  } finally {
    saving.value = false
  }
}

async function saveTools() {
  if (!store.currentAgent) return
  savingTools.value = true
  try {
    const allowedTools = toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
    await store.updateAgent(store.currentAgent.id, buildPayload(allowedTools))
  } finally {
    savingTools.value = false
  }
}

onMounted(async () => {
  const id = route.params.id as string
  await Promise.all([
    store.fetchAgent(id),
    mcpStore.fetchMcpServers(),
  ])
  loadForm()
})

watch(() => store.currentAgent, () => loadForm())
</script>
