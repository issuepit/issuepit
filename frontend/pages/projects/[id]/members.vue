<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 text-sm text-gray-500 mb-4">
        <NuxtLink :to="`/projects/${id}`" class="hover:text-gray-300">{{ projectsStore.currentProject.name }}</NuxtLink>
        <span>/</span>
        <span class="text-gray-400">Settings</span>
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

      <!-- Actions bar -->
      <div class="flex items-center justify-between mb-4">
        <p class="text-gray-400 text-sm">{{ membersStore.members.length }} member{{ membersStore.members.length === 1 ? '' : 's' }}</p>
        <button
          class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
          @click="showAddModal = true"
        >
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add Member
        </button>
      </div>

      <!-- Loading members -->
      <div v-if="membersStore.loading" class="flex items-center justify-center py-12">
        <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
      </div>

      <!-- Members table -->
      <div v-else-if="membersStore.members.length" class="rounded-xl border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Principal</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Type</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Permissions</th>
              <th class="px-4 py-3" />
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="member in membersStore.members" :key="member.id" class="hover:bg-gray-900/50 transition-colors">
              <td class="px-4 py-3 text-white font-medium">
                <span v-if="member.user">{{ member.user.username }}</span>
                <span v-else-if="member.team">{{ member.team.name }}</span>
                <span v-else class="text-gray-500">—</span>
              </td>
              <td class="px-4 py-3">
                <span v-if="member.userId"
                  class="inline-flex items-center text-xs bg-blue-900/30 text-blue-400 px-2 py-0.5 rounded-full">
                  User
                </span>
                <span v-else
                  class="inline-flex items-center text-xs bg-purple-900/30 text-purple-400 px-2 py-0.5 rounded-full">
                  Team
                </span>
              </td>
              <td class="px-4 py-3">
                <div class="flex flex-wrap gap-1">
                  <span
                    v-for="(label, flag) in permissionFlags"
                    :key="flag"
                    :class="[
                      'text-xs px-1.5 py-0.5 rounded',
                      hasPermission(member.permissions, Number(flag))
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
                    @click="openEditMember(member)"
                  >
                    Edit
                  </button>
                  <button
                    class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                    @click="confirmRemoveMember(member)"
                  >
                    Remove
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Empty state -->
      <div v-else class="flex flex-col items-center justify-center py-16 text-center">
        <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
          <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No members yet</p>
        <p class="text-gray-600 text-sm mt-1">Add users or teams to grant access</p>
      </div>
    </template>

    <!-- Not found -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">Project not found</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>

    <!-- Add / Edit Modal -->
    <div v-if="showAddModal || editingMember" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingMember ? 'Edit Permissions' : 'Add Member' }}</h2>
        <form class="space-y-4" @submit.prevent="handleSubmit">
          <!-- Principal type (only when adding) -->
          <div v-if="!editingMember">
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Type</label>
            <div class="flex gap-3">
              <label class="flex items-center gap-2 cursor-pointer">
                <input v-model="form.type" type="radio" value="user" class="text-brand-500" />
                <span class="text-sm text-gray-300">User</span>
              </label>
              <label class="flex items-center gap-2 cursor-pointer">
                <input v-model="form.type" type="radio" value="team" class="text-brand-500" />
                <span class="text-sm text-gray-300">Team</span>
              </label>
            </div>
          </div>

          <div v-if="!editingMember">
            <label class="block text-sm font-medium text-gray-300 mb-1.5">
              {{ form.type === 'user' ? 'Search User' : 'Select Team' }}
            </label>
            <!-- User autocomplete -->
            <div v-if="form.type === 'user'" class="relative">
              <input
                v-model="userSearchQuery"
                type="text"
                required
                placeholder="Search by username…"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
                @input="onPrincipalSearch"
              />
              <div
                v-if="userSearchResults.length"
                class="absolute z-10 mt-1 w-full bg-gray-800 border border-gray-700 rounded-lg shadow-xl overflow-hidden"
              >
                <button
                  v-for="user in userSearchResults"
                  :key="user.id"
                  type="button"
                  class="w-full text-left px-3 py-2 hover:bg-gray-700 transition-colors"
                  @click="selectPrincipalUser(user)"
                >
                  <span class="text-white text-sm font-medium">{{ user.username }}</span>
                  <span class="text-gray-400 text-xs ml-2">{{ user.email }}</span>
                </button>
              </div>
              <p v-if="form.principalId" class="text-xs text-brand-400 mt-1">Selected: {{ userSearchQuery }}</p>
            </div>
            <!-- Team dropdown -->
            <select
              v-else
              v-model="form.principalId"
              required
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <option value="" disabled>Select a team…</option>
              <option v-for="team in orgTeams" :key="team.id" :value="team.id">{{ team.name }}</option>
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
                  :checked="hasPermission(form.permissions, Number(flag))"
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
              :disabled="saving"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ saving ? 'Saving…' : editingMember ? 'Update' : 'Add' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeModal"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Error -->
    <ToastError :error="membersStore.error" />
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useProjectMembersStore } from '~/stores/projectMembers'
import { useTeamsStore } from '~/stores/teams'
import { ProjectPermission, ProjectPermissionLabels } from '~/types'
import type { ProjectMember, Team, User } from '~/types'

const route = useRoute()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const membersStore = useProjectMembersStore()
const teamsStore = useTeamsStore()
const api = useApi()

const permissionFlags = ProjectPermissionLabels

// Teams available for the project's org (for autocomplete in add modal)
const orgTeams = computed<Team[]>(() => teamsStore.teams)

onMounted(async () => {
  await Promise.all([
    projectsStore.fetchProject(id),
    membersStore.fetchMembers(id),
  ])
  // Load org teams for the team autocomplete once the project is available
  if (projectsStore.currentProject?.orgId) {
    await teamsStore.fetchTeams(projectsStore.currentProject.orgId)
  }
})

function hasPermission(perms: ProjectPermission, flag: number): boolean {
  return (perms & flag) !== 0
}

const showAddModal = ref(false)
const editingMember = ref<ProjectMember | null>(null)
const saving = ref(false)

const userSearchQuery = ref('')
const userSearchResults = ref<User[]>([])

async function onPrincipalSearch() {
  form.principalId = ''
  if (!userSearchQuery.value.trim()) {
    userSearchResults.value = []
    return
  }
  try {
    const data = await api.get<User[]>(`/api/users/search?q=${encodeURIComponent(userSearchQuery.value)}`)
    userSearchResults.value = data
  } catch {
    userSearchResults.value = []
  }
}

function selectPrincipalUser(user: User) {
  form.principalId = user.id
  userSearchQuery.value = user.username
  userSearchResults.value = []
}

const form = reactive({
  type: 'user' as 'user' | 'team',
  principalId: '',
  permissions: ProjectPermission.Read,
})

watch(() => form.type, () => {
  form.principalId = ''
  userSearchQuery.value = ''
  userSearchResults.value = []
})

function openEditMember(member: ProjectMember) {
  editingMember.value = member
  form.permissions = member.permissions
}

function closeModal() {
  showAddModal.value = false
  editingMember.value = null
  userSearchQuery.value = ''
  userSearchResults.value = []
  Object.assign(form, { type: 'user', principalId: '', permissions: ProjectPermission.Read })
}

function togglePermission(flag: number) {
  if (hasPermission(form.permissions, flag)) {
    form.permissions = (form.permissions & ~flag) as ProjectPermission
  } else {
    form.permissions = (form.permissions | flag) as ProjectPermission
  }
}

async function handleSubmit() {
  saving.value = true
  try {
    if (editingMember.value) {
      await membersStore.updateMember(id, {
        userId: editingMember.value.userId,
        teamId: editingMember.value.teamId,
        permissions: form.permissions,
      })
    } else {
      await membersStore.addMember(id, {
        userId: form.type === 'user' ? form.principalId : undefined,
        teamId: form.type === 'team' ? form.principalId : undefined,
        permissions: form.permissions,
      })
    }
    closeModal()
  } finally {
    saving.value = false
  }
}

function confirmRemoveMember(member: ProjectMember) {
  const name = member.user?.username || member.team?.name || 'this member'
  if (confirm(`Remove "${name}" from the project?`)) {
    membersStore.removeMember(id, {
      userId: member.userId,
      teamId: member.teamId,
    })
  }
}
</script>
