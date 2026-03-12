<template>
  <div class="p-8 max-w-3xl">
    <PageBreadcrumb :items="[
      { label: 'Profile', to: '/profile', icon: 'M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z' },
    ]" class="mb-2" />
    <p class="text-gray-400 mb-8 text-sm">Manage your account settings and memberships.</p>

    <div class="space-y-6">
      <!-- User Info -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Account</h2>
        <div class="flex items-center gap-4 mb-4">
          <div class="w-12 h-12 rounded-full bg-brand-600 flex items-center justify-center text-lg font-bold text-white shrink-0">
            {{ initials }}
          </div>
          <div>
            <p class="text-white font-medium">{{ auth.user?.username }}</p>
            <p class="text-gray-400 text-sm">{{ auth.user?.email }}</p>
            <p class="text-gray-600 text-xs mt-0.5">Member since {{ joinedDate }}</p>
          </div>
        </div>
      </div>

      <!-- Organizations -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <div class="flex items-center justify-between mb-4">
          <h2 class="font-semibold text-white">Organizations</h2>
          <NuxtLink to="/orgs" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
            Manage →
          </NuxtLink>
        </div>
        <div v-if="orgsStore.loading" class="flex items-center gap-2 text-gray-500 text-sm">
          <div class="w-4 h-4 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          Loading…
        </div>
        <div v-else-if="orgsStore.orgs.length" class="space-y-2">
          <NuxtLink
            v-for="org in orgsStore.orgs"
            :key="org.id"
            :to="`/orgs/${org.id}`"
            class="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-gray-800 transition-colors"
          >
            <div class="w-7 h-7 rounded-md bg-purple-900/50 flex items-center justify-center shrink-0">
              <svg class="w-3.5 h-3.5 text-purple-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </div>
            <span class="text-sm text-gray-200">{{ org.name }}</span>
            <span class="text-xs text-gray-600 font-mono ml-auto">{{ org.slug }}</span>
          </NuxtLink>
        </div>
        <p v-else class="text-sm text-gray-500">No organizations yet.</p>
      </div>

      <!-- Projects -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <div class="flex items-center justify-between mb-4">
          <h2 class="font-semibold text-white">Projects</h2>
          <NuxtLink to="/projects" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
            Manage →
          </NuxtLink>
        </div>
        <div v-if="projectsStore.loading" class="flex items-center gap-2 text-gray-500 text-sm">
          <div class="w-4 h-4 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          Loading…
        </div>
        <div v-else-if="projectsStore.projects.length" class="space-y-2">
          <NuxtLink
            v-for="project in projectsStore.projects"
            :key="project.id"
            :to="`/projects/${project.id}`"
            class="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-gray-800 transition-colors"
          >
            <div class="w-7 h-7 rounded-md bg-blue-900/50 flex items-center justify-center shrink-0">
              <svg class="w-3.5 h-3.5 text-blue-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
            </div>
            <span class="text-sm text-gray-200">{{ project.name }}</span>
            <span class="text-xs text-gray-600 font-mono ml-auto">{{ project.slug }}</span>
          </NuxtLink>
        </div>
        <p v-else class="text-sm text-gray-500">No projects yet.</p>
      </div>

      <!-- Teams -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Teams</h2>
        <div v-if="teamsLoading" class="flex items-center gap-2 text-gray-500 text-sm">
          <div class="w-4 h-4 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          Loading…
        </div>
        <div v-else-if="allTeams.length" class="space-y-2">
          <NuxtLink
            v-for="item in allTeams"
            :key="item.team.id"
            :to="`/orgs/${item.team.orgId}`"
            class="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-gray-800 transition-colors"
          >
            <div class="w-7 h-7 rounded-md bg-green-900/50 flex items-center justify-center shrink-0">
              <svg class="w-3.5 h-3.5 text-green-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
              </svg>
            </div>
            <span class="text-sm text-gray-200">{{ item.team.name }}</span>
            <span class="text-xs text-gray-600 ml-auto">{{ item.orgName }}</span>
          </NuxtLink>
        </div>
        <p v-else class="text-sm text-gray-500">No teams yet.</p>
      </div>
      <!-- GitHub Account -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">GitHub Account</h2>
        <p class="text-sm text-gray-400 mb-4">Manage your GitHub identities and linked accounts.</p>
        <div class="flex flex-wrap gap-3">
          <NuxtLink
            to="/config/github-identities"
            class="flex items-center gap-2 text-sm text-gray-300 hover:text-white px-4 py-2 rounded-lg border border-gray-700 hover:bg-gray-800 transition-colors"
          >
            <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
              <path d="M12 0C5.37 0 0 5.373 0 12c0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61-.546-1.385-1.335-1.755-1.335-1.755-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 21.795 24 17.298 24 12c0-6.627-5.373-12-12-12" />
            </svg>
            GitHub Identities
          </NuxtLink>
          <a
            href="/api/auth/github?returnUrl=/config/github-identities"
            class="flex items-center gap-2 text-sm text-gray-300 hover:text-white px-4 py-2 rounded-lg border border-gray-700 hover:bg-gray-800 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
            </svg>
            Add via GitHub SSO
          </a>
          <a
            href="https://github.com/settings/profile"
            target="_blank"
            rel="noopener noreferrer"
            class="flex items-center gap-2 text-sm text-gray-300 hover:text-white px-4 py-2 rounded-lg border border-gray-700 hover:bg-gray-800 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
            </svg>
            Manage on GitHub
          </a>
        </div>
      </div>

      <!-- Change Password -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Change Password</h2>
        <form class="space-y-4 max-w-sm" @submit.prevent="handleChangePassword">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Current Password</label>
            <input
              v-model="passwordForm.currentPassword"
              type="password"
              placeholder="Leave blank if not set"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">New Password</label>
            <input
              v-model="passwordForm.newPassword"
              type="password"
              required
              placeholder="New password"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Confirm New Password</label>
            <input
              v-model="passwordForm.confirmPassword"
              type="password"
              required
              placeholder="Confirm new password"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div v-if="passwordError" class="text-sm text-red-400 bg-red-900/20 border border-red-900/30 rounded-lg px-3 py-2">
            {{ passwordError }}
          </div>
          <div v-if="passwordSuccess" class="text-sm text-green-400 bg-green-900/20 border border-green-900/30 rounded-lg px-3 py-2">
            Password changed successfully.
          </div>
          <button
            type="submit"
            :disabled="savingPassword"
            class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
          >
            {{ savingPassword ? 'Saving…' : 'Change Password' }}
          </button>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'
import { useOrgsStore } from '~/stores/orgs'
import { useProjectsStore } from '~/stores/projects'
import type { Team } from '~/types'

const auth = useAuthStore()
const orgsStore = useOrgsStore()
const projectsStore = useProjectsStore()
const api = useApi()

const teamsLoading = ref(false)
const allTeams = ref<Array<{ team: Team; orgName: string }>>([])

onMounted(async () => {
  await Promise.all([
    orgsStore.fetchOrgs(),
    projectsStore.fetchProjects(),
  ])
  teamsLoading.value = true
  try {
    const teamArrays = await Promise.all(
      orgsStore.orgs.map(org =>
        api.get<Team[]>(`/api/orgs/${org.id}/teams`)
          .then(teams => teams.map(t => ({ team: t, orgName: org.name })))
          .catch(() => [] as Array<{ team: Team; orgName: string }>)
      )
    )
    allTeams.value = teamArrays.flat()
  } finally {
    teamsLoading.value = false
  }
})

const displayName = computed(() => auth.user?.username ?? auth.user?.email?.split('@')[0] ?? 'User')
const initials = computed(() => displayName.value.slice(0, 2).padEnd(2, displayName.value[0] ?? 'U').toUpperCase())
const joinedDate = computed(() => {
  if (!auth.user?.createdAt) return ''
  return new Date(auth.user.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })
})

// Change password
const passwordForm = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const savingPassword = ref(false)
const passwordError = ref<string | null>(null)
const passwordSuccess = ref(false)

async function handleChangePassword() {
  passwordError.value = null
  passwordSuccess.value = false

  if (passwordForm.newPassword !== passwordForm.confirmPassword) {
    passwordError.value = 'New passwords do not match.'
    return
  }
  if (passwordForm.newPassword.length < 6) {
    passwordError.value = 'Password must be at least 6 characters.'
    return
  }

  savingPassword.value = true
  try {
    await api.patch('/api/auth/me/password', {
      currentPassword: passwordForm.currentPassword || null,
      newPassword: passwordForm.newPassword,
    })
    passwordSuccess.value = true
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (e: unknown) {
    passwordError.value = e instanceof Error ? e.message : 'Failed to change password.'
  } finally {
    savingPassword.value = false
  }
}
</script>
