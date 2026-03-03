<template>
  <div class="p-8 max-w-3xl">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentSkill" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="store.currentSkill">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink to="/skills" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <div class="flex items-center gap-3 flex-1 min-w-0">
          <div class="w-10 h-10 bg-purple-900/40 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-5 h-5 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
            </svg>
          </div>
          <div>
            <h1 class="text-2xl font-bold text-white">{{ store.currentSkill.name }}</h1>
            <span :class="syncStatusClass(store.currentSkill.syncStatus)" class="text-xs px-2 py-0.5 rounded-full font-medium">
              {{ store.currentSkill.syncStatusName }}
            </span>
          </div>
        </div>
      </div>

      <ErrorBox :error="store.error" />

      <!-- Skill Content Form -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 mb-6">
        <h2 class="text-base font-semibold text-white mb-5">Skill Settings</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input v-model="form.name" type="text" placeholder="Skill name"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <input v-model="form.description" type="text" placeholder="Short description"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Content</label>
            <textarea v-model="form.content" rows="10" placeholder="You are an expert in..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none font-mono" />
          </div>
          <div class="flex justify-end pt-2">
            <button @click="saveSettings" :disabled="saving"
              class="px-5 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : 'Save Settings' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Git Repository -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 mb-6">
        <div class="flex items-center justify-between mb-1">
          <h2 class="text-base font-semibold text-white">Git Repository</h2>
          <span v-if="store.currentSkill.lastSyncedAt" class="text-xs text-gray-500">
            Last synced {{ new Date(store.currentSkill.lastSyncedAt).toLocaleString() }}
          </span>
        </div>
        <p class="text-sm text-gray-500 mb-5">
          Link a git repository to version-track this skill. The content will be stored and synced via git.
        </p>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Repository URL</label>
            <input v-model="form.gitRepoUrl" type="text" placeholder="https://github.com/org/skills-repo.git"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Subdirectory <span class="text-gray-600 font-normal">(optional, for sparse-checkout)</span></label>
            <input v-model="form.gitSubDir" type="text" placeholder="skills/python"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>

          <!-- Auth Section -->
          <div class="pt-2 border-t border-gray-800">
            <p class="text-sm font-medium text-gray-300 mb-3">Authentication</p>
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm text-gray-400 mb-1.5">Username</label>
                <input v-model="form.gitAuthUsername" type="text" placeholder="git username or 'x-access-token'"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
              <div>
                <label class="block text-sm text-gray-400 mb-1.5">Token / PAT</label>
                <input v-model="form.gitAuthToken" type="password" placeholder="Leave blank to keep existing"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              </div>
            </div>
          </div>

          <!-- Sync Status -->
          <div v-if="store.currentSkill.gitRepoUrl" class="bg-gray-800/40 rounded-lg p-3">
            <div class="flex items-center gap-2 mb-1">
              <span class="text-xs text-gray-500 uppercase tracking-wide">Sync Status</span>
              <span :class="syncStatusClass(store.currentSkill.syncStatus)"
                class="text-xs px-1.5 py-0.5 rounded-full font-medium">
                {{ store.currentSkill.syncStatusName }}
              </span>
            </div>
            <p v-if="store.currentSkill.syncMessage" class="text-xs text-gray-400">{{ store.currentSkill.syncMessage }}</p>
          </div>

          <div class="flex justify-end pt-2">
            <button @click="saveGitSettings" :disabled="saving"
              class="px-5 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : 'Save Git Settings' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Danger Zone -->
      <div class="bg-gray-900 border border-red-900/30 rounded-xl p-6">
        <h2 class="text-base font-semibold text-red-400 mb-3">Danger Zone</h2>
        <div class="flex items-center justify-between">
          <div>
            <p class="text-sm text-white">Delete this skill</p>
            <p class="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
          </div>
          <button @click="deleteSkill"
            class="px-4 py-2 bg-red-900/30 hover:bg-red-900/50 text-red-400 text-sm font-medium rounded-lg border border-red-900/40 transition-colors">
            Delete Skill
          </button>
        </div>
      </div>
    </template>

    <div v-else-if="!store.loading" class="text-center py-20 text-gray-500">
      Skill not found.
    </div>
  </div>
</template>

<script setup lang="ts">
import { useSkillsStore } from '~/stores/skills'
import { SkillSyncStatus } from '~/types'

const route = useRoute()
const router = useRouter()
const store = useSkillsStore()

const saving = ref(false)
const form = ref({
  name: '',
  description: '',
  content: '',
  gitRepoUrl: '',
  gitSubDir: '',
  gitAuthUsername: '',
  gitAuthToken: '',
})

onMounted(async () => {
  await store.fetchSkill(route.params.id as string)
  if (store.currentSkill) {
    form.value = {
      name: store.currentSkill.name,
      description: store.currentSkill.description ?? '',
      content: store.currentSkill.content ?? '',
      gitRepoUrl: store.currentSkill.gitRepoUrl ?? '',
      gitSubDir: store.currentSkill.gitSubDir ?? '',
      gitAuthUsername: store.currentSkill.gitAuthUsername ?? '',
      gitAuthToken: '',
    }
  }
})

async function saveSettings() {
  if (!store.currentSkill) return
  saving.value = true
  try {
    await store.updateSkill(store.currentSkill.id, {
      name: form.value.name,
      description: form.value.description || undefined,
      content: form.value.content,
      gitRepoUrl: store.currentSkill.gitRepoUrl,
      gitSubDir: store.currentSkill.gitSubDir,
      gitAuthUsername: store.currentSkill.gitAuthUsername,
    })
  } finally {
    saving.value = false
  }
}

async function saveGitSettings() {
  if (!store.currentSkill) return
  saving.value = true
  try {
    await store.updateSkill(store.currentSkill.id, {
      name: form.value.name,
      description: form.value.description || undefined,
      content: form.value.content,
      gitRepoUrl: form.value.gitRepoUrl || undefined,
      gitSubDir: form.value.gitSubDir || undefined,
      gitAuthUsername: form.value.gitAuthUsername || undefined,
      gitAuthToken: form.value.gitAuthToken || undefined,
    })
    form.value.gitAuthToken = ''
  } finally {
    saving.value = false
  }
}

async function deleteSkill() {
  if (!store.currentSkill) return
  if (!confirm(`Delete skill "${store.currentSkill.name}"? This cannot be undone.`)) return
  await store.deleteSkill(store.currentSkill.id)
  await router.push('/skills')
}

function syncStatusClass(status: SkillSyncStatus) {
  switch (status) {
    case SkillSyncStatus.Synced: return 'bg-green-900/40 text-green-400'
    case SkillSyncStatus.Ahead: return 'bg-yellow-900/40 text-yellow-400'
    case SkillSyncStatus.Behind: return 'bg-blue-900/40 text-blue-400'
    case SkillSyncStatus.Error: return 'bg-red-900/40 text-red-400'
    default: return 'bg-gray-800 text-gray-500'
  }
}
</script>
