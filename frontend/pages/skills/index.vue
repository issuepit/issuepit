<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Skills', to: '/skills', icon: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z' },
        ]" />
        <p class="text-gray-400 mt-1 text-sm">{{ store.skills.length }} skill{{ store.skills.length !== 1 ? 's' : '' }} configured</p>
      </div>
      <NuxtLink to="/skills/create"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Skill
      </NuxtLink>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Skills List -->
    <div v-else class="space-y-3">
      <div v-for="skill in store.skills" :key="skill.id"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors">
        <div class="flex items-start justify-between">
          <NuxtLink :to="`/skills/${skill.id}`" class="flex items-center gap-3 flex-1 min-w-0 mr-4">
            <div class="w-10 h-10 bg-purple-900/40 rounded-lg flex items-center justify-center shrink-0">
              <svg class="w-5 h-5 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
              </svg>
            </div>
            <div>
              <h3 class="font-semibold text-white">{{ skill.name }}</h3>
              <p v-if="skill.description" class="text-sm text-gray-400 mt-0.5">{{ skill.description }}</p>
            </div>
          </NuxtLink>

          <div class="flex items-center gap-2 shrink-0">
            <span :class="syncStatusClass(skill.syncStatus)"
              class="text-xs px-2 py-0.5 rounded-full font-medium">
              {{ skill.syncStatusName }}
            </span>
            <NuxtLink :to="`/skills/${skill.id}`"
              class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors">
              Edit
            </NuxtLink>
            <button @click="confirmDeleteSkill(skill)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors">
              Delete
            </button>
          </div>
        </div>

        <!-- Details -->
        <div class="mt-4 grid grid-cols-1 lg:grid-cols-2 gap-4">
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Git Repository</p>
            <code v-if="skill.gitRepoUrl" class="text-xs text-green-300 font-mono break-all">{{ skill.gitRepoUrl }}</code>
            <span v-else class="text-xs text-gray-600">Not configured</span>
          </div>
          <div class="bg-gray-800/40 rounded-lg p-3">
            <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Last Synced</p>
            <span class="text-xs text-gray-400">{{ skill.lastSyncedAt ? new Date(skill.lastSyncedAt).toLocaleString() : '—' }}</span>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="!store.loading && store.skills.length === 0"
        class="flex flex-col items-center justify-center py-20 text-center">
        <div class="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
          <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No skills yet</p>
        <p class="text-gray-600 text-sm mt-1">Create a skill to define reusable system prompts for your agents.</p>
        <button @click="navigateTo('/skills/create')"
          class="mt-4 px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white text-sm rounded-lg transition-colors">
          Create your first skill
        </button>
      </div>
    </div>

    <!-- Delete Confirmation Dialog -->
    <div v-if="showDeleteConfirm" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-sm p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-2">Delete Skill</h2>
        <p class="text-sm text-gray-400 mb-6">Are you sure you want to delete <span class="text-white font-medium">{{ skillToDelete?.name }}</span>? This action cannot be undone.</p>
        <div class="flex gap-3">
          <button @click="doDeleteSkill"
            class="flex-1 bg-red-600 hover:bg-red-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Delete
          </button>
          <button @click="showDeleteConfirm = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useSkillsStore } from '~/stores/skills'
import { SkillSyncStatus } from '~/types'
import type { Skill } from '~/types'

const store = useSkillsStore()

const showDeleteConfirm = ref(false)
const skillToDelete = ref<Skill | null>(null)

onMounted(async () => {
  await store.fetchSkills()
})

function confirmDeleteSkill(skill: Skill) {
  skillToDelete.value = skill
  showDeleteConfirm.value = true
}

async function doDeleteSkill() {
  if (!skillToDelete.value) return
  await store.deleteSkill(skillToDelete.value.id)
  showDeleteConfirm.value = false
  skillToDelete.value = null
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
