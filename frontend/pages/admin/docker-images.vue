<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Admin', to: '/admin/tenants', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4' },
          { label: 'Docker Images', to: '/admin/docker-images', icon: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01' },
        ]" />
        <p class="text-gray-400 mt-1 text-sm">Reconcile Docker images for all tenants, organizations, projects and agents.</p>
      </div>
      <button
        class="flex items-center gap-2 text-gray-400 hover:text-gray-200 text-sm border border-gray-700 hover:bg-gray-800 px-3 py-1.5 rounded-lg transition-colors"
        @click="loadData"
      >
        <svg class="w-4 h-4" :class="loading ? 'animate-spin' : ''" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
        </svg>
        Refresh
      </button>
    </div>

    <ErrorBox :error="error" />

    <!-- Loading -->
    <div v-if="loading && !tenants.length" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Content -->
    <div v-else-if="tenants.length" class="space-y-8">
      <div v-for="tenant in tenants" :key="tenant.id" class="rounded-xl border border-gray-800 overflow-hidden">
        <!-- Tenant header -->
        <div class="flex items-center gap-3 px-4 py-3 bg-gray-900 border-b border-gray-800">
          <svg class="w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
          </svg>
          <span class="text-sm font-semibold text-white">{{ tenant.name }}</span>
          <span class="text-xs text-gray-500 font-mono">{{ tenant.hostname }}</span>
          <!-- Config repo warning -->
          <span
            v-if="tenant.hasConfigRepo"
            class="ml-2 inline-flex items-center gap-1 text-xs text-yellow-400 bg-yellow-900/30 border border-yellow-700/40 px-2 py-0.5 rounded-full"
            title="This tenant has a config repo — JSON5 imports may overwrite image settings"
          >
            <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
            </svg>
            Config repo active
          </span>
        </div>

        <!-- Orgs table -->
        <div v-if="tenant.orgs.length" class="divide-y divide-gray-800/60">
          <div v-for="org in tenant.orgs" :key="org.id">
            <!-- Org row -->
            <div class="grid grid-cols-[180px_1fr_auto] gap-2 items-center px-4 py-2 bg-gray-900/30 hover:bg-gray-900/50 transition-colors">
              <div class="flex items-center gap-2 min-w-0">
                <svg class="w-3.5 h-3.5 text-brand-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                <span class="text-xs font-medium text-gray-200 truncate" :title="org.name">{{ org.name }}</span>
                <span class="text-xs text-gray-600 font-mono truncate">org</span>
              </div>
              <ImageEditCell
                :value="org.actRunnerImage ?? null"
                placeholder="inherited from global default"
                label="act runner image (inner)"
                :has-config-repo="tenant.hasConfigRepo"
                :config-source-file="org.actRunnerImageSourceFile"
                @save="(v) => saveOrgImage(org.id, v)"
              />
              <div class="w-4" />
            </div>

            <!-- Project rows -->
            <div
              v-for="project in org.projects"
              :key="project.id"
              class="grid grid-cols-[180px_1fr_auto] gap-2 items-center px-4 py-1.5 pl-10 hover:bg-gray-900/30 transition-colors border-t border-gray-800/30"
            >
              <div class="flex items-center gap-2 min-w-0">
                <svg class="w-3 h-3 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                </svg>
                <span class="text-xs text-gray-300 truncate" :title="project.name">{{ project.name }}</span>
                <span class="text-xs text-gray-600 font-mono">project</span>
              </div>
              <ImageEditCell
                :value="project.actRunnerImage ?? null"
                :placeholder="`inherit from org${org.actRunnerImage ? ` (${org.actRunnerImage})` : ' or global default'}`"
                label="act runner image (inner)"
                :has-config-repo="tenant.hasConfigRepo"
                :config-source-file="project.actRunnerImageSourceFile"
                @save="(v) => saveProjectImage(project.id, v)"
              />
              <div class="w-4" />
            </div>

            <!-- Agent rows -->
            <div
              v-for="agent in org.agents"
              :key="agent.id"
              class="grid grid-cols-[180px_1fr_auto] gap-2 items-center px-4 py-1.5 pl-10 hover:bg-gray-900/30 transition-colors border-t border-gray-800/30"
            >
              <div class="flex items-center gap-2 min-w-0">
                <svg class="w-3 h-3 text-purple-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18m0 0h10a2 2 0 002-2V9M9 21H5a2 2 0 01-2-2V9m0 0h18" />
                </svg>
                <span class="text-xs text-gray-300 truncate" :title="agent.name">{{ agent.name }}</span>
                <span class="text-xs text-gray-600 font-mono">agent</span>
              </div>
              <ImageEditCell
                :value="agent.dockerImage"
                placeholder="use runtime default"
                label="Docker image (outer)"
                :has-config-repo="false"
                @save="(v) => saveAgentImage(agent.id, v)"
              />
              <div class="w-4" />
            </div>
          </div>
        </div>
        <div v-else class="px-4 py-6 text-center text-sm text-gray-600">No organizations in this tenant.</div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">No data found</p>
    </div>
  </div>
</template>

<script setup lang="ts">
interface AgentImageEntry { id: string; name: string; dockerImage: string | null }
interface ProjectImageEntry { id: string; name: string; slug: string; actRunnerImage: string | null; actRunnerImageSourceFile: string | null }
interface OrgImageEntry { id: string; name: string; slug: string; actRunnerImage: string | null; actRunnerImageSourceFile: string | null; projects: ProjectImageEntry[]; agents: AgentImageEntry[] }
interface TenantImageEntry { id: string; name: string; hostname: string; hasConfigRepo: boolean; orgs: OrgImageEntry[] }

const api = useApi()

const tenants = ref<TenantImageEntry[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

async function loadData() {
  loading.value = true
  error.value = null
  try {
    tenants.value = await api.get<TenantImageEntry[]>('/api/admin/docker-images')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load docker images'
  } finally {
    loading.value = false
  }
}

async function saveOrgImage(orgId: string, image: string | null) {
  await api.patch(`/api/admin/docker-images/orgs/${orgId}`, { image })
  const tenant = tenants.value.find(t => t.orgs.some(o => o.id === orgId))
  const org = tenant?.orgs.find(o => o.id === orgId)
  if (org) { org.actRunnerImage = image; org.actRunnerImageSourceFile = null }
}

async function saveProjectImage(projectId: string, image: string | null) {
  await api.patch(`/api/admin/docker-images/projects/${projectId}`, { image })
  for (const tenant of tenants.value) {
    for (const org of tenant.orgs) {
      const project = org.projects.find(p => p.id === projectId)
      if (project) { project.actRunnerImage = image; project.actRunnerImageSourceFile = null; return }
    }
  }
}

async function saveAgentImage(agentId: string, image: string | null) {
  await api.patch(`/api/admin/docker-images/agents/${agentId}`, { image })
  for (const tenant of tenants.value) {
    for (const org of tenant.orgs) {
      const agent = org.agents.find(a => a.id === agentId)
      if (agent) { agent.dockerImage = image; return }
    }
  }
}

onMounted(loadData)
</script>
