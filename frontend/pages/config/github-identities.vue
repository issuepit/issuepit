<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">GitHub Identities</h2>
        <p class="text-sm text-gray-400 mt-0.5">Manage GitHub accounts (OAuth or PAT) and map them to agents, projects, or organizations.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="openCreate"
      >
        Add Identity
      </button>
    </div>

    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-16">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Empty -->
    <div v-else-if="!store.identities.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-3">
        <svg class="w-6 h-6 text-gray-500" fill="currentColor" viewBox="0 0 24 24">
          <path d="M12 0C5.37 0 0 5.373 0 12c0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61-.546-1.385-1.335-1.755-1.335-1.755-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 21.795 24 17.298 24 12c0-6.627-5.373-12-12-12" />
        </svg>
      </div>
      <p class="text-gray-500 text-sm">No GitHub identities configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="openCreate">Add your first identity →</button>
    </div>

    <!-- Identities list -->
    <div v-else class="space-y-3">
      <div v-for="identity in store.identities" :key="identity.id"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors">
        <div class="flex items-start justify-between gap-4">
          <div class="flex items-center gap-3 min-w-0">
            <div class="w-9 h-9 bg-gray-800 rounded-full flex items-center justify-center shrink-0">
              <svg class="w-5 h-5 text-gray-300" fill="currentColor" viewBox="0 0 24 24">
                <path d="M12 0C5.37 0 0 5.373 0 12c0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61-.546-1.385-1.335-1.755-1.335-1.755-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 21.795 24 17.298 24 12c0-6.627-5.373-12-12-12" />
              </svg>
            </div>
            <div class="min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="font-semibold text-white">{{ identity.name || identity.gitHubUsername }}</span>
                <span class="text-xs text-gray-500 font-mono">@{{ identity.gitHubUsername }}</span>
              </div>
              <p v-if="identity.gitHubEmail" class="text-xs text-gray-500 mt-0.5">{{ identity.gitHubEmail }}</p>
            </div>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <button @click="openMapping(identity)"
              class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              Manage Mappings
            </button>
            <button @click="store.deleteIdentity(identity.id)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors">
              Delete
            </button>
          </div>
        </div>

        <!-- Mappings summary -->
        <div class="mt-4 grid grid-cols-1 sm:grid-cols-4 gap-3">
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Agent</p>
            <p class="text-xs text-white">{{ identity.agentName || '—' }}</p>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Projects</p>
            <div class="flex flex-wrap gap-1">
              <span v-for="p in identity.projects" :key="p.projectId"
                class="text-xs bg-blue-900/30 text-blue-300 px-1.5 py-0.5 rounded">{{ p.name }}</span>
              <span v-if="!identity.projects.length" class="text-xs text-gray-600">None</span>
            </div>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Organizations</p>
            <div class="flex flex-wrap gap-1">
              <span v-for="o in identity.orgs" :key="o.orgId"
                class="text-xs bg-purple-900/30 text-purple-300 px-1.5 py-0.5 rounded">{{ o.name }}</span>
              <span v-if="!identity.orgs.length" class="text-xs text-gray-600">None</span>
            </div>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Issue Sync</p>
            <div class="flex flex-wrap gap-1">
              <span v-for="p in identity.syncProjects" :key="p.projectId"
                class="text-xs bg-green-900/30 text-green-300 px-1.5 py-0.5 rounded">{{ p.name }}</span>
              <span v-if="!identity.syncProjects.length" class="text-xs text-gray-600">None</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create modal -->
    <div v-if="showCreate" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">Add GitHub Identity</h3>
        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Display Name <span class="text-gray-600">(optional)</span></label>
            <input v-model="createForm.name" type="text" placeholder="e.g. Work PAT"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Personal Access Token (PAT)</label>
            <input v-model="createForm.token" type="password" required placeholder="ghp_..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
            <p class="text-xs text-gray-500 mt-1">The token needs <code class="text-gray-400">read:user</code> and <code class="text-gray-400">user:email</code> scopes.</p>
          </div>
          <div v-if="createError" class="text-sm text-red-400 bg-red-900/20 border border-red-900/30 rounded-lg px-3 py-2">
            {{ createError }}
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="creating"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ creating ? 'Validating…' : 'Add Identity' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="showCreate = false">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Mapping modal -->
    <div v-if="mappingIdentity" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-lg p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <div class="flex items-center justify-between mb-5">
          <h3 class="text-lg font-semibold text-white">
            Manage Mappings — <span class="text-gray-400 font-normal">@{{ mappingIdentity.gitHubUsername }}</span>
          </h3>
          <button @click="mappingIdentity = null" class="text-gray-500 hover:text-gray-300">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- Agent mapping -->
        <div class="mb-5">
          <p class="text-sm font-medium text-gray-300 mb-2">Agent</p>
          <div class="flex gap-2">
            <select v-model="selectedAgentId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="">— None —</option>
              <option v-for="agent in agentsStore.agents" :key="agent.id" :value="agent.id">{{ agent.name }}</option>
            </select>
            <button @click="saveAgentMapping"
              class="px-3 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm rounded-lg transition-colors">
              Save
            </button>
          </div>
        </div>

        <!-- Project mappings -->
        <div class="mb-5">
          <p class="text-sm font-medium text-gray-300 mb-2">Projects</p>
          <div class="space-y-1 mb-2">
            <div v-for="p in mappingIdentity.projects" :key="p.projectId"
              class="flex items-center justify-between bg-gray-800/50 rounded-lg px-3 py-1.5">
              <span class="text-sm text-white">{{ p.name }}</span>
              <button @click="store.unmapFromProject(mappingIdentity!.id, p.projectId); refreshMapping()"
                class="text-xs text-red-400 hover:text-red-300">Remove</button>
            </div>
            <p v-if="!mappingIdentity.projects.length" class="text-xs text-gray-600">No projects mapped.</p>
          </div>
          <div class="flex gap-2">
            <select v-model="selectedProjectId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="">Add project…</option>
              <option v-for="proj in availableProjects" :key="proj.id" :value="proj.id">{{ proj.name }}</option>
            </select>
            <button :disabled="!selectedProjectId" @click="addProjectMapping"
              class="px-3 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-40 text-white text-sm rounded-lg transition-colors">
              Add
            </button>
          </div>
        </div>

        <!-- Org mappings -->
        <div>
          <p class="text-sm font-medium text-gray-300 mb-2">Organizations</p>
          <div class="space-y-1 mb-2">
            <div v-for="o in mappingIdentity.orgs" :key="o.orgId"
              class="flex items-center justify-between bg-gray-800/50 rounded-lg px-3 py-1.5">
              <span class="text-sm text-white">{{ o.name }}</span>
              <button @click="store.unmapFromOrg(mappingIdentity!.id, o.orgId); refreshMapping()"
                class="text-xs text-red-400 hover:text-red-300">Remove</button>
            </div>
            <p v-if="!mappingIdentity.orgs.length" class="text-xs text-gray-600">No organizations mapped.</p>
          </div>
          <div class="flex gap-2">
            <select v-model="selectedOrgId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="">Add organization…</option>
              <option v-for="org in availableOrgs" :key="org.id" :value="org.id">{{ org.name }}</option>
            </select>
            <button :disabled="!selectedOrgId" @click="addOrgMapping"
              class="px-3 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-40 text-white text-sm rounded-lg transition-colors">
              Add
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useGitHubIdentitiesStore } from '~/stores/github-identities'
import { useAgentsStore } from '~/stores/agents'
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'
import type { GitHubIdentity } from '~/types'

const store = useGitHubIdentitiesStore()
const agentsStore = useAgentsStore()
const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()

onMounted(async () => {
  await Promise.all([
    store.fetchIdentities(),
    agentsStore.fetchAgents(),
    projectsStore.fetchProjects(),
    orgsStore.fetchOrgs(),
  ])
})

// --- Create ---
const showCreate = ref(false)
const creating = ref(false)
const createError = ref<string | null>(null)
const createForm = reactive({ name: '', token: '' })

function openCreate() {
  createForm.name = ''
  createForm.token = ''
  createError.value = null
  showCreate.value = true
}

async function handleCreate() {
  creating.value = true
  createError.value = null
  try {
    await store.createIdentity(createForm.token, createForm.name || undefined)
    if (store.error) {
      createError.value = store.error
    } else {
      showCreate.value = false
    }
  } finally {
    creating.value = false
  }
}

// --- Mapping ---
const mappingIdentity = ref<GitHubIdentity | null>(null)
const selectedAgentId = ref('')
const selectedProjectId = ref('')
const selectedOrgId = ref('')

function openMapping(identity: GitHubIdentity) {
  mappingIdentity.value = identity
  selectedAgentId.value = identity.agentId ?? ''
  selectedProjectId.value = ''
  selectedOrgId.value = ''
}

function refreshMapping() {
  if (!mappingIdentity.value) return
  const updated = store.identities.find(i => i.id === mappingIdentity.value!.id)
  if (updated) mappingIdentity.value = updated
}

async function saveAgentMapping() {
  if (!mappingIdentity.value) return
  if (selectedAgentId.value) {
    await store.mapToAgent(mappingIdentity.value.id, selectedAgentId.value)
  } else {
    await store.unmapFromAgent(mappingIdentity.value.id)
  }
  refreshMapping()
}

async function addProjectMapping() {
  if (!mappingIdentity.value || !selectedProjectId.value) return
  await store.mapToProject(mappingIdentity.value.id, selectedProjectId.value)
  selectedProjectId.value = ''
  refreshMapping()
}

async function addOrgMapping() {
  if (!mappingIdentity.value || !selectedOrgId.value) return
  await store.mapToOrg(mappingIdentity.value.id, selectedOrgId.value)
  selectedOrgId.value = ''
  refreshMapping()
}

const availableProjects = computed(() => {
  if (!mappingIdentity.value) return projectsStore.projects
  const mapped = new Set(mappingIdentity.value.projects.map(p => p.projectId))
  return projectsStore.projects.filter(p => !mapped.has(p.id))
})

const availableOrgs = computed(() => {
  if (!mappingIdentity.value) return orgsStore.orgs
  const mapped = new Set(mappingIdentity.value.orgs.map(o => o.orgId))
  return orgsStore.orgs.filter(o => !mapped.has(o.id))
})
</script>
