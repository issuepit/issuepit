<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-4">
        <NuxtLink :to="`/projects/${id}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <h1 class="text-xl font-bold text-white">{{ projectsStore.currentProject.name }}</h1>
      </div>

      <!-- Tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <NuxtLink
          :to="`/projects/${id}/settings`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/settings`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >Settings</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/ci-cd`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/ci-cd`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >CI/CD</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/members`"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            $route.path === `/projects/${id}/members`
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
        >Members</NuxtLink>
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
            <div class="flex items-center justify-between">
              <div>
                <label class="block text-sm font-medium text-gray-300">Common Agenda</label>
                <p class="text-xs text-gray-500 mt-0.5">Mark this project as the org-wide common agenda — a shared goal tracker across all projects</p>
              </div>
              <button
                type="button"
                :class="form.isAgenda ? 'bg-brand-600' : 'bg-gray-700'"
                class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full transition-colors duration-200"
                @click="form.isAgenda = !form.isAgenda"
              >
                <span
                  :class="form.isAgenda ? 'translate-x-4' : 'translate-x-0.5'"
                  class="inline-block h-4 w-4 mt-0.5 rounded-full bg-white transition-transform duration-200"
                />
              </button>
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
          <div class="flex items-center justify-between mb-1">
            <h2 class="font-semibold text-white">Git Origins</h2>
            <button
              class="text-xs text-brand-400 hover:text-brand-300 px-3 py-1.5 rounded-md border border-brand-900/30 hover:bg-brand-900/20 transition-colors"
              @click="openAddRepo"
            >
              Add Origin
            </button>
          </div>
          <p class="text-sm text-gray-500 mb-4">Link one or more Git remotes to this project</p>

          <div v-if="gitStore.loading" class="flex items-center justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>

          <!-- Repo list -->
          <div v-else-if="gitStore.repos.length" class="space-y-3">
            <div v-for="r in gitStore.repos" :key="r.id"
              class="bg-gray-800 rounded-lg p-3 space-y-2">
              <!-- Header row -->
              <div class="flex items-start justify-between gap-2">
                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-sm font-mono text-gray-200 truncate max-w-xs">{{ r.remoteUrl }}</span>
                    <span class="text-xs px-1.5 py-0.5 rounded-full font-medium"
                      :class="modeClasses(r.mode)">
                      {{ r.mode }}
                    </span>
                    <span v-if="r.status !== 'Active'" class="text-xs px-1.5 py-0.5 rounded-full"
                      :class="r.status === 'Disabled' ? 'bg-red-900/40 text-red-400' : 'bg-yellow-900/40 text-yellow-400'">
                      {{ r.status }}
                    </span>
                  </div>
                  <p class="text-xs text-gray-500 mt-0.5">Branch: {{ r.defaultBranch }} · {{ r.hasAuth ? 'auth configured' : 'no auth' }}</p>
                  <p v-if="r.statusMessage" class="text-xs text-gray-400 break-words mt-0.5">{{ r.statusMessage }}</p>
                  <p v-if="r.status === 'Throttled' && r.throttledUntil" class="text-xs text-gray-400 mt-0.5">
                    Polling resumes at {{ new Date(r.throttledUntil).toLocaleString() }}
                  </p>
                </div>
                <div class="flex items-center gap-1 shrink-0">
                  <button v-if="r.status !== 'Active'"
                    :disabled="gitStore.loading"
                    class="text-xs px-2 py-1 rounded border border-gray-700 text-green-400 hover:text-green-300 hover:bg-gray-700 transition-colors"
                    @click="enableRepoById(r)">
                    Re-enable
                  </button>
                  <button
                    class="text-xs px-2 py-1 rounded border border-gray-700 text-gray-400 hover:text-gray-200 hover:bg-gray-700 transition-colors"
                    @click="openEditRepo(r)">
                    Edit
                  </button>
                  <button
                    class="text-xs px-2 py-1 rounded border border-red-900/30 text-red-400 hover:text-red-300 hover:bg-red-900/20 transition-colors"
                    @click="removeRepo(r.id)">
                    Remove
                  </button>
                </div>
              </div>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 py-2">
            No git origins configured. Click <span class="text-brand-400">Add Origin</span> to link one.
          </div>

          <p v-if="gitStore.error" class="text-red-400 text-sm mt-3">{{ gitStore.error }}</p>
        </div>

        <!-- Add / Edit Repo modal -->
        <div v-if="showRepoModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" @click.self="showRepoModal = false">
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 w-full max-w-md shadow-xl space-y-4">
            <h3 class="font-semibold text-white">{{ editingRepoId ? 'Edit Git Origin' : 'Add Git Origin' }}</h3>
            <form class="space-y-3" @submit.prevent="saveRepo">
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">Remote URL</label>
                <input v-model="repoForm.remoteUrl" type="text" required placeholder="https://github.com/org/repo.git or git@…"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">Default branch</label>
                <input v-model="repoForm.defaultBranch" type="text" placeholder="main"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">Mode</label>
                <select v-model="repoForm.mode"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
                  <option value="Working">Working – agents push branches &amp; PRs here</option>
                  <option value="ReadOnly">Read-only – fetch only, never pushed to</option>
                  <option value="Release">Release – only main branch is pushed after merge</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">Username <span class="text-gray-500">(optional)</span></label>
                <input v-model="repoForm.authUsername" type="text" placeholder="git username"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">Token / Password <span class="text-gray-500">(optional)</span></label>
                <input v-model="repoForm.authToken" type="password" placeholder="PAT or password (leave blank to keep existing)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
              <p v-if="gitStore.error" class="text-red-400 text-sm">{{ gitStore.error }}</p>
              <div class="flex justify-end gap-2 pt-1">
                <button type="button" @click="showRepoModal = false"
                  class="text-sm px-4 py-2 rounded-lg border border-gray-700 text-gray-400 hover:text-gray-200 hover:bg-gray-800 transition-colors">
                  Cancel
                </button>
                <button type="submit" :disabled="gitStore.loading"
                  class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
                  {{ gitStore.loading ? 'Saving…' : (editingRepoId ? 'Save Changes' : 'Add Origin') }}
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Agents -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <div class="flex items-center justify-between mb-1">
            <h2 class="font-semibold text-white">Agents</h2>
            <button
              class="text-xs text-brand-400 hover:text-brand-300 px-3 py-1.5 rounded-md border border-brand-900/30 hover:bg-brand-900/20 transition-colors"
              @click="showLinkAgentModal = true"
            >
              Link Agent
            </button>
          </div>
          <p class="text-sm text-gray-500 mb-4">Agents available to this project</p>
          <div v-if="loadingAgents" class="flex items-center justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
          <div v-else-if="projectAgents.length" class="space-y-2">
            <div v-for="agent in projectAgents" :key="agent.agentId"
              class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2">
              <div>
                <span class="text-sm text-white font-medium">{{ agent.name }}</span>
                <span class="text-xs px-1.5 py-0.5 rounded-full ml-2"
                  :class="agent.source === 'org' ? 'bg-purple-900/40 text-purple-400' : 'bg-indigo-900/40 text-indigo-400'">
                  {{ agent.source === 'org' ? 'Via org' : 'Direct' }}
                </span>
                <span v-if="agent.isDisabled" class="text-xs text-red-400 ml-1">(disabled)</span>
              </div>
              <div class="flex items-center gap-2">
                <button
                  @click="toggleProjectAgent(agent.agentId, agent.isDisabled)"
                  :class="agent.isDisabled ? 'text-green-400 hover:text-green-300' : 'text-yellow-400 hover:text-yellow-300'"
                  class="text-xs px-2 py-1 rounded border border-gray-700 hover:bg-gray-700 transition-colors"
                >
                  {{ agent.isDisabled ? 'Enable' : 'Disable' }}
                </button>
                <button v-if="agent.source === 'project'"
                  @click="unlinkProjectAgent(agent.agentId)"
                  class="text-xs text-red-400 hover:text-red-300 px-2 py-1 rounded border border-red-900/30 hover:bg-red-900/20 transition-colors"
                >
                  Unlink
                </button>
              </div>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 py-2">
            No agents assigned. Link agents directly or via
            <NuxtLink to="/config/mcp-servers" class="text-brand-400 hover:text-brand-300">Agents → MCP Servers</NuxtLink>
            or manage org-level agents in the
            <NuxtLink :to="`/orgs/${projectsStore.currentProject?.organizationId}`" class="text-brand-400 hover:text-brand-300">Organization</NuxtLink>.
          </div>
        </div>

        <!-- MCP Servers -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <div class="flex items-center justify-between mb-1">
            <h2 class="font-semibold text-white">MCP Servers</h2>
            <button
              class="text-xs text-brand-400 hover:text-brand-300 px-3 py-1.5 rounded-md border border-brand-900/30 hover:bg-brand-900/20 transition-colors"
              @click="openCreateProjectMcp"
            >
              Add MCP Server
            </button>
          </div>
          <p class="text-sm text-gray-500 mb-4">MCP servers linked to this project</p>
          <div v-if="loadingProjectMcp" class="flex items-center justify-center py-6">
            <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>
          <div v-else-if="projectMcpServers.length" class="space-y-3">
            <div v-for="server in projectMcpServers" :key="server.mcpServerId"
              class="bg-gray-800 rounded-lg px-3 py-3">
              <div class="flex items-center justify-between mb-1">
                <span class="text-sm text-white font-medium">{{ server.name }}</span>
                <span v-if="server.enabledAgents.length" class="text-xs text-indigo-400">
                  {{ server.enabledAgents.length }} agent{{ server.enabledAgents.length !== 1 ? 's' : '' }} selected
                </span>
                <span v-else class="text-xs text-gray-500">All agents</span>
              </div>
              <p v-if="server.description" class="text-xs text-gray-500 mb-1">{{ server.description }}</p>
              <code class="text-xs text-green-300 font-mono">{{ server.url }}</code>
              <div v-if="server.enabledAgents.length" class="flex flex-wrap gap-1 mt-2">
                <span v-for="agent in server.enabledAgents" :key="agent.agentId"
                  class="text-xs bg-indigo-900/30 text-indigo-300 px-1.5 py-0.5 rounded">{{ agent.name }}</span>
              </div>
            </div>
          </div>
          <div v-else class="text-sm text-gray-600 py-2">
            No MCP servers linked. Add one above or link from
            <NuxtLink to="/config/mcp-servers" class="text-brand-400 hover:text-brand-300">Agents → MCP Servers</NuxtLink>.
          </div>
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

    <!-- Link Agent Modal -->
    <div v-if="showLinkAgentModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Link Agent to Project</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Agent</label>
            <select v-model="selectedLinkAgentId"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="" disabled>Select agent…</option>
              <option v-for="agent in availableAgentsToLink" :key="agent.id" :value="agent.id">{{ agent.name }}</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button :disabled="!selectedLinkAgentId || linkingAgent" @click="linkAgent"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ linkingAgent ? 'Linking…' : 'Link Agent' }}
          </button>
          <button @click="showLinkAgentModal = false; selectedLinkAgentId = ''"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Create Project MCP Server Modal -->
    <div v-if="showCreateMcpModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl max-h-[90vh] overflow-y-auto">
        <h2 class="text-lg font-bold text-white mb-5">Add MCP Server to Project</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name <span class="text-red-400">*</span></label>
            <input v-model="mcpForm.name" type="text" required placeholder="e.g. GitHub MCP"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <input v-model="mcpForm.description" type="text" placeholder="Optional description"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">URL <span class="text-red-400">*</span></label>
            <input v-model="mcpForm.url" type="text" required placeholder="https://mcp.example.com/sse"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Allowed Tools <span class="text-gray-500 font-normal">(comma-separated, empty = all)</span></label>
            <input v-model="mcpForm.allowedTools" type="text" placeholder="read_file, write_file"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button :disabled="!mcpForm.name || !mcpForm.url || savingMcp" @click="createProjectMcpServer"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ savingMcp ? 'Creating…' : 'Create & Link' }}
          </button>
          <button @click="showCreateMcpModal = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'
import { useGitStore } from '~/stores/git'
import { useAgentsStore } from '~/stores/agents'
import { useMcpServersStore } from '~/stores/mcp-servers'
import type { AgentProject, ProjectMcpServer, GitRepository, GitOriginMode } from '~/types'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()
const gitStore = useGitStore()
const agentsStore = useAgentsStore()
const mcpServersStore = useMcpServersStore()

// ── General form ──────────────────────────────────────────────
const form = reactive({ name: '', slug: '', description: '', gitHubRepo: '', isAgenda: false })
const savingGeneral = ref(false)
const saveGeneralError = ref<string | null>(null)

// ── Repo form (multi-origin) ──────────────────────────────────
const showRepoModal = ref(false)
const editingRepoId = ref<string | null>(null)
const repoForm = reactive<{ remoteUrl: string; defaultBranch: string; authUsername: string; authToken: string; mode: GitOriginMode }>({
  remoteUrl: '',
  defaultBranch: 'main',
  authUsername: '',
  authToken: '',
  mode: 'Working',
})

function modeClasses(mode: GitOriginMode) {
  if (mode === 'Working') return 'bg-brand-900/40 text-brand-400'
  if (mode === 'Release') return 'bg-purple-900/40 text-purple-400'
  return 'bg-gray-700/60 text-gray-400'
}

function openAddRepo() {
  editingRepoId.value = null
  Object.assign(repoForm, { remoteUrl: '', defaultBranch: 'main', authUsername: '', authToken: '', mode: 'Working' })
  gitStore.error = null
  showRepoModal.value = true
}

function openEditRepo(r: GitRepository) {
  editingRepoId.value = r.id
  Object.assign(repoForm, {
    remoteUrl: r.remoteUrl,
    defaultBranch: r.defaultBranch,
    authUsername: '',
    authToken: '',
    mode: r.mode,
  })
  gitStore.error = null
  showRepoModal.value = true
}

async function saveRepo() {
  if (!repoForm.remoteUrl) return
  const payload = {
    remoteUrl: repoForm.remoteUrl,
    defaultBranch: repoForm.defaultBranch || 'main',
    authUsername: repoForm.authUsername || undefined,
    authToken: repoForm.authToken || undefined,
    mode: repoForm.mode,
  }
  if (editingRepoId.value) {
    await gitStore.updateRepo(id, editingRepoId.value, payload)
  } else {
    await gitStore.addRepo(id, payload)
  }
  if (!gitStore.error) showRepoModal.value = false
}

async function removeRepo(repoId: string) {
  if (!confirm('Remove this git origin from the project?')) return
  await gitStore.deleteRepo(id, repoId)
}

async function enableRepoById(r: GitRepository) {
  await gitStore.enableRepo(id, r.id)
}

// ── Agents ───────────────────────────────────────────────────
const loadingAgents = ref(false)
const projectAgents = ref<AgentProject[]>([])
const showLinkAgentModal = ref(false)
const selectedLinkAgentId = ref('')
const linkingAgent = ref(false)

const availableAgentsToLink = computed(() => {
  const linked = new Set(projectAgents.value.map(a => a.agentId))
  return agentsStore.agents.filter(a => !linked.has(a.id))
})

async function fetchProjectAgents() {
  loadingAgents.value = true
  try {
    projectAgents.value = await agentsStore.fetchProjectAgents(id)
  } finally {
    loadingAgents.value = false
  }
}

async function toggleProjectAgent(agentId: string, isCurrentlyDisabled: boolean) {
  try {
    await agentsStore.setProjectAgentActive(id, agentId, isCurrentlyDisabled)
    await fetchProjectAgents()
  } catch {
    // silently ignore
  }
}

async function unlinkProjectAgent(agentId: string) {
  try {
    await agentsStore.unlinkAgentFromProject(id, agentId)
    await fetchProjectAgents()
  } catch {
    // silently ignore
  }
}

async function linkAgent() {
  if (!selectedLinkAgentId.value) return
  linkingAgent.value = true
  try {
    await agentsStore.linkAgentToProject(id, selectedLinkAgentId.value)
    selectedLinkAgentId.value = ''
    showLinkAgentModal.value = false
    await fetchProjectAgents()
  } catch {
    // silently ignore
  } finally {
    linkingAgent.value = false
  }
}

// ── Project MCP Servers ───────────────────────────────────────
const loadingProjectMcp = ref(false)
const projectMcpServers = ref<ProjectMcpServer[]>([])
const showCreateMcpModal = ref(false)
const savingMcp = ref(false)
const mcpForm = reactive({ name: '', description: '', url: '', configuration: '{}', allowedTools: '' })

async function fetchProjectMcpServers() {
  loadingProjectMcp.value = true
  try {
    projectMcpServers.value = await mcpServersStore.fetchProjectMcpServers(id)
  } finally {
    loadingProjectMcp.value = false
  }
}

function openCreateProjectMcp() {
  Object.assign(mcpForm, { name: '', description: '', url: '', configuration: '{}', allowedTools: '' })
  showCreateMcpModal.value = true
}

async function createProjectMcpServer() {
  if (!mcpForm.name || !mcpForm.url) return
  savingMcp.value = true
  try {
    const allowedTools = mcpForm.allowedTools.split(',').map(t => t.trim()).filter(Boolean)
    await mcpServersStore.createProjectMcpServer(id, {
      name: mcpForm.name,
      description: mcpForm.description || undefined,
      url: mcpForm.url,
      configuration: mcpForm.configuration || '{}',
      allowedTools,
    })
    showCreateMcpModal.value = false
    await fetchProjectMcpServers()
  } finally {
    savingMcp.value = false
  }
}

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
    orgsStore.fetchOrgs(),
    gitStore.fetchRepos(id),
    agentsStore.fetchAgents(),
    fetchProjectAgents(),
    fetchProjectMcpServers(),
  ])

  if (projectsStore.currentProject) {
    form.name = projectsStore.currentProject.name
    form.slug = projectsStore.currentProject.slug
    form.description = projectsStore.currentProject.description || ''
    form.gitHubRepo = projectsStore.currentProject.gitHubRepo || ''
    form.isAgenda = projectsStore.currentProject.isAgenda ?? false
  }
})

async function saveGeneral() {
  savingGeneral.value = true
  saveGeneralError.value = null
  try {
    await projectsStore.updateProject(id, {
      name: form.name,
      slug: form.slug,
      description: form.description,
      gitHubRepo: form.gitHubRepo.trim() || undefined,
      isAgenda: form.isAgenda,
    })
  } catch (e: unknown) {
    saveGeneralError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    savingGeneral.value = false
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
