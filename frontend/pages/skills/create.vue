<template>
  <div class="p-8 max-w-2xl">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-8">
      <NuxtLink to="/skills" class="text-gray-500 hover:text-gray-300 transition-colors">
        <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
        </svg>
      </NuxtLink>
      <div>
        <h1 class="text-2xl font-bold text-white">New Skill</h1>
        <p class="text-gray-400 mt-1 text-sm">Define a reusable system prompt for your agents.</p>
      </div>
    </div>

    <ErrorBox :error="store.error" />

    <!-- Form -->
    <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
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
          <textarea v-model="form.content" rows="8" placeholder="You are an expert in..."
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none font-mono" />
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
        <NuxtLink to="/skills"
          class="px-4 py-2 text-sm text-gray-400 hover:text-gray-200 transition-colors">
          Cancel
        </NuxtLink>
        <button @click="submitCreate" :disabled="!form.name || !form.content || creating"
          class="px-4 py-2 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
          {{ creating ? 'Creating…' : 'Create Skill' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useSkillsStore } from '~/stores/skills'
import { useOrgsStore } from '~/stores/orgs'

const store = useSkillsStore()
const orgsStore = useOrgsStore()
const router = useRouter()

const creating = ref(false)
const form = ref({
  name: '',
  description: '',
  content: '',
  orgId: '',
})

onMounted(async () => {
  await orgsStore.fetchOrgs()
  if (orgsStore.orgs.length > 0) {
    form.value.orgId = orgsStore.orgs[0].id
  }
})

async function submitCreate() {
  if (!form.value.name || !form.value.content) return
  creating.value = true
  try {
    const skill = await store.createSkill({
      name: form.value.name,
      description: form.value.description || undefined,
      content: form.value.content,
      orgId: form.value.orgId,
    })
    if (skill) {
      await router.push(`/skills/${skill.id}`)
    }
  } finally {
    creating.value = false
  }
}
</script>
