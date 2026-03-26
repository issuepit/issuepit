<template>
  <div class="p-8 h-screen flex flex-col overflow-hidden">
    <div class="flex items-start justify-between mb-6 gap-4 flex-wrap">
      <div>
        <PageBreadcrumb
:items="[
          { label: 'Agents', to: '/agents', icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
          { label: 'MCP Playground', to: '/config/mcp-playground', icon: 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z' },
        ]" />
        <p class="text-sm text-gray-400 mt-0.5">
          Interactively test tools on the built-in IssuePit MCP server.
          Endpoint: <code class="text-green-300 font-mono text-xs">{{ mcpBase }}/mcp</code>
        </p>
      </div>

      <!-- Controls: mode + permissions + reload -->
      <div class="flex items-center gap-3 flex-wrap">
        <!-- Readonly / Write mode toggle -->
        <div class="flex items-center gap-1 bg-gray-900 border border-gray-700 rounded-lg p-1 text-xs font-medium">
          <button
            class="px-3 py-1 rounded-md transition-colors"
            :class="isReadonly ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-300'"
            @click="setMode('readonly')"
          >
            Read-only
          </button>
          <button
            class="px-3 py-1 rounded-md transition-colors"
            :class="!isReadonly ? 'bg-brand-600 text-white' : 'text-gray-400 hover:text-gray-300'"
            @click="setMode('write')"
          >
            Write
          </button>
        </div>

        <!-- Permission toggles -->
        <div class="flex items-center gap-2">
          <button
            class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors"
            :class="showTodo ? 'bg-indigo-900/40 border-indigo-700 text-indigo-300' : 'bg-gray-900 border-gray-700 text-gray-500 hover:text-gray-400'"
            @click="toggleTodo"
          >
            <span class="w-2 h-2 rounded-full" :class="showTodo ? 'bg-indigo-400' : 'bg-gray-600'" />
            Todos
          </button>
          <button
            class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors"
            :class="showAdmin ? 'bg-amber-900/40 border-amber-700 text-amber-300' : 'bg-gray-900 border-gray-700 text-gray-500 hover:text-gray-400'"
            @click="toggleAdmin"
          >
            <span class="w-2 h-2 rounded-full" :class="showAdmin ? 'bg-amber-400' : 'bg-gray-600'" />
            Admin
          </button>
        </div>

        <button
          class="flex items-center gap-2 px-4 py-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium rounded-lg transition-colors border border-gray-700"
          :disabled="loadingTools"
          @click="loadTools"
        >
          <svg class="w-4 h-4" :class="loadingTools ? 'animate-spin' : ''" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          {{ loadingTools ? 'Loading…' : 'Reload Tools' }}
        </button>
      </div>
    </div>

    <!-- Error state -->
    <div v-if="toolsError" data-testid="mcp-tools-error" class="mb-4 bg-red-900/20 border border-red-900/40 rounded-lg px-4 py-3 text-sm text-red-400">
      {{ toolsError }}
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5 flex-1 min-h-0">
      <!-- Tools list grouped by topic -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 flex flex-col min-h-0">
        <h3 class="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">
          Available Tools
          <span v-if="filteredTools.length" class="ml-1 bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded-full">{{ filteredTools.length }}</span>
        </h3>
        <div v-if="loadingTools" class="space-y-2">
          <div v-for="i in 6" :key="i" class="h-9 bg-gray-800 rounded-lg animate-pulse" />
        </div>
        <div v-else-if="!filteredTools.length" data-testid="mcp-tools-empty" class="text-sm text-gray-600">
          No tools available. Make sure the MCP server is running.
        </div>
        <div v-else data-testid="mcp-tools-list" class="space-y-4 flex-1 min-h-0 overflow-y-auto">
          <div v-for="group in groupedTools" :key="group.topic">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs font-semibold uppercase tracking-wider" :class="group.color">{{ group.label }}</span>
              <span class="text-xs text-gray-700 bg-gray-800 px-1 rounded">{{ group.tools.length }}</span>
            </div>
            <ul class="space-y-0.5">
              <li
                v-for="tool in group.tools"
                :key="tool.name"
                class="cursor-pointer px-3 py-1.5 rounded-lg text-xs font-mono transition-colors"
                :class="selectedTool?.name === tool.name
                  ? 'bg-brand-600 text-white'
                  : 'text-gray-300 hover:bg-gray-800'"
                @click="selectTool(tool)"
              >
                {{ tool.name }}
              </li>
            </ul>
          </div>
        </div>
      </div>

      <!-- Tool form & result -->
      <div class="lg:col-span-2 space-y-4">
        <!-- Tool details + params -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
          <template v-if="selectedTool">
            <div class="mb-4">
              <h3 class="font-semibold text-white text-sm">{{ selectedTool.name }}</h3>
              <p v-if="selectedTool.description" class="text-sm text-gray-400 mt-1">{{ selectedTool.description }}</p>
            </div>

            <div v-if="toolParams.length" class="space-y-3 mb-5">
              <div v-for="param in toolParams" :key="param.name">
                <label class="block text-xs font-medium text-gray-400 mb-1">
                  {{ param.name }}
                  <span v-if="param.required" class="text-red-400 ml-0.5">*</span>
                  <span v-if="param.description" class="text-gray-600 font-normal ml-1">— {{ param.description }}</span>
                </label>

                <!-- Project autocomplete for projectId parameters -->
                <div v-if="isProjectIdParam(param.name)" class="relative">
                  <div class="flex gap-2">
                    <div class="relative flex-1">
                      <input
                        v-model="projectSearch"
                        type="text"
                        placeholder="Search projects by name…"
                        class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
                        @focus="projectDropdownOpen = true"
                        @blur="onProjectSearchBlur"
                        @input="projectDropdownOpen = true"
                      >
                      <div
                        v-if="projectDropdownOpen && filteredProjectOptions.length"
                        class="absolute z-10 top-full left-0 right-0 mt-1 bg-gray-800 border border-gray-700 rounded-lg shadow-lg max-h-48 overflow-y-auto"
                      >
                        <button
                          v-for="proj in filteredProjectOptions"
                          :key="proj.id"
                          class="w-full text-left px-3 py-2 text-sm hover:bg-gray-700 text-gray-200"
                          @mousedown.prevent="selectProject(proj, param.name)"
                        >
                          <span class="font-medium">{{ proj.name }}</span>
                          <span class="text-gray-500 text-xs ml-2">{{ proj.slug }}</span>
                        </button>
                      </div>
                    </div>
                    <input
                      v-model="paramValues[param.name]"
                      type="text"
                      placeholder="GUID"
                      class="w-52 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500"
                    >
                  </div>
                  <p v-if="paramValues[param.name]" class="text-xs text-gray-600 mt-0.5">Using ID: {{ paramValues[param.name] }}</p>
                </div>

                <!-- Issue autocomplete for issueId parameters -->
                <div v-else-if="isIssueIdParam(param.name)" class="relative">
                  <div class="flex gap-2">
                    <div class="relative flex-1">
                      <input
                        v-model="issueSearch"
                        type="text"
                        placeholder="Search issues by title or number…"
                        class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
                        @focus="issueDropdownOpen = true"
                        @blur="onIssueSearchBlur"
                        @input="issueDropdownOpen = true"
                      >
                      <div
                        v-if="issueDropdownOpen && filteredIssueOptions.length"
                        class="absolute z-10 top-full left-0 right-0 mt-1 bg-gray-800 border border-gray-700 rounded-lg shadow-lg max-h-48 overflow-y-auto"
                      >
                        <button
                          v-for="issue in filteredIssueOptions"
                          :key="issue.id"
                          class="w-full text-left px-3 py-2 text-sm hover:bg-gray-700 text-gray-200"
                          @mousedown.prevent="selectIssue(issue, param.name)"
                        >
                          <span class="text-gray-500 text-xs mr-1.5">#{{ issue.number }}</span>
                          <span class="font-medium">{{ issue.title }}</span>
                        </button>
                      </div>
                    </div>
                    <input
                      v-model="paramValues[param.name]"
                      type="text"
                      placeholder="GUID"
                      class="w-52 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500"
                    >
                  </div>
                  <p v-if="paramValues[param.name]" class="text-xs text-gray-600 mt-0.5">Using ID: {{ paramValues[param.name] }}</p>
                </div>

                <select
                  v-else-if="param.type === 'boolean'"
                  v-model="paramValues[param.name]"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
                >
                  <option value="">—</option>
                  <option value="true">true</option>
                  <option value="false">false</option>
                </select>
                <textarea
                  v-else-if="param.type === 'object' || param.type === 'array'"
                  v-model="paramValues[param.name]"
                  rows="3"
                  :placeholder="`${param.type === 'object' ? '{}' : '[]'}`"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500 resize-none"
                />
                <input
                  v-else
                  v-model="paramValues[param.name]"
                  type="text"
                  :placeholder="param.type ?? 'string'"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
                >
              </div>
            </div>
            <div v-else class="text-sm text-gray-600 mb-4 italic">No parameters required.</div>

            <div class="flex items-center gap-3">
              <button
                class="px-5 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
                :disabled="calling"
                @click="callTool"
              >
                {{ calling ? 'Calling…' : 'Call Tool' }}
              </button>
              <span v-if="callStatus" class="text-xs" :class="callStatus.ok ? 'text-green-400' : 'text-red-400'">
                {{ callStatus.message }}
              </span>
            </div>
          </template>
          <div v-else class="text-sm text-gray-600 italic">
            Select a tool from the list to get started.
          </div>
        </div>

        <!-- Result -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-xs font-semibold text-gray-400 uppercase tracking-wide">Result</h3>
            <button
              class="text-xs px-2 py-0.5 rounded border transition-colors"
              :class="showPretty ? 'bg-brand-900/40 border-brand-700 text-brand-300' : 'bg-gray-800 border-gray-700 text-gray-400 hover:text-gray-300'"
              @click="showPretty = !showPretty"
            >
              {{ showPretty ? 'Pretty' : 'Raw' }}
            </button>
          </div>
          <pre class="bg-gray-950 border border-gray-800 rounded-lg p-4 text-xs font-mono text-green-300 min-h-32 overflow-x-auto whitespace-pre-wrap break-all">{{ showPretty ? prettyResultText : resultText }}</pre>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import type { Issue } from '~/types'

interface McpTool {
  name: string
  description?: string
  inputSchema?: {
    properties?: Record<string, { type?: string; description?: string }>
    required?: string[]
  }
}

interface ToolParam {
  name: string
  type?: string
  description?: string
  required: boolean
}

interface ToolGroup {
  topic: string
  label: string
  color: string
  tools: McpTool[]
}

const TOOL_GROUPS: Array<{ topic: string; label: string; color: string; prefixes: string[] }> = [
  { topic: 'issues',   label: 'Issues',        color: 'text-blue-400',    prefixes: ['list_issues', 'get_issue', 'list_sub_issues', 'create_issue', 'update_issue', 'delete_issue'] },
  { topic: 'tasks',    label: 'Tasks',          color: 'text-cyan-400',    prefixes: ['list_issue_tasks', 'create_issue_task', 'update_issue_task', 'delete_issue_task'] },
  { topic: 'projects', label: 'Projects',       color: 'text-emerald-400', prefixes: ['list_projects', 'get_project', 'create_project', 'update_project', 'delete_project'] },
  { topic: 'todos',    label: 'Todos',          color: 'text-indigo-400',  prefixes: ['todo_'] },
  { topic: 'cicd',     label: 'CI/CD & Tests',  color: 'text-orange-400',  prefixes: ['list_ci', 'get_ci', 'get_test', 'compare_test'] },
  { topic: 'orgs',     label: 'Organizations',  color: 'text-purple-400',  prefixes: ['list_organizations', 'get_organization'] },
  { topic: 'repo',     label: 'Repository',     color: 'text-yellow-400',  prefixes: ['list_repo', 'get_repo'] },
]

const config = useRuntimeConfig()
const api = useApi()
const mcpBase = config.public.mcpBase as string
const mcpUrl = `${mcpBase}/mcp`

const projectsStore = useProjectsStore()

let reqId = 1
let sessionId: string | null = null

// Shared constants
const DROPDOWN_CLOSE_DELAY = 150
const MAX_JSON_PARSE_DEPTH = 5

// Tools whose `id` parameter is a project ID
const PROJECT_ID_TOOLS = new Set(['get_project', 'create_project', 'update_project', 'delete_project'])
// Tools whose `id` parameter is an issue ID
const ISSUE_ID_TOOLS = new Set(['get_issue', 'update_issue', 'delete_issue'])

// Mode and permission state
const isReadonly = ref(false)
const showTodo = ref(true)
const showAdmin = ref(false)

// Project autocomplete state
const projectSearch = ref('')
const projectDropdownOpen = ref(false)

// Issue autocomplete state
const issueSearch = ref('')
const issueDropdownOpen = ref(false)
const allIssues = ref<Pick<Issue, 'id' | 'title' | 'number'>[]>([])

async function mcpInitialize() {
  const body = JSON.stringify({ jsonrpc: '2.0', id: reqId++, method: 'initialize', params: { protocolVersion: '2025-11-25', capabilities: {}, clientInfo: { name: 'playground', version: '1.0' } } })
  const res = await fetch(mcpUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' },
    body,
  })
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`)
  sessionId = res.headers.get('Mcp-Session-Id')
  const ct = res.headers.get('content-type') ?? ''
  if (ct.includes('text/event-stream')) {
    const text = await res.text()
    const dataLine = text.split('\n').find((l: string) => l.startsWith('data:'))
    if (!dataLine) throw new Error('No data in SSE response')
    return JSON.parse(dataLine.slice(5).trim())
  }
  return res.json()
}

async function mcpRequest(method: string, params: Record<string, unknown>) {
  if (!sessionId) await mcpInitialize()
  const body = JSON.stringify({ jsonrpc: '2.0', id: reqId++, method, params })
  const headers: Record<string, string> = { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream' }
  if (sessionId) headers['Mcp-Session-Id'] = sessionId
  const res = await fetch(mcpUrl, { method: 'POST', headers, body })
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`)
  const ct = res.headers.get('content-type') ?? ''
  if (ct.includes('text/event-stream')) {
    const text = await res.text()
    const dataLine = text.split('\n').find((l: string) => l.startsWith('data:'))
    if (!dataLine) throw new Error('No data in SSE response')
    return JSON.parse(dataLine.slice(5).trim())
  }
  return res.json()
}

const tools = ref<McpTool[]>([])
const loadingTools = ref(false)
const toolsError = ref<string | null>(null)
const selectedTool = ref<McpTool | null>(null)
const paramValues = ref<Record<string, string>>({})
const calling = ref(false)
const callStatus = ref<{ ok: boolean; message: string } | null>(null)
const resultText = ref('// result will appear here')
const showPretty = ref(true)

// Write tools: create, update, delete operations
const WRITE_PREFIXES = ['create_', 'update_', 'delete_', 'todo_create', 'todo_update', 'todo_delete']
// Admin tools: create/delete project
const ADMIN_TOOLS = new Set(['create_project', 'delete_project'])
// Todo tools prefix
const TODO_PREFIX = 'todo_'

// --- Pretty-print result ---

function deepParseJson(val: unknown, depth = 0): unknown {
  if (depth > MAX_JSON_PARSE_DEPTH) return val
  if (typeof val === 'string') {
    const trimmed = val.trim()
    if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
      try { return deepParseJson(JSON.parse(trimmed), depth + 1) } catch { /* not JSON */ }
    }
  }
  if (Array.isArray(val)) return val.map(v => deepParseJson(v, depth))
  if (val !== null && typeof val === 'object') {
    return Object.fromEntries(
      Object.entries(val as Record<string, unknown>).map(([k, v]) => [k, deepParseJson(v, depth)]),
    )
  }
  return val
}

const prettyResultText = computed(() => {
  const text = resultText.value
  if (!text || text === '// result will appear here') return text
  try {
    return JSON.stringify(deepParseJson(JSON.parse(text)), null, 2)
  } catch {
    return text
  }
})

function isWriteTool(name: string): boolean {
  return WRITE_PREFIXES.some(p => name.startsWith(p))
}

/** The tool list filtered by the current mode and permission toggles */
const filteredTools = computed<McpTool[]>(() => {
  return tools.value.filter(t => {
    // Readonly mode: hide write tools
    if (isReadonly.value && isWriteTool(t.name)) return false
    // Todo permission: hide todo tools when disabled
    if (!showTodo.value && t.name.startsWith(TODO_PREFIX)) return false
    // Admin permission: hide admin tools when disabled
    if (!showAdmin.value && ADMIN_TOOLS.has(t.name)) return false
    return true
  })
})

/** Tools grouped by topic for display; longest-prefix match wins so that e.g.
 *  `create_issue_task` (matched by tasks prefix) beats `create_issue` (issues prefix). */
const groupedTools = computed<ToolGroup[]>(() => {
  const toolGroupIdx = new Map<string, number>()

  for (const tool of filteredTools.value) {
    let bestIdx = -1
    let bestLen = -1
    for (let i = 0; i < TOOL_GROUPS.length; i++) {
      for (const prefix of TOOL_GROUPS[i].prefixes) {
        if ((tool.name === prefix || tool.name.startsWith(prefix)) && prefix.length > bestLen) {
          bestIdx = i
          bestLen = prefix.length
        }
      }
    }
    if (bestIdx >= 0) toolGroupIdx.set(tool.name, bestIdx)
  }

  const groups: ToolGroup[] = []
  for (let i = 0; i < TOOL_GROUPS.length; i++) {
    const group = TOOL_GROUPS[i]
    const toolsInGroup = filteredTools.value.filter(t => toolGroupIdx.get(t.name) === i)
    if (toolsInGroup.length > 0) {
      groups.push({ topic: group.topic, label: group.label, color: group.color, tools: toolsInGroup })
    }
  }

  // Catch-all for tools not matched by any group
  const others = filteredTools.value.filter(t => !toolGroupIdx.has(t.name))
  if (others.length > 0) {
    groups.push({ topic: 'other', label: 'Other', color: 'text-gray-400', tools: others })
  }

  return groups
})

const toolParams = computed<ToolParam[]>(() => {
  if (!selectedTool.value) return []
  const props = selectedTool.value.inputSchema?.properties ?? {}
  const required = selectedTool.value.inputSchema?.required ?? []
  return Object.entries(props).map(([name, schema]) => ({
    name,
    type: schema.type,
    description: schema.description,
    required: required.includes(name),
  }))
})

// --- Project autocomplete ---

const allProjects = computed(() => projectsStore.projects)

const filteredProjectOptions = computed(() => {
  const q = projectSearch.value.trim().toLowerCase()
  if (!q) return allProjects.value.slice(0, 20)
  return allProjects.value
    .filter(p => p.name.toLowerCase().includes(q) || p.slug.toLowerCase().includes(q))
    .slice(0, 20)
})

function isProjectIdParam(paramName: string): boolean {
  if (paramName === 'projectId') return true
  if (paramName === 'id' && selectedTool.value) {
    return PROJECT_ID_TOOLS.has(selectedTool.value.name)
  }
  return false
}

function selectProject(proj: { id: string; name: string; slug: string }, paramName: string) {
  paramValues.value[paramName] = proj.id
  projectSearch.value = proj.name
  projectDropdownOpen.value = false
}

function onProjectSearchBlur() {
  // Delay close to allow click on dropdown items
  setTimeout(() => { projectDropdownOpen.value = false }, DROPDOWN_CLOSE_DELAY)
}

// --- Issue autocomplete ---

const filteredIssueOptions = computed(() => {
  const q = issueSearch.value.trim().toLowerCase()
  if (!q) return allIssues.value.slice(0, 20)
  return allIssues.value
    .filter(i => i.title.toLowerCase().includes(q) || String(i.number).includes(q))
    .slice(0, 20)
})

function isIssueIdParam(paramName: string): boolean {
  if (paramName === 'issueId') return true
  if (paramName === 'id' && selectedTool.value) {
    return ISSUE_ID_TOOLS.has(selectedTool.value.name)
  }
  return false
}

function selectIssue(issue: Pick<Issue, 'id' | 'title' | 'number'>, paramName: string) {
  paramValues.value[paramName] = issue.id
  issueSearch.value = `#${issue.number} ${issue.title}`
  issueDropdownOpen.value = false
}

function onIssueSearchBlur() {
  // Delay close to allow click on dropdown items
  setTimeout(() => { issueDropdownOpen.value = false }, DROPDOWN_CLOSE_DELAY)
}

// --- Mode and permissions ---

function setMode(mode: 'readonly' | 'write') {
  isReadonly.value = mode === 'readonly'
  // Deselect current tool if it becomes hidden after mode change
  if (selectedTool.value && !filteredTools.value.find(t => t.name === selectedTool.value?.name)) {
    selectedTool.value = null
  }
}

function toggleTodo() {
  showTodo.value = !showTodo.value
  if (selectedTool.value && !filteredTools.value.find(t => t.name === selectedTool.value?.name)) {
    selectedTool.value = null
  }
}

function toggleAdmin() {
  showAdmin.value = !showAdmin.value
  if (selectedTool.value && !filteredTools.value.find(t => t.name === selectedTool.value?.name)) {
    selectedTool.value = null
  }
}

async function loadTools() {
  loadingTools.value = true
  toolsError.value = null
  sessionId = null
  try {
    const rpc = await mcpRequest('tools/list', {})
    tools.value = rpc.result?.tools ?? []
    if (!tools.value.length) toolsError.value = 'No tools found on the MCP server.'
  } catch (e: unknown) {
    toolsError.value = `Failed to connect to MCP server: ${e instanceof Error ? e.message : String(e)}`
    tools.value = []
  } finally {
    loadingTools.value = false
  }
}

function selectTool(tool: McpTool) {
  selectedTool.value = tool
  paramValues.value = {}
  projectSearch.value = ''
  issueSearch.value = ''
  callStatus.value = null
  resultText.value = '// result will appear here'
}

async function loadIssues() {
  try {
    const data = await api.get<Pick<Issue, 'id' | 'title' | 'number'>[]>('/api/issues')
    allIssues.value = data
  } catch { /* ignore – issues search is a best-effort enhancement */ }
}

async function callTool() {
  if (!selectedTool.value) return
  calling.value = true
  callStatus.value = null
  try {
    const args: Record<string, unknown> = {}
    const parseErrors: string[] = []
    for (const param of toolParams.value) {
      const val = paramValues.value[param.name]
      if (val !== undefined && val !== '') {
        if (param.type === 'boolean') {
          args[param.name] = val === 'true'
        } else if (param.type === 'object' || param.type === 'array') {
          try {
            args[param.name] = JSON.parse(val)
          } catch {
            parseErrors.push(`"${param.name}" is not valid JSON`)
            args[param.name] = val
          }
        } else {
          args[param.name] = val
        }
      }
    }
    if (parseErrors.length) {
      callStatus.value = { ok: false, message: `JSON parse warning: ${parseErrors.join(', ')}` }
    }
    const rpc = await mcpRequest('tools/call', { name: selectedTool.value.name, arguments: args })
    const content = rpc.result?.content ?? rpc.error ?? rpc
    resultText.value = JSON.stringify(content, null, 2)
    callStatus.value = { ok: true, message: `✓ Success (id=${rpc.id})` }
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e)
    resultText.value = msg
    callStatus.value = { ok: false, message: `✗ Error: ${msg}` }
  } finally {
    calling.value = false
  }
}

onMounted(async () => {
  await Promise.all([
    loadTools(),
    projectsStore.fetchProjects(),
    loadIssues(),
  ])
})
</script>
