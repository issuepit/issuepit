<template>
  <div class="p-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Agents', to: '/agents', icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
          { label: 'MCP Playground', to: '/config/mcp-playground', icon: 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z' },
        ]" />
        <p class="text-sm text-gray-400 mt-0.5">
          Interactively test tools on the built-in IssuePit MCP server.
          Endpoint: <code class="text-green-300 font-mono text-xs">{{ mcpBase }}/mcp</code>
        </p>
      </div>
      <button
        class="flex items-center gap-2 px-4 py-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium rounded-lg transition-colors border border-gray-700"
        :disabled="loadingTools"
        @click="loadTools"
      >
        <svg class="w-4 h-4" :class="loadingTools ? 'animate-spin' : ''" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
        </svg>
        {{ loadingTools ? 'Loading…' : 'Reload Tools' }}
      </button>
    </div>

    <!-- Error state -->
    <div v-if="toolsError" data-testid="mcp-tools-error" class="mb-4 bg-red-900/20 border border-red-900/40 rounded-lg px-4 py-3 text-sm text-red-400">
      {{ toolsError }}
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">
      <!-- Tools list -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
        <h3 class="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">
          Available Tools
          <span v-if="tools.length" class="ml-1 bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded-full">{{ tools.length }}</span>
        </h3>
        <div v-if="loadingTools" class="space-y-2">
          <div v-for="i in 6" :key="i" class="h-9 bg-gray-800 rounded-lg animate-pulse" />
        </div>
        <div v-else-if="!tools.length" data-testid="mcp-tools-empty" class="text-sm text-gray-600">
          No tools available. Make sure the MCP server is running.
        </div>
        <ul v-else data-testid="mcp-tools-list" class="space-y-1 max-h-96 overflow-y-auto">
          <li
            v-for="tool in tools"
            :key="tool.name"
            class="cursor-pointer px-3 py-2 rounded-lg text-sm font-mono transition-colors"
            :class="selectedTool?.name === tool.name
              ? 'bg-brand-600 text-white'
              : 'text-gray-300 hover:bg-gray-800'"
            @click="selectTool(tool)"
          >
            {{ tool.name }}
          </li>
        </ul>
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
                <select
                  v-if="param.type === 'boolean'"
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
                />
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
          <h3 class="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">Result</h3>
          <pre class="bg-gray-950 border border-gray-800 rounded-lg p-4 text-xs font-mono text-green-300 min-h-32 overflow-x-auto whitespace-pre-wrap break-all">{{ resultText }}</pre>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
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

const config = useRuntimeConfig()
const mcpBase = config.public.mcpBase as string
const mcpUrl = `${mcpBase}/mcp`

let reqId = 1
let sessionId: string | null = null

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
  callStatus.value = null
  resultText.value = '// result will appear here'
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

onMounted(() => loadTools())
</script>
