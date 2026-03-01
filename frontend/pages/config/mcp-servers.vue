<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">MCP Servers</h2>
        <p class="text-sm text-gray-400 mt-0.5">Register external MCP servers that agents can connect to.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="showCreate = true"
      >
        Add MCP Server
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.mcpServers.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No MCP servers configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="showCreate = true">Add your first MCP server →</button>
    </div>

    <!-- Servers table -->
    <div v-else class="rounded-lg border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">URL</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="server in store.mcpServers" :key="server.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ server.name }}</td>
            <td class="px-4 py-3 text-gray-300 font-mono text-xs">{{ server.url }}</td>
            <td class="px-4 py-3 text-gray-400">{{ formatDate(server.createdAt) }}</td>
            <td class="px-4 py-3 text-right">
              <button
                class="text-gray-500 hover:text-red-400 transition-colors text-xs"
                @click="confirmDelete(server.id, server.name)"
              >
                Delete
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create modal -->
    <div v-if="showCreate" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">Add MCP Server</h3>
        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. GitHub MCP"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">URL</label>
            <input v-model="form.url" type="url" required placeholder="https://mcp.example.com"
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
              {{ saving ? 'Saving…' : 'Save' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="showCreate = false">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useMcpServersStore } from '~/stores/mcp-servers'

const store = useMcpServersStore()

onMounted(() => store.fetchMcpServers())

const showCreate = ref(false)
const saving = ref(false)

const form = reactive({
  name: '',
  url: '',
  configuration: '{}',
  orgId: '', // TODO: resolve from active org context
})

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

async function handleCreate() {
  saving.value = true
  try {
    await store.createMcpServer({
      orgId: form.orgId,
      name: form.name,
      url: form.url,
      configuration: form.configuration,
    })
    showCreate.value = false
    Object.assign(form, { name: '', url: '', configuration: '{}' })
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete MCP server "${name}"?`)) store.deleteMcpServer(id)
}
</script>
