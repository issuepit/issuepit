<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">API Keys</h2>
        <p class="text-sm text-gray-400 mt-0.5">Store credentials for Hetzner, AI providers, GitHub, and more.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="showCreate = true"
      >
        Add Key
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.keysLoading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.apiKeys.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No API keys configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="showCreate = true">Add your first key →</button>
    </div>

    <!-- Keys table -->
    <div v-else class="rounded-lg border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Provider</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Expires</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="key in store.apiKeys" :key="key.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ key.name }}</td>
            <td class="px-4 py-3">
              <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium"
                :class="providerBadgeClass(key.provider)">
                {{ key.providerName }}
              </span>
            </td>
            <td class="px-4 py-3 text-gray-400">{{ formatDate(key.createdAt) }}</td>
            <td class="px-4 py-3 text-gray-400">{{ key.expiresAt ? formatDate(key.expiresAt) : '—' }}</td>
            <td class="px-4 py-3 text-right">
              <button
                class="text-gray-500 hover:text-red-400 transition-colors text-xs"
                @click="confirmDelete(key.id, key.name)"
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
        <h3 class="text-lg font-semibold text-white mb-5">Add API Key</h3>
        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. Hetzner Production"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Provider</label>
            <select v-model.number="form.provider"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option v-for="(label, val) in providerOptions" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Value</label>
            <input v-model="form.value" type="password" required placeholder="Paste your key here"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Expires (optional)</label>
            <input v-model="form.expiresAt" type="date"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : 'Save Key' }}
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
import { ApiKeyProvider, ApiKeyProviderLabels } from '~/types'
import { useConfigStore } from '~/stores/config'

const store = useConfigStore()

onMounted(() => store.fetchApiKeys())

const showCreate = ref(false)
const saving = ref(false)

const form = reactive({
  name: '',
  provider: ApiKeyProvider.Custom,
  value: '',
  expiresAt: '',
  orgId: '', // TODO: resolve from active org context
})

const providerOptions = computed(() =>
  Object.entries(ApiKeyProviderLabels).reduce((acc, [k, v]) => ({ ...acc, [k]: v }), {} as Record<string, string>)
)

function providerBadgeClass(provider: ApiKeyProvider) {
  const map: Partial<Record<ApiKeyProvider, string>> = {
    [ApiKeyProvider.Hetzner]: 'bg-red-900/50 text-red-300',
    [ApiKeyProvider.OpenAi]: 'bg-green-900/50 text-green-300',
    [ApiKeyProvider.Anthropic]: 'bg-orange-900/50 text-orange-300',
    [ApiKeyProvider.GitHub]: 'bg-gray-700 text-gray-300',
    [ApiKeyProvider.GitLab]: 'bg-orange-900/50 text-orange-300',
    [ApiKeyProvider.AzureOpenAi]: 'bg-blue-900/50 text-blue-300',
    [ApiKeyProvider.Google]: 'bg-yellow-900/50 text-yellow-300',
    [ApiKeyProvider.OpenRouter]: 'bg-purple-900/50 text-purple-300',
  }
  return map[provider] ?? 'bg-gray-700 text-gray-300'
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

async function handleCreate() {
  saving.value = true
  try {
    await store.createApiKey({
      orgId: form.orgId,
      name: form.name,
      provider: form.provider,
      value: form.value,
      expiresAt: form.expiresAt || undefined,
    })
    showCreate.value = false
    Object.assign(form, { name: '', provider: ApiKeyProvider.Custom, value: '', expiresAt: '' })
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete key "${name}"?`)) store.deleteApiKey(id)
}
</script>
