<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <h1 class="text-2xl font-bold text-white">Projects</h1>
        <p class="text-gray-400 mt-1">{{ store.projects.length }} projects</p>
      </div>
      <button @click="showCreate = true"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Project
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Grid -->
    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <NuxtLink v-for="project in store.projects" :key="project.id"
        :to="`/projects/${project.id}`"
        class="bg-gray-900 border border-gray-800 rounded-xl p-5 hover:border-gray-700 transition-colors group">
        <div class="flex items-start gap-3 mb-3">
          <div :style="{ background: project.color || '#4c6ef5' }"
            class="w-10 h-10 rounded-lg flex items-center justify-center text-white font-bold text-lg shrink-0">
            {{ project.name.charAt(0).toUpperCase() }}
          </div>
          <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-white group-hover:text-brand-300 transition-colors truncate">
              {{ project.name }}
            </h3>
            <p class="text-xs text-gray-500">/{{ project.slug }}</p>
          </div>
          <span v-if="project.isPrivate"
            class="text-xs bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded">Private</span>
        </div>
        <p v-if="project.description" class="text-sm text-gray-400 mb-4 line-clamp-2">
          {{ project.description }}
        </p>
        <div class="flex items-center gap-4 text-xs text-gray-500">
          <span>{{ project.issueCount }} issues</span>
          <span>{{ project.memberCount }} members</span>
        </div>
      </NuxtLink>

      <!-- Empty state -->
      <div v-if="!store.loading && store.projects.length === 0"
        class="col-span-full flex flex-col items-center justify-center py-20 text-center">
        <div class="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
          <svg class="w-8 h-8 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
              d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
          </svg>
        </div>
        <p class="text-gray-400 font-medium">No projects yet</p>
        <p class="text-gray-600 text-sm mt-1">Create your first project to get started</p>
        <button @click="showCreate = true"
          class="mt-4 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
          Create Project
        </button>
      </div>
    </div>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Project</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input v-model="form.name" type="text" placeholder="My Project"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Slug</label>
            <input v-model="form.slug" type="text" placeholder="my-project"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Project Key <span class="text-gray-500 font-normal">(optional)</span></label>
            <input v-model="form.issueKey" type="text" maxlength="10" placeholder="e.g. IP"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 uppercase" />
            <p class="text-xs text-gray-600 mt-1">Short key for issue IDs — issues will display as <span class="font-mono text-gray-400">{{ form.issueKey ? `${form.issueKey.toUpperCase()}-1` : '#1' }}</span></p>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Organization</label>
            <select v-model="form.orgId" data-testid="org-select"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="" disabled selected>Select an organization</option>
              <option v-for="org in orgsStore.orgs" :key="org.id" :value="org.id">{{ org.name }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.description" rows="3" placeholder="Optional description..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create
          </button>
          <button @click="showCreate = false; resetForm()"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'

const store = useProjectsStore()
const orgsStore = useOrgsStore()
const showCreate = ref(false)
const form = reactive({ name: '', slug: '', description: '', orgId: '', issueKey: '' })

function generateIssueKey(name: string): string {
  const words = name.split(/[\s\-_]+/).filter(Boolean)
  if (words.length === 0) return ''
  let key = words.map(w => w[0].toUpperCase()).join('')
  if (key.length === 1 && name.length >= 3) key = name.slice(0, 3).toUpperCase()
  return key.slice(0, 10)
}

watch(() => form.name, (val) => {
  form.slug = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  form.issueKey = generateIssueKey(val)
})

onMounted(async () => {
  await Promise.all([store.fetchProjects(), orgsStore.fetchOrgs()])
})

async function submitCreate() {
  if (!form.name || !form.orgId) return
  await store.createProject({
    ...form,
    issueKey: form.issueKey.trim().toUpperCase() || undefined,
  })
  showCreate.value = false
  resetForm()
}

function resetForm() {
  form.name = ''
  form.slug = ''
  form.description = ''
  form.orgId = ''
  form.issueKey = ''
}
</script>
