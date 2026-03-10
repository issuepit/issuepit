<template>
  <div class="p-8 max-w-2xl">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-8">
      <PageBreadcrumb :items="[
        { label: 'Skills', to: '/skills', icon: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z' },
        { label: 'New Skill', to: '/skills/create', icon: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z' },
      ]" />
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
