<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
        <h1 class="text-xl font-bold text-white">Settings — {{ projectsStore.currentProject.name }}</h1>
      </div>

      <div class="space-y-6 max-w-2xl">
        <!-- General -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-4">General</h2>
          <form class="space-y-4" @submit.prevent="saveGeneral">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
              <input v-model="form.name" type="text" required
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Slug</label>
              <input v-model="form.slug" type="text" required
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
              <textarea v-model="form.description" rows="3"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">GitHub Repository URL</label>
              <input v-model="form.gitHubRepo" type="text" placeholder="https://github.com/org/repo"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <p v-if="saveGeneralError" class="text-red-400 text-sm">{{ saveGeneralError }}</p>
            <button type="submit" :disabled="savingGeneral"
              class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              {{ savingGeneral ? 'Saving…' : 'Save Changes' }}
            </button>
          </form>
        </div>

        <!-- Repository -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Repository</h2>
          <p class="text-sm text-gray-500 mb-4">Link a Git repository to this project</p>

          <div v-if="gitStore.loading" class="flex items-center justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>

          <form v-else class="space-y-3" @submit.prevent="saveRepo">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Remote URL</label>
              <input v-model="repoForm.remoteUrl" type="text" placeholder="https://github.com/org/repo.git or git@…"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Default branch</label>
              <input v-model="repoForm.defaultBranch" type="text" placeholder="main"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Username <span class="text-gray-500">(optional)</span></label>
              <input v-model="repoForm.authUsername" type="text" placeholder="git username"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Token / Password <span class="text-gray-500">(optional)</span></label>
              <input v-model="repoForm.authToken" type="password" placeholder="PAT or password"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <p v-if="gitStore.error" class="text-red-400 text-sm">{{ gitStore.error }}</p>
            <button type="submit" :disabled="gitStore.loading"
              class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              {{ gitStore.repo ? 'Update Repository' : 'Link Repository' }}
            </button>
          </form>
        </div>

        <!-- Runner Options -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Options</h2>
          <p class="text-sm text-gray-500 mb-4">Configure CI/CD runner behaviour for this project</p>
          <form class="space-y-4" @submit.prevent="saveRunnerOptions">
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">Mount repository in Docker</label>
                <p class="text-xs text-gray-500 mt-0.5">Bind the workspace directory into the runner container</p>
              </div>
              <button
                type="button"
                :class="runnerForm.mountRepositoryInDocker ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900"
                @click="runnerForm.mountRepositoryInDocker = !runnerForm.mountRepositoryInDocker">
                <span
                  :class="runnerForm.mountRepositoryInDocker ? 'translate-x-6' : 'translate-x-1'"
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" />
              </button>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Max concurrent runners
                <span class="text-gray-500 font-normal">(0 = unlimited)</span>
              </label>
              <input v-model.number="runnerForm.maxConcurrentRunners" type="number" min="0"
                class="w-40 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
            </div>
            <p v-if="saveRunnerError" class="text-red-400 text-sm">{{ saveRunnerError }}</p>
            <button type="submit" :disabled="savingRunner"
              class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              {{ savingRunner ? 'Saving…' : 'Save Runner Options' }}
            </button>
          </form>
        </div>

        <!-- Agents -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Agents</h2>
          <p class="text-sm text-gray-500 mb-4">Agents available to this project via linked MCP servers</p>
          <div v-if="loadingAgents" class="flex items-center justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
          <div v-else-if="projectAgents.length" class="space-y-2">
            <div v-for="agent in projectAgents" :key="agent.agentId"
              class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
              <div>
                <span class="text-sm text-white font-medium">{{ agent.agentName }}</span>
                <span class="text-xs text-gray-500 ml-2">via {{ agent.mcpServerName }}</span>
              </div>
              <NuxtLink to="/agents" class="text-xs text-brand-400 hover:text-brand-300">View</NuxtLink>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 py-2">
            No agents assigned. Link MCP servers to this project from
            <NuxtLink to="/config/mcp-servers" class="text-brand-400 hover:text-brand-300">Configuration → MCP Servers</NuxtLink>.
          </div>
        </div>

        <!-- Members -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <div>
              <h2 class="font-semibold text-white">Members</h2>
              <p class="text-sm text-gray-500">Users and teams with access to this project</p>
            </div>
            <NuxtLink :to="`/projects/${id}/members`"
              class="text-sm text-brand-400 hover:text-brand-300 transition-colors">
              Manage Members →
            </NuxtLink>
          </div>
          <div v-if="membersStore.loading" class="flex items-center justify-center py-4">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
          <div v-else-if="membersStore.members.length" class="space-y-2">
            <div v-for="member in membersStore.members.slice(0, 5)" :key="member.id"
              class="flex items-center gap-3 bg-gray-800 rounded-lg px-3 py-2">
              <span
                :class="member.userId ? 'bg-blue-900/30 text-blue-400' : 'bg-purple-900/30 text-purple-400'"
                class="text-xs px-1.5 py-0.5 rounded-full">
                {{ member.userId ? 'User' : 'Team' }}
              </span>
              <span class="text-sm text-white">{{ member.user?.username || member.team?.name || '—' }}</span>
            </div>
            <p v-if="membersStore.members.length > 5" class="text-xs text-gray-500 pl-1">
              +{{ membersStore.members.length - 5 }} more
            </p>
          </div>
          <div v-else class="text-sm text-gray-600 py-2">No members yet.</div>
        </div>

        <!-- Move to Organization -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Move to Organization</h2>
          <p class="text-sm text-gray-500 mb-4">Transfer this project to another organization within your tenant</p>
          <div class="flex gap-3">
            <select v-model="targetOrgId"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option value="" disabled>Select organization…</option>
              <option v-for="org in otherOrgs" :key="org.id" :value="org.id">{{ org.name }}</option>
            </select>
            <button :disabled="!targetOrgId || movingProject"
              class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors whitespace-nowrap"
              @click="moveToOrg">
              {{ movingProject ? 'Moving…' : 'Move Project' }}
            </button>
          </div>
          <p v-if="moveError" class="text-red-400 text-sm mt-2">{{ moveError }}</p>
        </div>

        <!-- Danger Zone -->
        <div class="bg-gray-900 border border-red-900/40 rounded-xl p-6">
          <h2 class="font-semibold text-red-400 mb-1">Danger Zone</h2>
          <p class="text-sm text-gray-500 mb-4">Permanently delete this project and all its data</p>
          <button
            class="bg-red-600 hover:bg-red-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
            @click="confirmDelete">
            Delete Project
          </button>
        </div>
      </div>
    </template>

    <!-- Not found -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">Project not found</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>

    <ToastError :error="projectsStore.error" />
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useProjectMembersStore } from '~/stores/projectMembers'
import { useOrgsStore } from '~/stores/orgs'
import { useGitStore } from '~/stores/git'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const membersStore = useProjectMembersStore()
const orgsStore = useOrgsStore()
const gitStore = useGitStore()

// ── General form ──────────────────────────────────────────────
const form = reactive({ name: '', slug: '', description: '', gitHubRepo: '' })
const savingGeneral = ref(false)
const saveGeneralError = ref<string | null>(null)

// ── Runner Options form ────────────────────────────────────────
const runnerForm = reactive({ mountRepositoryInDocker: true, maxConcurrentRunners: 0 })
const savingRunner = ref(false)
const saveRunnerError = ref<string | null>(null)

// ── Repo form ─────────────────────────────────────────────────
const repoForm = reactive({ remoteUrl: '', defaultBranch: 'main', authUsername: '', authToken: '' })

// ── Agents ───────────────────────────────────────────────────
const api = useApi()
const loadingAgents = ref(false)
const projectAgents = ref<Array<{ agentId: string; agentName: string; mcpServerName: string }>>([])

// ── Move to org ───────────────────────────────────────────────
const targetOrgId = ref('')
const movingProject = ref(false)
const moveError = ref<string | null>(null)

const otherOrgs = computed(() =>
  orgsStore.orgs.filter(o => o.id !== projectsStore.currentProject?.organizationId)
)

onMounted(async () => {
  await Promise.all([
    projectsStore.fetchProject(id),
    membersStore.fetchMembers(id),
    orgsStore.fetchOrgs(),
    gitStore.fetchRepo(id),
    fetchProjectAgents(),
  ])

  if (projectsStore.currentProject) {
    form.name = projectsStore.currentProject.name
    form.slug = projectsStore.currentProject.slug
    form.description = projectsStore.currentProject.description || ''
    form.gitHubRepo = projectsStore.currentProject.gitHubRepo || ''
    runnerForm.mountRepositoryInDocker = projectsStore.currentProject.mountRepositoryInDocker ?? true
    runnerForm.maxConcurrentRunners = projectsStore.currentProject.maxConcurrentRunners ?? 0
  }

  if (gitStore.repo) {
    repoForm.remoteUrl = gitStore.repo.remoteUrl
    repoForm.defaultBranch = gitStore.repo.defaultBranch
  }
})

async function fetchProjectAgents() {
  loadingAgents.value = true
  try {
    const servers = await api.get<Array<{
      id: string; name: string;
      linkedAgents: Array<{ agentId: string; name: string }>;
      linkedProjects: Array<{ projectId: string; name: string }>;
    }>>('/api/mcp-servers')

    const result: typeof projectAgents.value = []
    for (const server of servers) {
      const linked = server.linkedProjects?.some(p => p.projectId === id)
      if (linked) {
        for (const agent of server.linkedAgents ?? []) {
          if (!result.find(a => a.agentId === agent.agentId)) {
            result.push({ agentId: agent.agentId, agentName: agent.name, mcpServerName: server.name })
          }
        }
      }
    }
    projectAgents.value = result
  } catch {
    // silently ignore
  } finally {
    loadingAgents.value = false
  }
}

async function saveGeneral() {  savingGeneral.value = true
  saveGeneralError.value = null
  try {
    await projectsStore.updateProject(id, {
      name: form.name,
      slug: form.slug,
      description: form.description,
      gitHubRepo: form.gitHubRepo.trim() || undefined,
    })
  } catch (e: unknown) {
    saveGeneralError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    savingGeneral.value = false
  }
}

async function saveRunnerOptions() {
  savingRunner.value = true
  saveRunnerError.value = null
  try {
    await projectsStore.updateProject(id, {
      name: form.name,
      slug: form.slug,
      description: form.description,
      gitHubRepo: form.gitHubRepo.trim() || undefined,
      mountRepositoryInDocker: runnerForm.mountRepositoryInDocker,
      maxConcurrentRunners: runnerForm.maxConcurrentRunners,
    })
  } catch (e: unknown) {
    saveRunnerError.value = e instanceof Error ? e.message : 'Failed to save runner options'
  } finally {
    savingRunner.value = false
  }
}

async function saveRepo() {  if (!repoForm.remoteUrl) return
  const payload = {
    remoteUrl: repoForm.remoteUrl,
    defaultBranch: repoForm.defaultBranch || 'main',
    authUsername: repoForm.authUsername || undefined,
    authToken: repoForm.authToken || undefined,
  }
  if (gitStore.repo) {
    await gitStore.updateRepo(id, payload)
  } else {
    await gitStore.createRepo(id, payload)
  }
}

async function moveToOrg() {
  if (!targetOrgId.value) return
  movingProject.value = true
  moveError.value = null
  try {
    await projectsStore.moveProject(id, targetOrgId.value)
    targetOrgId.value = ''
  } catch (e: unknown) {
    moveError.value = e instanceof Error ? e.message : 'Failed to move project'
  } finally {
    movingProject.value = false
  }
}

async function confirmDelete() {
  if (!confirm(`Delete project "${projectsStore.currentProject?.name}"? This cannot be undone.`)) return
  await projectsStore.deleteProject(id)
  router.push('/projects')
}
</script>
