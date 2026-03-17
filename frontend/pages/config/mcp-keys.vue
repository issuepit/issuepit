<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">MCP Access Keys</h2>
        <p class="text-sm text-gray-400 mt-0.5">Manage authentication tokens for the IssuePit built-in MCP server.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="showCreate = true"
      >
        Create Key
      </button>
    </div>

    <!-- Info box -->
    <div class="rounded-lg border border-blue-900/40 bg-blue-900/10 px-4 py-3 mb-6 text-sm text-blue-300">
      <p class="font-medium mb-1">Using MCP keys</p>
      <p class="text-blue-400/80">
        Configure your MCP client with <code class="bg-blue-900/30 px-1 rounded font-mono">Authorization: Bearer &lt;token&gt;</code>.
        Ephemeral keys are created automatically for each agent run and are not shown here.
      </p>
    </div>

    <!-- Loading -->
    <div v-if="store.mcpTokensLoading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.mcpTokens.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No MCP access keys yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="showCreate = true">Create your first key →</button>
    </div>

    <!-- Tokens table -->
    <div v-else class="rounded-lg border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Permissions</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Scope</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Expires</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="token in store.mcpTokens" :key="token.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ token.name }}</td>
            <td class="px-4 py-3">
              <span
                :class="token.isReadOnly ? 'bg-yellow-900/40 text-yellow-300' : 'bg-green-900/40 text-green-300'"
                class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium"
              >
                {{ token.isReadOnly ? 'Read-only' : 'Read & write' }}
              </span>
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs">
              <span v-if="token.projectId">Project</span>
              <span v-else-if="token.orgId">Org</span>
              <span v-else>Tenant</span>
            </td>
            <td class="px-4 py-3 text-gray-400"><DateDisplay :date="token.createdAt" mode="absolute" resolution="date" /></td>
            <td class="px-4 py-3 text-gray-400"><span v-if="token.expiresAt"><DateDisplay :date="token.expiresAt" mode="absolute" resolution="date" /></span><span v-else>—</span></td>
            <td class="px-4 py-3 text-right">
              <button
                class="text-gray-500 hover:text-red-400 transition-colors text-xs"
                @click="confirmRevoke(token.id, token.name)"
              >
                Revoke
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create modal -->
    <div v-if="showCreate" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">Create MCP Access Key</h3>
        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. My IDE integration"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div class="flex items-center gap-3">
            <input id="readonly-toggle" v-model="form.isReadOnly" type="checkbox"
              class="h-4 w-4 rounded border-gray-600 bg-gray-800 text-brand-500 focus:ring-brand-500" />
            <label for="readonly-toggle" class="text-sm text-gray-300">Read-only (hides write tools)</label>
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Expires (optional)</label>
            <input v-model="form.expiresAt" type="date"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Creating…' : 'Create Key' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="closeCreate">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Token revealed modal (shown once after creation) -->
    <div v-if="newToken" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-lg p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-2">Key Created</h3>
        <p class="text-sm text-yellow-400 mb-4">
          <span aria-hidden="true">⚠</span>
          <span class="sr-only">Warning:</span>
          Copy this token now — it will <strong>not</strong> be shown again.
        </p>
        <div class="flex items-center gap-2 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 mb-4">
          <code class="flex-1 text-sm text-green-300 font-mono break-all select-all">{{ newToken }}</code>
          <button class="shrink-0 text-xs text-gray-400 hover:text-white px-2 py-1 rounded bg-gray-700 hover:bg-gray-600 transition-colors"
            @click="copyToken">{{ copied ? 'Copied!' : 'Copy' }}</button>
        </div>
        <div class="text-xs text-gray-500 mb-4">
          Use as <code class="text-gray-300 font-mono">Authorization: Bearer {{ newToken }}</code>
        </div>
        <button class="w-full px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
          @click="newToken = null; copied = false">
          Done
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useConfigStore } from '~/stores/config'

const store = useConfigStore()

onMounted(() => store.fetchMcpTokens())

const showCreate = ref(false)
const saving = ref(false)
const newToken = ref<string | null>(null)
const copied = ref(false)

const form = reactive({
  name: '',
  isReadOnly: false,
  expiresAt: '',
})

function closeCreate() {
  showCreate.value = false
  Object.assign(form, { name: '', isReadOnly: false, expiresAt: '' })
}

async function handleCreate() {
  saving.value = true
  try {
    const created = await store.createMcpToken({
      name: form.name,
      isReadOnly: form.isReadOnly,
      expiresAt: form.expiresAt || undefined,
    })
    newToken.value = created.rawToken
    closeCreate()
    await store.fetchMcpTokens()
  } finally {
    saving.value = false
  }
}

async function copyToken() {
  if (!newToken.value) return
  await navigator.clipboard.writeText(newToken.value)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

function confirmRevoke(id: string, name: string) {
  if (confirm(`Revoke key "${name}"? Any clients using this key will lose access.`)) {
    store.revokeMcpToken(id)
  }
}
</script>
