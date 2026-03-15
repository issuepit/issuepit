<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Runtimes</h2>
        <p class="text-sm text-gray-400 mt-0.5">Define where agents execute: local Docker, SSH, Hetzner cloud, and more.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="openCreate"
      >
        Add Runtime
      </button>
    </div>

    <!-- Pool Status -->
    <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-5 mb-6">
      <div class="flex items-center justify-between mb-3">
        <h3 class="font-medium text-white">Pool Status</h3>
        <button
          class="text-xs text-gray-400 hover:text-gray-200 transition-colors"
          :disabled="store.poolStatusLoading"
          @click="store.fetchPoolStatus()"
        >
          {{ store.poolStatusLoading ? 'Refreshing…' : '↺ Refresh' }}
        </button>
      </div>

      <div v-if="store.poolStatus">
        <!-- Agent Pools -->
        <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">Agent Pools</p>
        <div class="space-y-2 mb-4">
          <div
            v-for="pool in store.poolStatus.agentPools"
            :key="pool.runtimeConfigId ?? 'default'"
            class="flex items-center gap-3 p-3 rounded-lg bg-gray-900 border border-gray-800"
          >
            <div class="flex-1 min-w-0">
              <span class="text-sm text-white font-medium">{{ pool.runtimeName }}</span>
            </div>
            <div class="flex items-center gap-4 text-xs text-gray-400">
              <span class="flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-green-500 inline-block" />
                {{ pool.runningAgents }} running
              </span>
              <span v-if="pool.pendingAgents > 0" class="flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-yellow-500 inline-block" />
                {{ pool.pendingAgents }} pending
              </span>
              <span class="text-gray-600">
                limit: {{ pool.maxConcurrentAgents === 0 ? '∞' : pool.maxConcurrentAgents }}
              </span>
            </div>
            <!-- Utilisation bar -->
            <div v-if="pool.maxConcurrentAgents > 0" class="w-24 h-1.5 rounded-full bg-gray-800">
              <div
                class="h-full rounded-full transition-all"
                :class="poolBarColor(pool.runningAgents, pool.maxConcurrentAgents)"
                :style="{ width: poolBarWidth(pool.runningAgents, pool.maxConcurrentAgents) }"
              />
            </div>
          </div>
          <p v-if="!store.poolStatus.agentPools.length" class="text-xs text-gray-600">No agent pool data.</p>
        </div>

        <!-- CI/CD Pools -->
        <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">CI/CD Pools</p>
        <div class="space-y-2">
          <div
            v-for="pool in store.poolStatus.cicdPools"
            :key="pool.orgId"
            class="flex items-center gap-3 p-3 rounded-lg bg-gray-900 border border-gray-800"
          >
            <div class="flex-1 min-w-0">
              <span class="text-sm text-white font-medium">{{ pool.orgName }}</span>
            </div>
            <div class="flex items-center gap-4 text-xs text-gray-400">
              <span class="flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-green-500 inline-block" />
                {{ pool.runningCiCdRuns }} running
              </span>
              <span v-if="pool.pendingCiCdRuns > 0" class="flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-yellow-500 inline-block" />
                {{ pool.pendingCiCdRuns }} pending
              </span>
              <span class="text-gray-600">
                limit: {{ pool.maxConcurrentRunners === 0 ? '∞' : pool.maxConcurrentRunners }}
              </span>
            </div>
            <div v-if="pool.maxConcurrentRunners > 0" class="w-24 h-1.5 rounded-full bg-gray-800">
              <div
                class="h-full rounded-full transition-all"
                :class="poolBarColor(pool.runningCiCdRuns, pool.maxConcurrentRunners)"
                :style="{ width: poolBarWidth(pool.runningCiCdRuns, pool.maxConcurrentRunners) }"
              />
            </div>
          </div>
          <p v-if="!store.poolStatus.cicdPools.length" class="text-xs text-gray-600">No CI/CD pool data.</p>
        </div>
      </div>
      <p v-else-if="!store.poolStatusLoading" class="text-xs text-gray-600">Click Refresh to load pool status.</p>
      <p v-else class="text-xs text-gray-500">Loading…</p>
    </div>

    <!-- Loading -->
    <div v-if="store.runtimesLoading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.runtimes.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No runtimes configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="openCreate">Add your first runtime →</button>
    </div>

    <!-- Runtimes list -->
    <div v-else class="space-y-3">
      <div
        v-for="rt in store.runtimes"
        :key="rt.id"
        class="rounded-lg border border-gray-800 bg-gray-900/40 p-4 flex items-start gap-4 hover:border-gray-700 transition-colors"
      >
        <!-- Icon -->
        <div class="w-10 h-10 rounded-lg flex items-center justify-center shrink-0" :class="runtimeIconBg(rt.type)">
          <span class="text-lg">{{ runtimeEmoji(rt.type) }}</span>
        </div>

        <!-- Info -->
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="font-medium text-white">{{ rt.name }}</span>
            <span v-if="rt.isDefault"
              class="px-1.5 py-0.5 rounded text-xs font-medium bg-brand-900/60 text-brand-300 border border-brand-800">
              Default
            </span>
            <span v-if="rt.maxConcurrentAgents > 0"
              class="px-1.5 py-0.5 rounded text-xs font-medium bg-gray-800 text-gray-400 border border-gray-700">
              max {{ rt.maxConcurrentAgents }} agents
            </span>
          </div>
          <p class="text-sm text-gray-400 mt-0.5">{{ rt.typeName }}</p>
          <pre class="text-xs text-gray-600 mt-2 font-mono truncate">{{ rt.configuration }}</pre>
        </div>

        <!-- Actions -->
        <div class="flex gap-2 shrink-0">
          <button
            class="px-3 py-1.5 text-xs text-gray-400 hover:text-white border border-gray-700 hover:border-gray-600 rounded-lg transition-colors"
            @click="openEdit(rt)"
          >
            Edit
          </button>
          <button
            class="px-3 py-1.5 text-xs text-gray-500 hover:text-red-400 border border-gray-800 hover:border-red-900 rounded-lg transition-colors"
            @click="confirmDelete(rt.id, rt.name)"
          >
            Delete
          </button>
        </div>
      </div>
    </div>

    <!-- Create / Edit modal -->
    <div v-if="showForm" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-lg p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">{{ editing ? 'Edit Runtime' : 'Add Runtime' }}</h3>
        <form class="space-y-4" @submit.prevent="handleSave">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. Hetzner EU Worker"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>

          <div>
            <label class="block text-sm text-gray-400 mb-1">Runtime Type</label>
            <select v-model.number="form.type"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option v-for="(label, val) in runtimeTypeOptions" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
          </div>

          <!-- Dynamic config hints based on type -->
          <div>
            <label class="block text-sm text-gray-400 mb-1">
              Configuration (JSON)
              <span class="text-gray-600 ml-1">— {{ configHint }}</span>
            </label>
            <textarea v-model="form.configuration" rows="5" required
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500 resize-none"
              :placeholder="configPlaceholder" />
          </div>

          <div>
            <label class="block text-sm text-gray-400 mb-1">
              Max Concurrent Agents
              <span class="text-gray-600 ml-1">— 0 = unlimited</span>
            </label>
            <input v-model.number="form.maxConcurrentAgents" type="number" min="0"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
              placeholder="0" />
          </div>

          <div class="flex items-center gap-2">
            <input id="isDefault" v-model="form.isDefault" type="checkbox"
              class="w-4 h-4 rounded border-gray-600 bg-gray-800 text-brand-600" />
            <label for="isDefault" class="text-sm text-gray-300">Set as default runtime</label>
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
import { RuntimeType, RuntimeTypeLabels } from '~/types'
import type { RuntimeConfiguration } from '~/types'
import { useConfigStore } from '~/stores/config'

const store = useConfigStore()

onMounted(() => {
  store.fetchRuntimes()
  store.fetchPoolStatus()
})

const showForm = ref(false)
const saving = ref(false)
const editing = ref<RuntimeConfiguration | null>(null)

const form = reactive({
  name: '',
  type: RuntimeType.Docker,
  configuration: '{}',
  isDefault: false,
  orgId: '',
  maxConcurrentAgents: 0,
})

const runtimeTypeOptions = computed(() =>
  Object.entries(RuntimeTypeLabels).reduce((acc, [k, v]) => ({ ...acc, [k]: v }), {} as Record<string, string>)
)

const configHint = computed(() => {
  const hints: Record<RuntimeType, string> = {
    [RuntimeType.Native]: 'no extra config needed',
    [RuntimeType.Docker]: 'docker socket path, image defaults',
    [RuntimeType.Ssh]: 'host, port, user, key reference',
    [RuntimeType.HetznerSsh]: 'server type, location, API key ref',
    [RuntimeType.OpenSandbox]: 'sandbox endpoint, credentials ref',
  }
  return hints[form.type as RuntimeType] ?? ''
})

const configPlaceholder = computed(() => {
  const examples: Record<RuntimeType, string> = {
    [RuntimeType.Native]: '{}',
    [RuntimeType.Docker]: '{\n  "socketPath": "/var/run/docker.sock",\n  "defaultImage": "ubuntu:24.04"\n}',
    [RuntimeType.Ssh]: '{\n  "host": "10.0.0.1",\n  "port": 22,\n  "user": "agent",\n  "keyRef": "hetzner-ssh-key"\n}',
    [RuntimeType.HetznerSsh]: '{\n  "serverType": "cx21",\n  "location": "nbg1",\n  "apiKeyRef": "hetzner-api-key",\n  "sshKeyRef": "hetzner-ssh-key"\n}',
    [RuntimeType.OpenSandbox]: '{\n  "endpoint": "https://sandbox.example.com",\n  "credentialsRef": "opensandbox-creds"\n}',
  }
  return examples[form.type as RuntimeType] ?? '{}'
})

watch(() => form.type, () => {
  if (!editing.value) form.configuration = configPlaceholder.value
})

function runtimeEmoji(type: RuntimeType): string {
  const map: Record<RuntimeType, string> = {
    [RuntimeType.Native]: '🖥️',
    [RuntimeType.Docker]: '🐳',
    [RuntimeType.Ssh]: '🔐',
    [RuntimeType.HetznerSsh]: '☁️',
    [RuntimeType.OpenSandbox]: '📦',
  }
  return map[type] ?? '⚙️'
}

function runtimeIconBg(type: RuntimeType): string {
  const map: Record<RuntimeType, string> = {
    [RuntimeType.Native]: 'bg-gray-800',
    [RuntimeType.Docker]: 'bg-blue-900/40',
    [RuntimeType.Ssh]: 'bg-yellow-900/40',
    [RuntimeType.HetznerSsh]: 'bg-red-900/40',
    [RuntimeType.OpenSandbox]: 'bg-purple-900/40',
  }
  return map[type] ?? 'bg-gray-800'
}

function poolBarWidth(active: number, max: number): string {
  if (max <= 0) return '0%'
  return `${Math.min(100, Math.round((active / max) * 100))}%`
}

function poolBarColor(active: number, max: number): string {
  if (max <= 0) return 'bg-gray-600'
  const pct = active / max
  if (pct >= 1) return 'bg-red-500'
  if (pct >= 0.75) return 'bg-yellow-500'
  return 'bg-green-500'
}

function openCreate() {
  editing.value = null
  Object.assign(form, { name: '', type: RuntimeType.Docker, configuration: configPlaceholder.value, isDefault: false, orgId: '', maxConcurrentAgents: 0 })
  showForm.value = true
}

function openEdit(rt: RuntimeConfiguration) {
  editing.value = rt
  Object.assign(form, { name: rt.name, type: rt.type, configuration: rt.configuration, isDefault: rt.isDefault, orgId: rt.orgId, maxConcurrentAgents: rt.maxConcurrentAgents })
  showForm.value = true
}

async function handleSave() {
  saving.value = true
  try {
    if (editing.value) {
      await store.updateRuntime(editing.value.id, { ...form })
    } else {
      await store.createRuntime({ ...form })
    }
    showForm.value = false
  } finally {
    saving.value = false
  }
}

function confirmDelete(id: string, name: string) {
  if (confirm(`Delete runtime "${name}"?`)) store.deleteRuntime(id)
}
</script>
