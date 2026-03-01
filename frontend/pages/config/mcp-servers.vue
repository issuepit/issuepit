<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">MCP Servers</h2>
        <p class="text-sm text-gray-400 mt-0.5">Manage Model Context Protocol servers and link them to agents.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="openCreate"
      >
        Add MCP Server
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.mcpServers.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No MCP servers configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="openCreate">Add your first MCP server →</button>
    </div>

    <!-- MCP Servers list -->
    <div v-else class="space-y-3">
      <div
        v-for="server in store.mcpServers"
        :key="server.id"
        class="rounded-lg border border-gray-800 bg-gray-900/40 p-4 hover:border-gray-700 transition-colors"
      >
        <div class="flex items-start justify-between gap-4">
          <!-- Icon + Info -->
          <div class="flex items-start gap-3 flex-1 min-w-0">
            <div class="w-10 h-10 rounded-lg bg-indigo-900/40 flex items-center justify-center shrink-0">
              <svg class="w-5 h-5 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M5 12h14M12 5l7 7-7 7" />
              </svg>
            </div>
            <div class="flex-1 min-w-0">
              <p class="font-medium text-white">{{ server.name }}</p>
              <p v-if="server.description" class="text-sm text-gray-400 mt-0.5">{{ server.description }}</p>
              <code class="text-xs text-green-300 font-mono mt-1 block truncate">{{ server.url }}</code>
            </div>
          </div>

          <!-- Actions -->
          <div class="flex gap-2 shrink-0">
            <button
              class="px-3 py-1.5 text-xs text-gray-400 hover:text-white border border-gray-700 hover:border-gray-600 rounded-lg transition-colors"
              @click="openEdit(server)"
            >
              Edit
            </button>
            <button
              class="px-3 py-1.5 text-xs text-gray-500 hover:text-red-400 border border-gray-800 hover:border-red-900 rounded-lg transition-colors"
              @click="confirmDelete(server.id, server.name)"
            >
              Delete
            </button>
          </div>
        </div>

        <!-- Allowed Tools -->
        <div v-if="server.allowedTools?.length" class="mt-3 flex flex-wrap gap-1">
          <span
            v-for="tool in server.allowedTools.slice(0, 6)"
            :key="tool"
            class="text-xs bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded font-mono"
          >{{ tool }}</span>
          <span v-if="server.allowedTools.length > 6" class="text-xs text-gray-500">
            +{{ server.allowedTools.length - 6 }} more
          </span>
        </div>
      </div>
    </div>

    <!-- Create / Edit modal -->
    <div v-if="showForm" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-2xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h3 class="text-lg font-semibold text-white mb-5">{{ editing ? 'Edit MCP Server' : 'Add MCP Server' }}</h3>

        <!-- Template picker (only on create) -->
        <div v-if="!editing && store.templates.length" class="mb-5">
          <p class="text-sm text-gray-400 mb-2">Start from a template:</p>
          <div class="grid grid-cols-2 gap-2 sm:grid-cols-3">
            <button
              v-for="tpl in store.templates"
              :key="tpl.key"
              class="text-left rounded-lg border border-gray-700 bg-gray-800/60 px-3 py-2.5 hover:border-brand-600 hover:bg-gray-800 transition-colors"
              @click="applyTemplate(tpl)"
            >
              <p class="text-sm font-medium text-white">{{ tpl.name }}</p>
              <p class="text-xs text-gray-500 mt-0.5 line-clamp-2">{{ tpl.description }}</p>
            </button>
          </div>
        </div>

        <form class="space-y-4" @submit.prevent="handleSave">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. GitHub MCP"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Description <span class="text-gray-600">(optional)</span></label>
            <input v-model="form.description" type="text" placeholder="What this MCP server provides"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">URL / Command</label>
            <input v-model="form.url" type="text" required placeholder="npx @modelcontextprotocol/server-github"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">
              Allowed Tools
              <span class="text-gray-600">(comma-separated)</span>
            </label>
            <input v-model="toolsInput" type="text" placeholder="create_issue, list_issues, search_repositories"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">
              Configuration (JSON)
              <span class="text-gray-600 ml-1">— environment variables, options</span>
            </label>
            <textarea v-model="form.configuration" rows="4" required
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500 resize-none"
              placeholder="{}" />
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : 'Save' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="showForm = false">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { McpServer, McpServerTemplate } from '~/types'
import { useMcpServersStore } from '~/stores/mcp-servers'

const store = useMcpServersStore()

onMounted(async () => {
  await Promise.all([store.fetchMcpServers(), store.fetchTemplates()])
})

const showForm = ref(false)
const saving = ref(false)
const editing = ref<McpServer | null>(null)
const toolsInput = ref('')

const form = reactive({
  name: '',
  description: '',
  url: '',
  configuration: '{}',
  orgId: '', // TODO: resolve from active org context
})

function openCreate() {
  editing.value = null
  Object.assign(form, { name: '', description: '', url: '', configuration: '{}', orgId: '' })
  toolsInput.value = ''
  showForm.value = true
}

function openEdit(server: McpServer) {
  editing.value = server
  Object.assign(form, {
    name: server.name,
    description: server.description ?? '',
    url: server.url,
    configuration: server.configuration,
    orgId: server.orgId,
  })
  toolsInput.value = (server.allowedTools ?? []).join(', ')
  showForm.value = true
}

function applyTemplate(tpl: McpServerTemplate) {
  form.name = tpl.name
  form.description = tpl.description
  form.url = tpl.url
  form.configuration = tpl.configuration
  toolsInput.value = (tpl.allowedTools ?? []).join(', ')
}

async function handleSave() {
  saving.value = true
  try {
    const allowedTools = toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)
    const payload = { ...form, allowedTools }
    if (editing.value) {
      await store.updateMcpServer(editing.value.id, payload)
    } else {
      await store.createMcpServer(payload)
    }
    showForm.value = false
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete MCP server "${name}"?`)) store.deleteMcpServer(id)
}
</script>
