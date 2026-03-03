<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <h1 class="text-2xl font-bold text-white">Skills</h1>
        <p class="text-gray-400 mt-1">{{ store.skills.length }} skill{{ store.skills.length !== 1 ? 's' : '' }} configured</p>
      </div>
      <button @click="openCreate"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Skill
      </button>
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
            <button @click="store.deleteSkill(skill.id)"
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
        <button @click="openCreate"
          class="mt-4 px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white text-sm rounded-lg transition-colors">
          Create your first skill
        </button>
      </div>
    </div>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 w-full max-w-lg">
        <h2 class="text-lg font-semibold text-white mb-5">New Skill</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name <span class="text-red-400">*</span></label>
            <input v-model="form.name" type="text" placeholder="e.g. Python Expert"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <input v-model="form.description" type="text" placeholder="Short description of this skill"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Content <span class="text-red-400">*</span></label>
            <textarea v-model="form.content" rows="5" placeholder="You are an expert in..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Organization</label>
            <select v-model="form.orgId"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="org in orgsStore.orgs" :key="org.id" :value="org.id">{{ org.name }}</option>
            </select>
          </div>
        </div>
        <div class="flex justify-end gap-3 mt-6">
          <button @click="showCreate = false"
            class="px-4 py-2 text-sm text-gray-400 hover:text-gray-200 transition-colors">
            Cancel
          </button>
          <button @click="submitCreate" :disabled="!form.name || !form.content || creating"
            class="px-4 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
            {{ creating ? 'Creating…' : 'Create Skill' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useSkillsStore } from '~/stores/skills'
import { useOrgsStore } from '~/stores/orgs'
import { SkillSyncStatus } from '~/types'

const store = useSkillsStore()
const orgsStore = useOrgsStore()

const showCreate = ref(false)
const creating = ref(false)
const form = ref({
  name: '',
  description: '',
  content: '',
  orgId: '',
})

onMounted(async () => {
  await Promise.all([store.fetchSkills(), orgsStore.fetchOrgs()])
  if (orgsStore.orgs.length > 0 && !form.value.orgId) {
    form.value.orgId = orgsStore.orgs[0].id
  }
})

function openCreate() {
  form.value = { name: '', description: '', content: '', orgId: orgsStore.orgs[0]?.id ?? '' }
  showCreate.value = true
}

async function submitCreate() {
  if (!form.value.name || !form.value.content) return
  creating.value = true
  try {
    await store.createSkill({
      name: form.value.name,
      description: form.value.description || undefined,
      content: form.value.content,
      orgId: form.value.orgId,
    })
    showCreate.value = false
  } finally {
    creating.value = false
  }
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
