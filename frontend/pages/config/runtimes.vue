<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Agent Runtimes</h2>
        <p class="text-sm text-gray-400 mt-0.5">Define where agents execute: local Docker, SSH, Hetzner cloud, and more.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="openCreate"
      >
        Add Runtime
      </button>
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

onMounted(() => store.fetchRuntimes())

const showForm = ref(false)
const saving = ref(false)
const editing = ref<RuntimeConfiguration | null>(null)

const form = reactive({
  name: '',
  type: RuntimeType.Docker,
  configuration: '{}',
  isDefault: false,
  orgId: '',
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

function openCreate() {
  editing.value = null
  Object.assign(form, { name: '', type: RuntimeType.Docker, configuration: configPlaceholder.value, isDefault: false, orgId: '' })
  showForm.value = true
}

function openEdit(rt: RuntimeConfiguration) {
  editing.value = rt
  Object.assign(form, { name: rt.name, type: rt.type, configuration: rt.configuration, isDefault: rt.isDefault, orgId: rt.orgId })
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
