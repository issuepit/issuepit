<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Agents', to: '/agents', icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2' },
          { label: 'Modes', to: '/agents', icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
      </div>
      <button @click="openCreate"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Agent Mode
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Agents Grid -->
    <div v-else class="space-y-3">
      <div v-for="agent in store.agents" :key="agent.id"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors">
        <div class="flex items-start justify-between">
          <NuxtLink :to="`/agents/${agent.id}`" class="flex items-center gap-3 flex-1 min-w-0 mr-4">
            <div class="w-10 h-10 bg-indigo-900/40 rounded-lg flex items-center justify-center shrink-0">
              <svg class="w-5 h-5 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
              </svg>
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h3 class="font-semibold text-white">{{ agent.name }}</h3>
                <span :class="agent.isActive ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
                  class="text-xs px-1.5 py-0.5 rounded-full">
                  {{ agent.isActive ? 'Active' : 'Inactive' }}
                </span>
              </div>
              <p v-if="agent.description" class="text-sm text-gray-400 mt-0.5">{{ agent.description }}</p>
            </div>
          </NuxtLink>

          <div class="flex items-center gap-2 shrink-0">
            <button @click="store.toggleAgent(agent.id, !agent.isActive)"
              :class="agent.isActive ? 'text-yellow-400 hover:text-yellow-300' : 'text-green-400 hover:text-green-300'"
              class="text-xs px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              {{ agent.isActive ? 'Deactivate' : 'Activate' }}
            </button>
            <NuxtLink :to="`/agents/${agent.id}`"
              class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              Edit
            </NuxtLink>
            <button @click="store.deleteAgent(agent.id)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors">
              Delete
            </button>
          </div>
        </div>

        <!-- Details -->
        <div class="mt-4 grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Docker Image</p>
            <code class="text-xs text-green-300 font-mono">{{ agent.dockerImage || '—' }}</code>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">Allowed Tools</p>
            <div class="flex flex-wrap gap-1">
              <span v-for="tool in (agent.allowedTools ?? []).slice(0, 4)" :key="tool"
                class="text-xs bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded font-mono">{{ tool }}</span>
              <span v-if="(agent.allowedTools ?? []).length > 4" class="text-xs text-gray-500">
                +{{ agent.allowedTools.length - 4 }} more
              </span>
              <span v-if="!agent.allowedTools?.length" class="text-xs text-gray-600">None</span>
            </div>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">System Prompt</p>
            <p class="text-xs text-gray-400 line-clamp-2">{{ agent.systemPrompt || '—' }}</p>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="!store.loading && store.agents.length === 0"
        class="flex flex-col items-center justify-center py-20 text-center">
        <div class="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
          <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No agent modes yet</p>
        <p class="text-gray-600 text-sm mt-1">Create your first agent mode to automate tasks</p>
        <button @click="openCreate"
          class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          Create Agent Mode
        </button>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div v-if="showModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-xl p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingId ? 'Edit Agent Mode' : 'Create Agent Mode' }}</h2>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Organization</label>
              <select v-model="form.orgId" data-testid="org-select"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="" disabled selected>Select an organization</option>
                <option v-for="org in orgsStore.orgs" :key="org.id" :value="org.id">{{ org.name }}</option>
              </select>
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
              <input v-model="form.name" type="text" placeholder="Agent name"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
              <input v-model="form.description" type="text" placeholder="Optional description"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Docker Image</label>
              <input v-model="form.dockerImage" type="text" placeholder="ghcr.io/org/agent:latest"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">System Prompt</label>
              <textarea v-model="form.systemPrompt" rows="4"
                placeholder="You are a helpful agent that..."
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
            </div>
            <div class="col-span-2">
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Allowed Tools
                <span class="text-gray-500 font-normal">(comma-separated)</span>
              </label>
              <input v-model="toolsInput" type="text" placeholder="read_file, write_file, execute_command"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
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
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitModal"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ editingId ? 'Update' : 'Create' }}
          </button>
          <button @click="showModal = false; resetForm()"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAgentsStore } from '~/stores/agents'
import { useOrgsStore } from '~/stores/orgs'
import type { RunnerType } from '~/types'
import { RunnerTypeLabels } from '~/types'

const store = useAgentsStore()
const orgsStore = useOrgsStore()
const showModal = ref(false)
const editingId = ref<string | null>(null)
const toolsInput = ref('')

const form = reactive({
  name: '',
  description: '',
  dockerImage: '',
  systemPrompt: '',
  isActive: true,
  runnerType: null as RunnerType | null,
  model: '',
  orgId: '',
})

const runnerOptions = [
  { value: null, label: '— None (use container entrypoint)' },
  ...Object.entries(RunnerTypeLabels).map(([k, v]) => ({ value: Number(k) as RunnerType, label: v }))
]

onMounted(async () => {
  await Promise.all([store.fetchAgents(), orgsStore.fetchOrgs()])
})

function openCreate() {
  editingId.value = null
  resetForm()
  showModal.value = true
}

async function submitModal() {
  if (!form.name) return
  const payload = {
    ...form,
    allowedTools: JSON.stringify(toolsInput.value.split(',').map(t => t.trim()).filter(Boolean)),
    runnerType: form.runnerType,
    model: form.model || undefined,
  }
  if (editingId.value) {
    await store.updateAgent(editingId.value, payload)
  } else {
    await store.createAgent(payload)
  }
  showModal.value = false
  resetForm()
}

function resetForm() {
  editingId.value = null
  form.name = ''
  form.description = ''
  form.dockerImage = ''
  form.systemPrompt = ''
  form.isActive = true
  form.runnerType = null
  form.model = ''
  form.orgId = ''
  toolsInput.value = ''
}
</script>
