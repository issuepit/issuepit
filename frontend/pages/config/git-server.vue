<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Git Server</h2>
        <p class="text-sm text-gray-400 mt-0.5">Manage git repositories hosted on the IssuePit git server.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="showCreate = true"
      >
        New Repository
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="text-gray-500 text-sm">Loading…</div>

    <!-- No org -->
    <div v-else-if="!currentOrgId" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No organization found. Create an organization first.</p>
    </div>

    <!-- Empty -->
    <div v-else-if="!store.repos.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No git repositories yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="showCreate = true">Create your first repo →</button>
    </div>

    <!-- Repos table -->
    <div v-else class="space-y-2">
      <div
        v-for="repo in store.repos"
        :key="repo.id"
        class="rounded-lg border border-gray-800 overflow-hidden"
      >
        <!-- Repo header row -->
        <div class="flex items-center justify-between px-4 py-3 bg-gray-900/50 hover:bg-gray-900 transition-colors">
          <div class="flex items-center gap-3 min-w-0">
            <button
              class="text-gray-500 hover:text-gray-300 transition-colors flex-shrink-0"
              :title="expanded[repo.id] ? 'Collapse' : 'Expand'"
              @click="toggleExpand(repo.id)"
            >
              <svg class="w-4 h-4 transition-transform" :class="{ 'rotate-90': expanded[repo.id] }" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
            </button>
            <div class="min-w-0">
              <span class="text-white font-mono font-medium">{{ repo.slug }}</span>
              <span v-if="repo.description" class="ml-2 text-gray-500 text-sm truncate">{{ repo.description }}</span>
            </div>
            <span v-if="repo.isReadOnly" class="px-2 py-0.5 rounded text-xs bg-yellow-900/40 text-yellow-400">read-only</span>
            <span v-if="repo.isTemporary" class="px-2 py-0.5 rounded text-xs bg-gray-700 text-gray-400">temp</span>
          </div>
          <div class="flex items-center gap-4 flex-shrink-0">
            <span class="text-gray-500 text-xs hidden sm:block">
              <DateDisplay :date="repo.createdAt" mode="absolute" resolution="date" />
            </span>
            <button
              class="text-gray-500 hover:text-red-400 transition-colors text-xs"
              @click="confirmDelete(repo)"
            >
              Delete
            </button>
          </div>
        </div>

        <!-- Expanded details -->
        <div v-if="expanded[repo.id]" class="border-t border-gray-800 bg-gray-900/20 px-4 py-4 space-y-6">
          <!-- Repo info -->
          <div class="grid grid-cols-2 gap-4 text-sm">
            <div>
              <span class="text-gray-500">Default branch:</span>
              <span class="ml-2 text-gray-300 font-mono">{{ repo.defaultBranch }}</span>
            </div>
            <div>
              <span class="text-gray-500">Default access:</span>
              <span class="ml-2 text-gray-300">{{ GitServerAccessLevelLabels[repo.defaultAccessLevel] }}</span>
            </div>
          </div>

          <!-- Permissions section -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <h4 class="text-sm font-medium text-gray-300">Permissions</h4>
              <button
                class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                @click="openGrantPermission(repo.id)"
              >
                + Grant
              </button>
            </div>
            <div v-if="permissionsLoading[repo.id]" class="text-gray-500 text-xs">Loading…</div>
            <div v-else-if="!permissions[repo.id]?.length" class="text-gray-600 text-xs">No explicit permissions.</div>
            <table v-else class="w-full text-xs">
              <thead>
                <tr class="text-gray-500">
                  <th class="text-left pb-1">User</th>
                  <th class="text-left pb-1">Level</th>
                  <th class="pb-1"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="perm in permissions[repo.id]" :key="perm.id" class="text-gray-400">
                  <td class="py-1">{{ perm.username ?? perm.userId ?? perm.apiKeyId ?? '—' }}</td>
                  <td class="py-1">{{ GitServerAccessLevelLabels[perm.accessLevel] }}</td>
                  <td class="py-1 text-right">
                    <button class="text-gray-600 hover:text-red-400 transition-colors" @click="revokePermission(repo.id, perm.id)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Branch Protections section -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <h4 class="text-sm font-medium text-gray-300">Branch Protections</h4>
              <button
                class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
                @click="openAddProtection(repo.id)"
              >
                + Add Rule
              </button>
            </div>
            <div v-if="protectionsLoading[repo.id]" class="text-gray-500 text-xs">Loading…</div>
            <div v-else-if="!protections[repo.id]?.length" class="text-gray-600 text-xs">No branch protection rules.</div>
            <table v-else class="w-full text-xs">
              <thead>
                <tr class="text-gray-500">
                  <th class="text-left pb-1">Pattern</th>
                  <th class="text-left pb-1">No force push</th>
                  <th class="text-left pb-1">Require PR</th>
                  <th class="pb-1"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-800">
                <tr v-for="rule in protections[repo.id]" :key="rule.id" class="text-gray-400">
                  <td class="py-1 font-mono">{{ rule.pattern }}</td>
                  <td class="py-1">{{ rule.disallowForcePush ? '✓' : '—' }}</td>
                  <td class="py-1">{{ rule.requirePullRequest ? '✓' : '—' }}</td>
                  <td class="py-1 text-right">
                    <button class="text-gray-600 hover:text-red-400 transition-colors" @click="deleteProtection(repo.id, rule.id)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Create repo modal -->
    <div v-if="showCreate" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">New Repository</h3>
        <form class="space-y-4" @submit.prevent="handleCreate">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Slug</label>
            <input v-model="createForm.slug" type="text" required placeholder="e.g. my-repo"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
            <p class="text-xs text-gray-600 mt-1">Lowercase letters, digits, and hyphens only.</p>
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Description (optional)</label>
            <input v-model="createForm.description" type="text" placeholder="Short description"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Default branch</label>
            <input v-model="createForm.defaultBranch" type="text" placeholder="main"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
          </div>
          <div v-if="createError" class="text-red-400 text-sm">{{ createError }}</div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Creating…' : 'Create' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="showCreate = false">Cancel</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Delete confirm modal -->
    <div v-if="deleteTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-sm p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-2">Delete Repository</h3>
        <p class="text-sm text-gray-400 mb-4">
          Are you sure you want to delete <span class="text-white font-mono">{{ deleteTarget.slug }}</span>?
          This action cannot be undone.
        </p>
        <div class="flex gap-3">
          <button
            class="flex-1 px-4 py-2 bg-red-600 hover:bg-red-500 text-white text-sm font-medium rounded-lg transition-colors"
            @click="handleDelete"
          >
            Delete
          </button>
          <button class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="deleteTarget = null">Cancel</button>
        </div>
      </div>
    </div>

    <!-- Grant permission modal -->
    <div v-if="grantTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">Grant Permission</h3>
        <form class="space-y-4" @submit.prevent="handleGrantPermission">
          <div>
            <label class="block text-sm text-gray-400 mb-1">User ID</label>
            <input v-model="grantForm.userId" type="text" placeholder="User UUID"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-1">Access Level</label>
            <select v-model.number="grantForm.accessLevel"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500">
              <option v-for="(label, val) in GitServerAccessLevelLabels" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Granting…' : 'Grant' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="grantTarget = null">Cancel</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Add branch protection modal -->
    <div v-if="protectionTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-md p-6 shadow-xl">
        <h3 class="text-lg font-semibold text-white mb-5">Add Branch Protection</h3>
        <form class="space-y-4" @submit.prevent="handleAddProtection">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Pattern</label>
            <input v-model="protectionForm.pattern" type="text" required placeholder="e.g. main or release/*"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-brand-500" />
          </div>
          <div class="flex items-center gap-2">
            <input id="disallowForcePush" v-model="protectionForm.disallowForcePush" type="checkbox"
              class="rounded border-gray-600 bg-gray-800 text-brand-500" />
            <label for="disallowForcePush" class="text-sm text-gray-400">Disallow force push</label>
          </div>
          <div class="flex items-center gap-2">
            <input id="requirePullRequest" v-model="protectionForm.requirePullRequest" type="checkbox"
              class="rounded border-gray-600 bg-gray-800 text-brand-500" />
            <label for="requirePullRequest" class="text-sm text-gray-400">Require pull request</label>
          </div>
          <div class="flex items-center gap-2">
            <input id="allowAdminBypass" v-model="protectionForm.allowAdminBypass" type="checkbox"
              class="rounded border-gray-600 bg-gray-800 text-brand-500" />
            <label for="allowAdminBypass" class="text-sm text-gray-400">Allow admin bypass</label>
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Adding…' : 'Add Rule' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="protectionTarget = null">Cancel</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { GitServerAccessLevelLabels } from '~/types'
import type { GitServerRepo, GitServerPermission, GitServerBranchProtection, GitServerAccessLevel } from '~/types'
import { useGitServerStore } from '~/stores/gitServer'
import { useOrgsStore } from '~/stores/orgs'

const store = useGitServerStore()
const orgsStore = useOrgsStore()

const currentOrgId = computed(() => orgsStore.orgs[0]?.id ?? null)

onMounted(async () => {
  await orgsStore.fetchOrgs()
  if (currentOrgId.value) {
    await store.fetchRepos(currentOrgId.value)
  }
})

// ── Expand / collapse ──────────────────────────────────────────────────────────

const expanded = ref<Record<string, boolean>>({})
const permissions = ref<Record<string, GitServerPermission[]>>({})
const protections = ref<Record<string, GitServerBranchProtection[]>>({})
const permissionsLoading = ref<Record<string, boolean>>({})
const protectionsLoading = ref<Record<string, boolean>>({})

async function toggleExpand(repoId: string) {
  expanded.value[repoId] = !expanded.value[repoId]
  if (expanded.value[repoId] && currentOrgId.value) {
    if (!permissions.value[repoId]) {
      permissionsLoading.value[repoId] = true
      try {
        permissions.value[repoId] = await store.fetchPermissions(currentOrgId.value, repoId)
      } finally {
        permissionsLoading.value[repoId] = false
      }
    }
    if (!protections.value[repoId]) {
      protectionsLoading.value[repoId] = true
      try {
        protections.value[repoId] = await store.fetchBranchProtections(currentOrgId.value, repoId)
      } finally {
        protectionsLoading.value[repoId] = false
      }
    }
  }
}

// ── Create repo ────────────────────────────────────────────────────────────────

const showCreate = ref(false)
const saving = ref(false)
const createError = ref<string | null>(null)

const createForm = reactive({
  slug: '',
  description: '',
  defaultBranch: 'main',
})

async function handleCreate() {
  if (!currentOrgId.value) return
  saving.value = true
  createError.value = null
  try {
    await store.createRepo(currentOrgId.value, {
      slug: createForm.slug,
      description: createForm.description || undefined,
      defaultBranch: createForm.defaultBranch || 'main',
    })
    showCreate.value = false
    Object.assign(createForm, { slug: '', description: '', defaultBranch: 'main' })
  } catch (e: unknown) {
    createError.value = e instanceof Error ? e.message : 'Failed to create repository'
  } finally {
    saving.value = false
  }
}

// ── Delete repo ────────────────────────────────────────────────────────────────

const deleteTarget = ref<GitServerRepo | null>(null)

function confirmDelete(repo: GitServerRepo) {
  deleteTarget.value = repo
}

async function handleDelete() {
  if (!deleteTarget.value || !currentOrgId.value) return
  await store.deleteRepo(currentOrgId.value, deleteTarget.value.id)
  deleteTarget.value = null
}

// ── Grant permission ───────────────────────────────────────────────────────────

const grantTarget = ref<string | null>(null) // repoId
const grantForm = reactive({
  userId: '',
  accessLevel: 1 as GitServerAccessLevel,
})

function openGrantPermission(repoId: string) {
  grantTarget.value = repoId
  Object.assign(grantForm, { userId: '', accessLevel: 1 })
}

async function handleGrantPermission() {
  if (!grantTarget.value || !currentOrgId.value) return
  saving.value = true
  try {
    const perm = await store.grantPermission(currentOrgId.value, grantTarget.value, {
      userId: grantForm.userId || undefined,
      accessLevel: grantForm.accessLevel,
    })
    if (!permissions.value[grantTarget.value]) permissions.value[grantTarget.value] = []
    const existing = permissions.value[grantTarget.value].findIndex(p => p.id === perm.id)
    if (existing >= 0) permissions.value[grantTarget.value][existing] = perm
    else permissions.value[grantTarget.value].push(perm)
    grantTarget.value = null
  } finally {
    saving.value = false
  }
}

async function revokePermission(repoId: string, permId: string) {
  if (!currentOrgId.value) return
  await store.revokePermission(currentOrgId.value, repoId, permId)
  if (permissions.value[repoId]) {
    permissions.value[repoId] = permissions.value[repoId].filter(p => p.id !== permId)
  }
}

// ── Branch protection ──────────────────────────────────────────────────────────

const protectionTarget = ref<string | null>(null) // repoId
const protectionForm = reactive({
  pattern: '',
  disallowForcePush: false,
  requirePullRequest: false,
  allowAdminBypass: false,
})

function openAddProtection(repoId: string) {
  protectionTarget.value = repoId
  Object.assign(protectionForm, { pattern: '', disallowForcePush: false, requirePullRequest: false, allowAdminBypass: false })
}

async function handleAddProtection() {
  if (!protectionTarget.value || !currentOrgId.value) return
  saving.value = true
  try {
    const rule = await store.createBranchProtection(currentOrgId.value, protectionTarget.value, {
      pattern: protectionForm.pattern,
      disallowForcePush: protectionForm.disallowForcePush,
      requirePullRequest: protectionForm.requirePullRequest,
      allowAdminBypass: protectionForm.allowAdminBypass,
    })
    if (!protections.value[protectionTarget.value]) protections.value[protectionTarget.value] = []
    protections.value[protectionTarget.value].push(rule)
    protectionTarget.value = null
  } finally {
    saving.value = false
  }
}

async function deleteProtection(repoId: string, ruleId: string) {
  if (!currentOrgId.value) return
  await store.deleteBranchProtection(currentOrgId.value, repoId, ruleId)
  if (protections.value[repoId]) {
    protections.value[repoId] = protections.value[repoId].filter(r => r.id !== ruleId)
  }
}
</script>
