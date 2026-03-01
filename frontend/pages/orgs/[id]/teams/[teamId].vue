<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="teamsStore.loading && !team" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="team">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink :to="`/orgs/${orgId}`" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <div>
          <h1 class="text-2xl font-bold text-white">{{ team.name }}</h1>
          <p class="text-gray-500 text-sm font-mono">{{ team.slug }}</p>
        </div>
      </div>

      <!-- Tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            activeTab === tab.id
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
          @click="activeTab = tab.id"
        >
          {{ tab.label }}
        </button>
      </div>

      <!-- Members Tab -->
      <div v-if="activeTab === 'members'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ teamsStore.members.length }} member{{ teamsStore.members.length === 1 ? '' : 's' }}</p>
        </div>

        <div v-if="teamsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else>
          <div v-if="teamsStore.members.length" class="mb-4 rounded-xl border border-gray-800 overflow-hidden">
            <table class="w-full text-sm">
              <thead class="bg-gray-900">
                <tr>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">User</th>
                  <th class="text-left px-4 py-3 text-gray-400 font-medium">Email</th>
                  <th class="px-4 py-3" />
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="m in teamsStore.members" :key="m.userId" class="hover:bg-gray-900/50">
                  <td class="px-4 py-3 text-white font-medium">{{ m.user?.username }}</td>
                  <td class="px-4 py-3 text-gray-400 text-xs">{{ m.user?.email }}</td>
                  <td class="px-4 py-3 text-right">
                    <button
                      class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                      @click="removeTeamMember(m.userId)"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-else class="text-gray-500 text-sm mb-4">No members yet.</p>

          <!-- Add member by username -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4">
            <label class="block text-sm font-medium text-gray-300 mb-2">Add Member by Username</label>
            <div class="relative">
              <div class="flex gap-2">
                <input
                  v-model="memberSearchQuery"
                  type="text"
                  placeholder="Search by username…"
                  class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
                  @input="onMemberSearch"
                />
                <button
                  :disabled="!selectedUserId"
                  class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
                  @click="addTeamMember"
                >
                  Add
                </button>
              </div>
              <div
                v-if="memberSearchResults.length"
                class="absolute z-10 mt-1 w-full bg-gray-800 border border-gray-700 rounded-lg shadow-xl overflow-hidden"
              >
                <button
                  v-for="user in memberSearchResults"
                  :key="user.id"
                  type="button"
                  class="w-full text-left px-3 py-2 hover:bg-gray-700 transition-colors"
                  @click="selectUser(user)"
                >
                  <span class="text-white text-sm font-medium">{{ user.username }}</span>
                  <span class="text-gray-400 text-xs ml-2">{{ user.email }}</span>
                </button>
              </div>
            </div>
            <p v-if="selectedUserId" class="text-xs text-brand-400 mt-1.5">Selected: {{ memberSearchQuery }}</p>
          </div>
        </div>
      </div>

      <!-- Projects Tab -->
      <div v-if="activeTab === 'projects'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ teamsStore.teamProjects.length }} project{{ teamsStore.teamProjects.length === 1 ? '' : 's' }}</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="showAddProjectModal = true"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add to Project
          </button>
        </div>

        <div v-if="teamsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="teamsStore.teamProjects.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Project</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Permissions</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="pm in teamsStore.teamProjects" :key="pm.id" class="hover:bg-gray-900/50 transition-colors">
                <td class="px-4 py-3 text-white font-medium">{{ pm.project?.name }}</td>
                <td class="px-4 py-3">
                  <div class="flex flex-wrap gap-1">
                    <span
                      v-for="(label, flag) in permissionFlags"
                      :key="flag"
                      :class="[
                        'text-xs px-1.5 py-0.5 rounded',
                        hasPermission(pm.permissions, Number(flag))
                          ? 'bg-green-900/30 text-green-400'
                          : 'bg-gray-800 text-gray-600'
                      ]"
                    >
                      {{ label }}
                    </span>
                  </div>
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="flex items-center justify-end gap-2">
                    <button
                      class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                      @click="openEditProjectPermissions(pm)"
                    >
                      Edit
                    </button>
                    <button
                      class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                      @click="confirmRemoveFromProject(pm)"
                    >
                      Remove
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">Team has no project access yet</p>
          <p class="text-gray-600 text-sm mt-1">Add the team to a project to grant access</p>
        </div>
      </div>
    </template>

    <!-- Not found -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">Team not found</p>
      <NuxtLink :to="`/orgs/${orgId}`" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Organization</NuxtLink>
    </div>

    <!-- Add to Project Modal -->
    <div v-if="showAddProjectModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Add Team to Project</h2>
        <form class="space-y-4" @submit.prevent="handleAddToProject">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Project</label>
            <select
              v-model="projectForm.projectId"
              required
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <option value="">Select a project…</option>
              <option v-for="p in availableProjects" :key="p.id" :value="p.id">{{ p.name }}</option>
            </select>
          </div>
          <!-- Permissions checkboxes -->
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-2">Permissions</label>
            <div class="grid grid-cols-2 gap-2">
              <label
                v-for="(label, flag) in permissionFlags"
                :key="flag"
                class="flex items-center gap-2 cursor-pointer rounded-lg p-2 hover:bg-gray-800 transition-colors"
              >
                <input
                  type="checkbox"
                  :checked="hasPermission(projectForm.permissions, Number(flag))"
                  class="w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
                  @change="togglePermission(Number(flag))"
                />
                <span class="text-sm text-gray-300">{{ label }}</span>
              </label>
            </div>
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingProject || !projectForm.projectId"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingProject ? 'Saving…' : 'Add' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeProjectModal"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Edit Project Permissions Modal -->
    <div v-if="editingProjectMember" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Edit Permissions — {{ editingProjectMember.project?.name }}</h2>
        <form class="space-y-4" @submit.prevent="handleUpdatePermissions">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-2">Permissions</label>
            <div class="grid grid-cols-2 gap-2">
              <label
                v-for="(label, flag) in permissionFlags"
                :key="flag"
                class="flex items-center gap-2 cursor-pointer rounded-lg p-2 hover:bg-gray-800 transition-colors"
              >
                <input
                  type="checkbox"
                  :checked="hasPermission(editPermissions, Number(flag))"
                  class="w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
                  @change="toggleEditPermission(Number(flag))"
                />
                <span class="text-sm text-gray-300">{{ label }}</span>
              </label>
            </div>
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingProject"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingProject ? 'Saving…' : 'Update' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="editingProjectMember = null"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <ToastError :error="teamsStore.error || projectMembersStore.error" />
  </div>
</template>

<script setup lang="ts">
import { useTeamsStore } from '~/stores/teams'
import { useOrgsStore } from '~/stores/orgs'
import { useProjectMembersStore } from '~/stores/projectMembers'
import { ProjectPermission, ProjectPermissionLabels } from '~/types'
import type { Team, User, ProjectMember } from '~/types'

const route = useRoute()
const orgId = route.params.id as string
const teamId = route.params.teamId as string

const teamsStore = useTeamsStore()
const orgsStore = useOrgsStore()
const projectMembersStore = useProjectMembersStore()
const api = useApi()

const team = ref<Team | null>(null)
const permissionFlags = ProjectPermissionLabels

const tabs = [
  { id: 'members', label: 'Members' },
  { id: 'projects', label: 'Projects' },
]
const activeTab = ref('members')

onMounted(async () => {
  const teams = await teamsStore.fetchTeams(orgId)
  team.value = teamsStore.teams.find(t => t.id === teamId) || null
  await Promise.all([
    teamsStore.fetchMembers(orgId, teamId),
    teamsStore.fetchTeamProjects(orgId, teamId),
    orgsStore.fetchOrgProjects(orgId),
  ])
})

watch(activeTab, async (tab) => {
  if (tab === 'members') await teamsStore.fetchMembers(orgId, teamId)
  if (tab === 'projects') await teamsStore.fetchTeamProjects(orgId, teamId)
})

// --- Members ---
const memberSearchQuery = ref('')
const memberSearchResults = ref<User[]>([])
const selectedUserId = ref('')

async function onMemberSearch() {
  selectedUserId.value = ''
  if (!memberSearchQuery.value.trim()) {
    memberSearchResults.value = []
    return
  }
  try {
    const data = await api.get<User[]>(`/api/users/search?q=${encodeURIComponent(memberSearchQuery.value)}`)
    memberSearchResults.value = data
  } catch {
    memberSearchResults.value = []
  }
}

function selectUser(user: User) {
  selectedUserId.value = user.id
  memberSearchQuery.value = user.username
  memberSearchResults.value = []
}

async function addTeamMember() {
  if (!selectedUserId.value) return
  await teamsStore.addMember(orgId, teamId, selectedUserId.value)
  selectedUserId.value = ''
  memberSearchQuery.value = ''
}

async function removeTeamMember(userId: string) {
  if (confirm('Remove this member from the team?')) {
    await teamsStore.removeMember(orgId, teamId, userId)
  }
}

// --- Projects ---
const showAddProjectModal = ref(false)
const editingProjectMember = ref<ProjectMember | null>(null)
const savingProject = ref(false)
const projectForm = reactive({ projectId: '', permissions: ProjectPermission.Read })
const editPermissions = ref<ProjectPermission>(ProjectPermission.Read)

const availableProjects = computed(() => {
  const assignedIds = new Set(teamsStore.teamProjects.map(pm => pm.projectId))
  return orgsStore.orgProjects.filter(p => !assignedIds.has(p.id))
})

function hasPermission(perms: ProjectPermission, flag: number): boolean {
  return (perms & flag) !== 0
}

function togglePermission(flag: number) {
  if (hasPermission(projectForm.permissions, flag)) {
    projectForm.permissions = (projectForm.permissions & ~flag) as ProjectPermission
  } else {
    projectForm.permissions = (projectForm.permissions | flag) as ProjectPermission
  }
}

function toggleEditPermission(flag: number) {
  if (hasPermission(editPermissions.value, flag)) {
    editPermissions.value = (editPermissions.value & ~flag) as ProjectPermission
  } else {
    editPermissions.value = (editPermissions.value | flag) as ProjectPermission
  }
}

function openEditProjectPermissions(pm: ProjectMember) {
  editingProjectMember.value = pm
  editPermissions.value = pm.permissions
}

function closeProjectModal() {
  showAddProjectModal.value = false
  Object.assign(projectForm, { projectId: '', permissions: ProjectPermission.Read })
}

async function handleAddToProject() {
  if (!projectForm.projectId) return
  savingProject.value = true
  try {
    await projectMembersStore.addMember(projectForm.projectId, {
      teamId,
      permissions: projectForm.permissions,
    })
    await teamsStore.fetchTeamProjects(orgId, teamId)
    closeProjectModal()
  } finally {
    savingProject.value = false
  }
}

async function handleUpdatePermissions() {
  if (!editingProjectMember.value) return
  savingProject.value = true
  try {
    await projectMembersStore.updateMember(editingProjectMember.value.projectId, {
      teamId,
      permissions: editPermissions.value,
    })
    await teamsStore.fetchTeamProjects(orgId, teamId)
    editingProjectMember.value = null
  } finally {
    savingProject.value = false
  }
}

async function confirmRemoveFromProject(pm: ProjectMember) {
  if (confirm(`Remove team from project "${pm.project?.name}"?`)) {
    await projectMembersStore.removeMember(pm.projectId, { teamId })
    await teamsStore.fetchTeamProjects(orgId, teamId)
  }
}
</script>
