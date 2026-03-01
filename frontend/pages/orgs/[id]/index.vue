<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="orgsStore.loading && !orgsStore.currentOrg" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="orgsStore.currentOrg">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink to="/orgs" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <div>
          <h1 class="text-2xl font-bold text-white">{{ orgsStore.currentOrg.name }}</h1>
          <p class="text-gray-500 text-sm font-mono">{{ orgsStore.currentOrg.slug }}</p>
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

      <!-- Teams Tab -->
      <div v-if="activeTab === 'teams'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ teamsStore.teams.length }} team{{ teamsStore.teams.length === 1 ? '' : 's' }}</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="openCreateTeam"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New Team
          </button>
        </div>

        <div v-if="teamsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="teamsStore.teams.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Slug</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr
                v-for="team in teamsStore.teams"
                :key="team.id"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="openTeamMembers(team)"
              >
                <td class="px-4 py-3 text-white font-medium">{{ team.name }}</td>
                <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ team.slug }}</td>
                <td class="px-4 py-3 text-gray-400">{{ formatDate(team.createdAt) }}</td>
                <td class="px-4 py-3 text-right" @click.stop>
                  <div class="flex items-center justify-end gap-2">
                    <button
                      class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                      @click="openEditTeam(team)"
                    >
                      Edit
                    </button>
                    <button
                      class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                      @click="confirmDeleteTeam(team.id, team.name)"
                    >
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
            <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <p class="text-gray-400 font-medium">No teams yet</p>
          <p class="text-gray-600 text-sm mt-1">Create your first team</p>
        </div>
      </div>

      <!-- Members Tab -->
      <div v-if="activeTab === 'members'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ orgsStore.members.length }} member{{ orgsStore.members.length === 1 ? '' : 's' }}</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="showAddMember = true"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add Member
          </button>
        </div>

        <div v-if="orgsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="orgsStore.members.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">User</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Email</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Role</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="member in orgsStore.members" :key="member.userId" class="hover:bg-gray-900/50 transition-colors">
                <td class="px-4 py-3 text-white font-medium">{{ member.user?.username }}</td>
                <td class="px-4 py-3 text-gray-400 text-xs">{{ member.user?.email }}</td>
                <td class="px-4 py-3">
                  <select
                    :value="member.role"
                    class="bg-gray-800 border border-gray-700 rounded px-2 py-1 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    @change="updateMemberRole(member.userId, Number(($event.target as HTMLSelectElement).value))"
                  >
                    <option v-for="(label, val) in OrgRoleLabels" :key="val" :value="val">{{ label }}</option>
                  </select>
                </td>
                <td class="px-4 py-3 text-right">
                  <button
                    class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                    @click="confirmRemoveMember(member.userId, member.user?.username || member.userId)"
                  >
                    Remove
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No members yet</p>
        </div>
      </div>
    </template>

    <!-- Not found / Error -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">{{ orgsStore.error || 'Organization not found' }}</p>
      <NuxtLink to="/orgs" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Organizations</NuxtLink>
    </div>

    <!-- Team Create/Edit Modal -->
    <div v-if="showTeamModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingTeamId ? 'Edit Team' : 'New Team' }}</h2>
        <form class="space-y-4" @submit.prevent="handleTeamSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input
              v-model="teamForm.name"
              type="text"
              required
              placeholder="Engineering"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Slug</label>
            <input
              v-model="teamForm.slug"
              type="text"
              required
              placeholder="engineering"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingTeam"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingTeam ? 'Saving…' : editingTeamId ? 'Update' : 'Create' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeTeamModal"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Team Members Modal -->
    <div v-if="showTeamMembersModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <div class="flex items-center justify-between mb-5">
          <h2 class="text-lg font-bold text-white">{{ selectedTeam?.name }} — Members</h2>
          <button class="text-gray-500 hover:text-gray-300 transition-colors" @click="showTeamMembersModal = false">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div v-if="teamsStore.loading" class="flex items-center justify-center py-8">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else>
          <div v-if="teamsStore.members.length" class="mb-4 rounded-lg border border-gray-800 overflow-hidden">
            <table class="w-full text-sm">
              <thead class="bg-gray-900">
                <tr>
                  <th class="text-left px-4 py-2.5 text-gray-400 font-medium">User</th>
                  <th class="text-left px-4 py-2.5 text-gray-400 font-medium">Email</th>
                  <th class="px-4 py-2.5" />
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="m in teamsStore.members" :key="m.userId" class="hover:bg-gray-900/50">
                  <td class="px-4 py-2.5 text-white">{{ m.user?.username }}</td>
                  <td class="px-4 py-2.5 text-gray-400 text-xs">{{ m.user?.email }}</td>
                  <td class="px-4 py-2.5 text-right">
                    <button
                      class="text-xs text-red-400 hover:text-red-300 transition-colors"
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

          <!-- Add member to team -->
          <div class="flex gap-2">
            <input
              v-model="newTeamMemberUserId"
              type="text"
              placeholder="User ID"
              class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
            <button
              class="bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
              @click="addTeamMember"
            >
              Add
            </button>
          </div>
          <p class="text-xs text-gray-500 mt-1.5">Enter the user's UUID to add them to the team.</p>
        </div>
      </div>
    </div>

    <!-- Add Org Member Modal -->
    <div v-if="showAddMember" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Add Member</h2>
        <form class="space-y-4" @submit.prevent="handleAddMember">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">User ID</label>
            <input
              v-model="addMemberForm.userId"
              type="text"
              required
              placeholder="User UUID"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Role</label>
            <select
              v-model="addMemberForm.role"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <option v-for="(label, val) in OrgRoleLabels" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingMember"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingMember ? 'Adding…' : 'Add Member' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="showAddMember = false"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Error toast for secondary operations (teams, members) -->
    <ToastError v-if="orgsStore.currentOrg" :error="orgsStore.error || teamsStore.error" />
  </div>
</template>

<script setup lang="ts">
import { useOrgsStore } from '~/stores/orgs'
import { useTeamsStore } from '~/stores/teams'
import { OrgRole, OrgRoleLabels } from '~/types'
import type { Team } from '~/types'

const route = useRoute()
const orgId = route.params.id as string

const orgsStore = useOrgsStore()
const teamsStore = useTeamsStore()

const tabs = [
  { id: 'teams', label: 'Teams' },
  { id: 'members', label: 'Members' },
]
const activeTab = ref('teams')

onMounted(async () => {
  await Promise.all([
    orgsStore.fetchOrg(orgId),
    teamsStore.fetchTeams(orgId),
    orgsStore.fetchMembers(orgId),
  ])
})

watch(activeTab, async (tab) => {
  if (tab === 'members') await orgsStore.fetchMembers(orgId)
  if (tab === 'teams') await teamsStore.fetchTeams(orgId)
})

// --- Teams ---
const showTeamModal = ref(false)
const editingTeamId = ref<string | null>(null)
const savingTeam = ref(false)
const teamForm = reactive({ name: '', slug: '' })

watch(() => teamForm.name, (val) => {
  if (!editingTeamId.value) {
    teamForm.slug = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  }
})

function openCreateTeam() {
  editingTeamId.value = null
  Object.assign(teamForm, { name: '', slug: '' })
  showTeamModal.value = true
}

function openEditTeam(team: Team) {
  editingTeamId.value = team.id
  Object.assign(teamForm, { name: team.name, slug: team.slug })
  showTeamModal.value = true
}

function closeTeamModal() {
  showTeamModal.value = false
  editingTeamId.value = null
}

async function handleTeamSubmit() {
  if (!teamForm.name || !teamForm.slug) return
  savingTeam.value = true
  try {
    if (editingTeamId.value) {
      await teamsStore.updateTeam(orgId, editingTeamId.value, { name: teamForm.name, slug: teamForm.slug })
    } else {
      await teamsStore.createTeam(orgId, { name: teamForm.name, slug: teamForm.slug })
    }
    closeTeamModal()
  } finally {
    savingTeam.value = false
  }
}

function confirmDeleteTeam(id: string, name: string) {
  if (confirm(`Delete team "${name}"? This cannot be undone.`)) {
    teamsStore.deleteTeam(orgId, id)
  }
}

// --- Team Members Modal ---
const showTeamMembersModal = ref(false)
const selectedTeam = ref<Team | null>(null)
const newTeamMemberUserId = ref('')

async function openTeamMembers(team: Team) {
  selectedTeam.value = team
  showTeamMembersModal.value = true
  await teamsStore.fetchMembers(orgId, team.id)
}

async function addTeamMember() {
  if (!newTeamMemberUserId.value || !selectedTeam.value) return
  await teamsStore.addMember(orgId, selectedTeam.value.id, newTeamMemberUserId.value)
  newTeamMemberUserId.value = ''
}

async function removeTeamMember(userId: string) {
  if (!selectedTeam.value) return
  if (confirm('Remove this member from the team?')) {
    await teamsStore.removeMember(orgId, selectedTeam.value.id, userId)
  }
}

// --- Org Members ---
const showAddMember = ref(false)
const savingMember = ref(false)
const addMemberForm = reactive({ userId: '', role: OrgRole.Member })

async function handleAddMember() {
  if (!addMemberForm.userId) return
  savingMember.value = true
  try {
    await orgsStore.addMember(orgId, addMemberForm.userId, addMemberForm.role)
    showAddMember.value = false
    Object.assign(addMemberForm, { userId: '', role: OrgRole.Member })
  } finally {
    savingMember.value = false
  }
}

async function updateMemberRole(userId: string, role: OrgRole) {
  await orgsStore.updateMember(orgId, userId, role)
}

function confirmRemoveMember(userId: string, name: string) {
  if (confirm(`Remove "${name}" from the organization?`)) {
    orgsStore.removeMember(orgId, userId)
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
