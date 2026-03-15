<template>
  <div class="p-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Agents', to: '/agents', icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
          { label: 'MCP Servers', to: '/config/mcp-servers', icon: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01' },
        ]" />
        <p class="text-sm text-gray-400 mt-0.5">Register external MCP servers that agents can connect to.</p>
      </div>
      <div class="flex gap-2">
        <button
          class="px-4 py-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium rounded-lg transition-colors border border-gray-700"
          @click="showTemplates = true"
        >
          Templates
        </button>
        <button
          class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
          @click="openCreate"
        >
          Add MCP Server
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.mcpServers.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No MCP servers configured yet.</p>
      <div class="flex gap-3 justify-center mt-3">
        <button class="text-gray-400 hover:text-gray-200 text-sm border border-gray-700 px-3 py-1.5 rounded-lg" @click="showTemplates = true">Browse Templates</button>
        <button class="text-brand-400 hover:text-brand-300 text-sm" @click="openCreate">Add your first MCP server →</button>
      </div>
    </div>

    <!-- Servers list -->
    <div v-else class="space-y-3">
      <div
        v-for="server in store.mcpServers"
        :key="server.id"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors cursor-pointer"
        @click="openDetail(server)"
      >
        <div class="flex items-start justify-between gap-4">
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2 flex-wrap">
              <h3 class="font-semibold text-white">{{ server.name }}</h3>
              <span v-if="server.linkedAgents?.length" class="text-xs bg-indigo-900/40 text-indigo-400 px-2 py-0.5 rounded-full">
                {{ server.linkedAgents.length }} agent{{ server.linkedAgents.length !== 1 ? 's' : '' }}
              </span>
              <span v-if="server.linkedProjects?.length" class="text-xs bg-blue-900/40 text-blue-400 px-2 py-0.5 rounded-full">
                {{ server.linkedProjects.length }} project{{ server.linkedProjects.length !== 1 ? 's' : '' }}
              </span>
              <span v-if="server.secrets?.length" class="text-xs bg-yellow-900/40 text-yellow-400 px-2 py-0.5 rounded-full">
                {{ server.secrets.length }} secret{{ server.secrets.length !== 1 ? 's' : '' }}
              </span>
            </div>
            <p v-if="server.description" class="text-sm text-gray-400 mt-0.5">{{ server.description }}</p>
            <code class="text-xs text-green-300 font-mono mt-1 block">{{ server.url }}</code>
          </div>
          <div class="flex gap-2 shrink-0">
            <button
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
              @click.stop="confirmDelete(server.id, server.name)"
            >
              Delete
            </button>
          </div>
        </div>

        <!-- Allowed Tools & Meta -->
        <div class="mt-3 flex flex-wrap gap-3 text-xs text-gray-500">
          <span>Created {{ formatDate(server.createdAt) }}</span>
          <span v-if="allowedToolsList(server).length">
            Tools:
            <span v-for="t in allowedToolsList(server).slice(0, 3)" :key="t" class="ml-1 bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded font-mono">{{ t }}</span>
            <span v-if="allowedToolsList(server).length > 3" class="ml-1">+{{ allowedToolsList(server).length - 3 }} more</span>
          </span>
          <span v-else class="text-gray-600">All tools allowed</span>
        </div>
      </div>
    </div>

    <!-- ── Create / Edit modal ── -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-lg p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h3 class="text-lg font-semibold text-white mb-5">{{ editingId ? 'Edit MCP Server' : 'Add MCP Server' }}</h3>
        <form class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name <span class="text-red-400">*</span></label>
            <input v-model="form.name" type="text" required placeholder="e.g. GitHub MCP"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Description</label>
            <input v-model="form.description" type="text" placeholder="Optional description"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">URL <span class="text-red-400">*</span></label>
            <input v-model="form.url" type="text" required placeholder="https://mcp.example.com or http://localhost:3000"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500 font-mono" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">
              Allowed Tools
              <span class="text-gray-600 font-normal">(comma-separated; empty = all tools)</span>
            </label>
            <input v-model="toolsInput" type="text" placeholder="list_issues, create_issue, search_repositories"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500 font-mono" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Configuration (JSON)</label>
            <textarea v-model="form.configuration" rows="3" placeholder="{}"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500 font-mono resize-none"></textarea>
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : (editingId ? 'Update' : 'Save') }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="closeModal">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- ── Detail / Manage modal ── -->
    <div v-if="detailServer" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-2xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between mb-5">
          <div class="min-w-0">
            <h3 class="text-lg font-semibold text-white">{{ detailServer.name }}</h3>
            <code class="text-xs text-green-300 font-mono truncate block mt-0.5">{{ detailServer.url }}</code>
          </div>
          <button class="text-gray-500 hover:text-gray-300 ml-4 shrink-0" @click="detailServer = null">✕</button>
        </div>

        <!-- Tabs -->
        <div class="flex gap-1 border-b border-gray-800 mb-5">
          <button v-for="tab in detailTabs" :key="tab"
            class="px-3 py-1.5 text-sm font-medium transition-colors rounded-t"
            :class="detailTab === tab ? 'text-white border-b-2 border-brand-500 -mb-px' : 'text-gray-400 hover:text-gray-200'"
            @click="detailTab = tab">
            {{ tab }}
          </button>
        </div>

        <!-- Info tab -->
        <div v-if="detailTab === 'Info'">
          <dl class="space-y-3 text-sm">
            <div class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">Name</dt>
              <dd class="text-white">{{ detailServer.name }}</dd>
            </div>
            <div v-if="detailServer.description" class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">Description</dt>
              <dd class="text-gray-300">{{ detailServer.description }}</dd>
            </div>
            <div class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">URL</dt>
              <dd><code class="text-green-300 font-mono text-xs">{{ detailServer.url }}</code></dd>
            </div>
            <div class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">Allowed Tools</dt>
              <dd>
                <span v-if="allowedToolsList(detailServer).length" class="flex flex-wrap gap-1">
                  <span v-for="t in allowedToolsList(detailServer)" :key="t"
                    class="bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded text-xs font-mono">{{ t }}</span>
                </span>
                <span v-else class="text-gray-600">All tools allowed</span>
              </dd>
            </div>
            <div class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">Configuration</dt>
              <dd><code class="text-gray-300 font-mono text-xs">{{ detailServer.configuration }}</code></dd>
            </div>
            <div class="flex gap-4">
              <dt class="w-32 text-gray-500 shrink-0">Created</dt>
              <dd class="text-gray-400">{{ formatDate(detailServer.createdAt) }}</dd>
            </div>
          </dl>
          <div class="mt-5 pt-5 border-t border-gray-800 flex gap-3">
            <button
              class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
              @click="startEditFromDetail"
            >
              Edit Server
            </button>
            <NuxtLink to="/config/mcp-playground"
              class="px-4 py-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium rounded-lg transition-colors border border-gray-700"
              @click="detailServer = null"
            >
              Open Playground
            </NuxtLink>
          </div>
        </div>

        <!-- Secrets tab -->
        <div v-if="detailTab === 'Secrets'">
          <p class="text-sm text-gray-400 mb-4">Environment secrets injected when an agent uses this MCP server. Scope secrets to specific projects, orgs, or agents for fine-grained control.</p>
          <div v-if="detailServer.secrets?.length" class="space-y-2 mb-4">
            <div v-for="s in detailServer.secrets" :key="s.id"
              class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
              <div class="flex items-center gap-3 min-w-0">
                <code class="text-sm text-yellow-300 font-mono">{{ s.key }}</code>
                <span v-if="s.scope && s.scope !== 'Global'" class="text-xs px-1.5 py-0.5 rounded-full"
                  :class="{
                    'bg-blue-900/40 text-blue-400': s.scope === 'Project',
                    'bg-purple-900/40 text-purple-400': s.scope === 'Org',
                    'bg-indigo-900/40 text-indigo-400': s.scope === 'Agent',
                  }">{{ s.scope }}</span>
              </div>
              <div class="flex items-center gap-3 text-xs text-gray-500 shrink-0">
                <span>{{ formatDate(s.createdAt) }}</span>
                <button class="text-red-400 hover:text-red-300" @click="removeSecret(s.id)">Delete</button>
              </div>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 mb-4">No secrets configured.</div>
          <form class="space-y-2" @submit.prevent="addSecret">
            <div class="flex gap-2">
              <input v-model="newSecretKey" placeholder="KEY_NAME" required
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
              <input v-model="newSecretValue" placeholder="secret value" type="password" required
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
            </div>
            <div class="flex gap-2">
              <select v-model="newSecretScope"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
                <option value="Global">Global (all contexts)</option>
                <option value="Project">Project-scoped</option>
                <option value="Org">Org-scoped</option>
                <option value="Agent">Agent-scoped</option>
              </select>
              <select v-if="newSecretScope === 'Project'" v-model="newSecretScopeId"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
                <option value="">Select project…</option>
                <option v-for="p in projectsStore.projects" :key="p.id" :value="p.id">{{ p.name }}</option>
              </select>
              <select v-else-if="newSecretScope === 'Org'" v-model="newSecretScopeId"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
                <option value="">Select org…</option>
                <option v-for="o in orgsStore.orgs" :key="o.id" :value="o.id">{{ o.name }}</option>
              </select>
              <select v-else-if="newSecretScope === 'Agent'" v-model="newSecretScopeId"
                class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
                <option value="">Select agent…</option>
                <option v-for="a in agentsStore.agents" :key="a.id" :value="a.id">{{ a.name }}</option>
              </select>
              <button type="submit" :disabled="savingSecret || (newSecretScope !== 'Global' && !newSecretScopeId)"
                class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors whitespace-nowrap">
                {{ savingSecret ? '…' : 'Add Secret' }}
              </button>
            </div>
          </form>
        </div>

        <!-- Linked Agents tab -->
        <div v-if="detailTab === 'Linked Agents'">
          <p class="text-sm text-gray-400 mb-4">Agents that have this MCP server configured.</p>
          <div v-if="detailServer.linkedAgents?.length" class="space-y-2 mb-4">
            <div v-for="a in detailServer.linkedAgents" :key="a.agentId"
              class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
              <div class="flex items-center gap-3">
                <div class="w-7 h-7 bg-indigo-900/40 rounded-md flex items-center justify-center shrink-0">
                  <svg class="w-3.5 h-3.5 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
                  </svg>
                </div>
                <NuxtLink :to="`/agents/${a.agentId}`" class="text-sm text-white hover:text-brand-400 transition-colors" @click="detailServer = null">{{ a.name }}</NuxtLink>
              </div>
              <button class="text-xs text-red-400 hover:text-red-300" @click="unlinkAgent(a.agentId)">Unlink</button>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 mb-4">No agents linked.</div>
          <div class="flex gap-2">
            <select v-model="selectedAgentId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="" disabled>Select an agent…</option>
              <option
                v-for="agent in availableAgents"
                :key="agent.id"
                :value="agent.id"
              >{{ agent.name }}</option>
            </select>
            <button :disabled="!selectedAgentId || savingAgent"
              class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors whitespace-nowrap"
              @click="linkAgent">
              {{ savingAgent ? '…' : 'Link' }}
            </button>
          </div>
        </div>

        <!-- Linked Projects tab -->
        <div v-if="detailTab === 'Projects'">
          <p class="text-sm text-gray-400 mb-4">Projects this MCP server is available to.</p>
          <div v-if="detailServer.linkedProjects?.length" class="space-y-2 mb-4">
            <div v-for="p in detailServer.linkedProjects" :key="p.projectId"
              class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
              <NuxtLink :to="`/projects/${p.projectId}/settings`" class="text-sm text-white hover:text-brand-400 transition-colors" @click="detailServer = null">{{ p.name }}</NuxtLink>
              <button class="text-xs text-red-400 hover:text-red-300" @click="unlinkProject(p.projectId)">Unlink</button>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 mb-4">No projects linked.</div>
          <div class="flex gap-2">
            <select v-model="selectedProjectId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="" disabled>Select a project…</option>
              <option
                v-for="proj in availableProjects"
                :key="proj.id"
                :value="proj.id"
              >{{ proj.name }}</option>
            </select>
            <button :disabled="!selectedProjectId || savingProject"
              class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors whitespace-nowrap"
              @click="linkProject">
              {{ savingProject ? '…' : 'Link' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- ── Template store modal ── -->
    <div v-if="showTemplates" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-2xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between mb-5">
          <div>
            <h3 class="text-lg font-semibold text-white">MCP Server Templates</h3>
            <p class="text-sm text-gray-400 mt-0.5">Pre-configured templates for popular MCP servers.</p>
          </div>
          <button class="text-gray-500 hover:text-gray-300" @click="showTemplates = false">✕</button>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div
            v-for="tpl in templates"
            :key="tpl.name"
            class="bg-gray-800 border border-gray-700 rounded-lg p-4 hover:border-gray-500 transition-colors cursor-pointer group"
            @click="applyTemplate(tpl)"
          >
            <div class="flex items-center gap-3 mb-2">
              <span class="text-xl">{{ tpl.icon }}</span>
              <h4 class="font-medium text-white text-sm">{{ tpl.name }}</h4>
            </div>
            <p class="text-xs text-gray-400 mb-3">{{ tpl.description }}</p>
            <code class="text-xs text-green-300 font-mono block truncate">{{ tpl.url }}</code>
            <button
              class="mt-3 text-xs text-brand-400 group-hover:text-brand-300 transition-colors"
            >
              Use template →
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useMcpServersStore } from '~/stores/mcp-servers'
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'
import { useAgentsStore } from '~/stores/agents'
import type { McpServer } from '~/types'

const store = useMcpServersStore()
const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()
const agentsStore = useAgentsStore()

onMounted(async () => {
  await store.fetchMcpServers()
  await Promise.all([
    projectsStore.fetchProjects(),
    orgsStore.fetchOrgs(),
    agentsStore.fetchAgents(),
  ])
})

// ── List helpers ──────────────────────────────────────────────────────────────

function allowedToolsList(server: McpServer): string[] {
  try {
    const parsed = typeof server.allowedTools === 'string' ? JSON.parse(server.allowedTools) : server.allowedTools
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

// ── Templates ────────────────────────────────────────────────────────────────

const showTemplates = ref(false)

const templates = [
  {
    icon: '🐙',
    name: 'GitHub',
    description: 'Manage issues, pull requests, repositories, and more via the GitHub API.',
    url: 'https://api.githubcopilot.com/mcp/',
    allowedTools: 'list_issues,create_issue,get_issue,create_pull_request,search_repositories,list_branches',
    configuration: '{}',
  },
  {
    icon: '🎭',
    name: 'Playwright',
    description: 'Browser automation and web scraping with Playwright.',
    url: 'http://localhost:3001',
    allowedTools: 'navigate,click,fill,screenshot,evaluate',
    configuration: '{}',
  },
  {
    icon: '🔍',
    name: 'Brave Search',
    description: 'Web search powered by the Brave Search API.',
    url: 'https://mcp.bravesearch.com',
    allowedTools: 'web_search,local_search',
    configuration: '{}',
  },
  {
    icon: '📁',
    name: 'Filesystem',
    description: 'Read and write files on the local filesystem.',
    url: 'http://localhost:3002',
    allowedTools: 'read_file,write_file,list_directory,create_directory',
    configuration: '{}',
  },
  {
    icon: '🗄️',
    name: 'PostgreSQL',
    description: 'Query and manage a PostgreSQL database.',
    url: 'http://localhost:3003',
    allowedTools: 'query,list_tables,describe_table',
    configuration: '{"readOnly": true}',
  },
  {
    icon: '🦊',
    name: 'GitLab',
    description: 'Manage GitLab projects, issues, and merge requests.',
    url: 'https://gitlab.com/-/mcp',
    allowedTools: 'list_issues,create_issue,list_merge_requests,create_merge_request',
    configuration: '{}',
  },
]

function applyTemplate(tpl: typeof templates[0]) {
  editingId.value = null
  form.name = tpl.name
  form.description = tpl.description
  form.url = tpl.url
  form.configuration = tpl.configuration
  toolsInput.value = tpl.allowedTools
  showTemplates.value = false
  showModal.value = true
}

// ── Create / Edit ─────────────────────────────────────────────────────────────

const showModal = ref(false)
const editingId = ref<string | null>(null)
const saving = ref(false)
const toolsInput = ref('')

const form = reactive({
  name: '',
  description: '',
  url: '',
  configuration: '{}',
  orgId: '', // resolved from active org
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', description: '', url: '', configuration: '{}', orgId: '' })
  toolsInput.value = ''
  showModal.value = true
}

function openEdit(server: McpServer) {
  editingId.value = server.id
  form.name = server.name
  form.description = server.description ?? ''
  form.url = server.url
  form.configuration = server.configuration
  form.orgId = server.orgId
  toolsInput.value = allowedToolsList(server).join(', ')
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  editingId.value = null
}

async function handleSubmit() {
  saving.value = true
  try {
    const allowedTools = toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
    if (editingId.value) {
      await store.updateMcpServer(editingId.value, {
        name: form.name,
        description: form.description || undefined,
        url: form.url,
        configuration: form.configuration,
        allowedTools,
      })
    } else {
      await store.createMcpServer({
        orgId: form.orgId,
        name: form.name,
        description: form.description || undefined,
        url: form.url,
        configuration: form.configuration,
        allowedTools,
      })
    }
    closeModal()
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete MCP server "${name}"?`)) store.deleteMcpServer(id)
}

// ── Detail / Manage ──────────────────────────────────────────────────────────

const detailServer = ref<McpServer | null>(null)
const detailTabs = ['Info', 'Secrets', 'Linked Agents', 'Projects'] as const
const detailTab = ref<typeof detailTabs[number]>('Info')

function openDetail(server: McpServer) {
  detailServer.value = server
  detailTab.value = 'Info'
}

function startEditFromDetail() {
  if (!detailServer.value) return
  const server = detailServer.value
  detailServer.value = null
  openEdit(server)
}

// Refresh detail panel when store updates
watch(() => store.mcpServers, (servers) => {
  if (detailServer.value) {
    detailServer.value = servers.find(s => s.id === detailServer.value!.id) ?? null
  }
}, { deep: true })

// -- Secrets --

const newSecretKey = ref('')
const newSecretValue = ref('')
const newSecretScope = ref('Global')
const newSecretScopeId = ref('')
const savingSecret = ref(false)

watch(newSecretScope, () => { newSecretScopeId.value = '' })

async function addSecret() {
  if (!detailServer.value) return
  savingSecret.value = true
  try {
    await store.addSecret(detailServer.value.id, newSecretKey.value, newSecretValue.value, newSecretScope.value, newSecretScopeId.value || undefined)
    newSecretKey.value = ''
    newSecretValue.value = ''
    newSecretScope.value = 'Global'
    newSecretScopeId.value = ''
  } finally {
    savingSecret.value = false
  }
}

async function removeSecret(secretId: string) {
  if (!detailServer.value) return
  if (confirm('Delete this secret?')) {
    await store.deleteSecret(detailServer.value.id, secretId)
  }
}

// -- Project links --

const selectedProjectId = ref('')
const savingProject = ref(false)

const availableProjects = computed(() => {
  const linked = detailServer.value?.linkedProjects?.map(p => p.projectId) ?? []
  return projectsStore.projects.filter(p => !linked.includes(p.id))
})

async function linkProject() {
  if (!detailServer.value || !selectedProjectId.value) return
  savingProject.value = true
  try {
    await store.linkProject(detailServer.value.id, selectedProjectId.value)
    selectedProjectId.value = ''
  } finally {
    savingProject.value = false
  }
}

async function unlinkProject(projectId: string) {
  if (!detailServer.value) return
  await store.unlinkProject(detailServer.value.id, projectId)
}

// -- Agent links --

const selectedAgentId = ref('')
const savingAgent = ref(false)

const availableAgents = computed(() => {
  const linked = detailServer.value?.linkedAgents?.map(a => a.agentId) ?? []
  return agentsStore.agents.filter(a => !linked.includes(a.id))
})

async function linkAgent() {
  if (!detailServer.value || !selectedAgentId.value) return
  savingAgent.value = true
  try {
    await store.linkAgent(detailServer.value.id, selectedAgentId.value)
    selectedAgentId.value = ''
  } finally {
    savingAgent.value = false
  }
}

async function unlinkAgent(agentId: string) {
  if (!detailServer.value) return
  await store.unlinkAgent(detailServer.value.id, agentId)
}
</script>

