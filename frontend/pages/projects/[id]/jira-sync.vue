<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 mb-4">
        <PageBreadcrumb
:items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject.name, to: `/projects/${id}`, color: projectsStore.currentProject.color || '#4c6ef5' },
          { label: 'Jira Sync', to: `/projects/${id}/jira-sync`, icon: 'M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1' },
        ]" />
      </div>

      <!-- Settings nav tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <NuxtLink
          :to="`/projects/${id}/settings`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/settings` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >Settings</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/ci-cd`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/ci-cd` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >CI/CD</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/members`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/members` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >Members</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/github-sync`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/github-sync` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >GitHub Sync</NuxtLink>
        <NuxtLink
          :to="`/projects/${id}/jira-sync`"
          :class="['px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px', $route.path === `/projects/${id}/jira-sync` ? 'text-white border-brand-500' : 'text-gray-400 hover:text-gray-200 border-transparent']"
        >Jira Sync</NuxtLink>
      </div>

      <!-- Inner tabs -->
      <div class="flex gap-1 mb-6">
        <button
          v-for="tab in tabs"
          :key="tab"
          :class="['px-4 py-1.5 text-sm font-medium rounded-lg transition-colors', activeTab === tab ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-400 hover:text-gray-200']"
          @click="activeTab = tab"
        >
          {{ tab }}
        </button>
      </div>

      <!-- ── Configuration ───────────────────────────────────────────────── -->
      <div v-if="activeTab === 'Configuration'" class="space-y-6 max-w-2xl">
        <!-- Required Permissions info box -->
        <div class="bg-blue-950/40 border border-blue-800/50 rounded-xl p-5">
          <h3 class="text-sm font-semibold text-blue-300 mb-2 flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Required Jira API Token Permissions
          </h3>
          <p class="text-xs text-blue-200/80 mb-2">
            To import all issues and their comments, the Jira API token must belong to a user with the following project permissions:
          </p>
          <ul class="text-xs text-blue-200/70 space-y-1 list-disc list-inside">
            <li><span class="font-mono text-blue-300">Browse Projects</span> — required to list and read issues</li>
            <li><span class="font-mono text-blue-300">View Read-Only Workflow</span> — required to read issue status</li>
            <li><span class="font-mono text-blue-300">Service Desk Agent</span> — only if importing from a Service Desk project</li>
          </ul>
          <p class="text-xs text-blue-200/60 mt-2">
            Jira is read-only in IssuePit — no write operations will be performed.
            Create your API token at
            <a href="https://id.atlassian.com/manage-profile/security/api-tokens" target="_blank" rel="noopener noreferrer" class="text-blue-400 hover:text-blue-300 underline">id.atlassian.com</a>.
          </p>
        </div>

        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Jira Sync Configuration</h2>
          <p class="text-sm text-gray-500 mb-4">
            Configure how this project imports issues from Jira. An API key with provider <span class="font-mono text-gray-400">Jira</span> is required.
            Add one in
            <NuxtLink to="/config/keys" class="text-brand-400 hover:text-brand-300">Config → API Keys</NuxtLink>.
          </p>

          <form class="space-y-4" @submit.prevent="saveConfig">
            <!-- Jira Base URL -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Jira Base URL</label>
              <input
v-model="form.jiraBaseUrl" type="text" placeholder="https://your-company.atlassian.net"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <p class="text-xs text-gray-600 mt-1">Example: <span class="font-mono">https://acme.atlassian.net</span></p>
            </div>

            <!-- Jira Project Key -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Jira Project Key</label>
              <input
v-model="form.jiraProjectKey" type="text" placeholder="PROJ"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <p class="text-xs text-gray-600 mt-1">The short key for your Jira project (e.g. <span class="font-mono">PROJ</span>, <span class="font-mono">ACME</span>).</p>
            </div>

            <!-- Jira User Email -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Jira User Email</label>
              <input
v-model="form.jiraEmail" type="email" placeholder="you@company.com"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <p class="text-xs text-gray-600 mt-1">The email address of the Jira account whose API token you are using.</p>
            </div>

            <!-- API Key -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Jira API Token (Key)</label>
              <select
v-model="form.apiKeyId"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option value="">— None —</option>
                <option v-for="key in jiraApiKeys" :key="key.id" :value="key.id">
                  {{ key.name }}
                </option>
              </select>
              <p v-if="jiraApiKeys.length === 0" class="text-xs text-yellow-500 mt-1">
                No Jira API keys found. Add one in
                <NuxtLink to="/config/keys" class="text-brand-400 hover:text-brand-300">Config → API Keys</NuxtLink>
                with provider <span class="font-mono">Jira</span>.
              </p>
            </div>

            <!-- Import Options -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-2">Import Options</label>
              <div class="space-y-2">
                <label class="flex items-start gap-3 p-3 rounded-lg border border-gray-700 cursor-pointer hover:border-gray-600 transition-colors">
                  <input
v-model="form.onlyImportWithParent" type="checkbox"
                    class="mt-0.5 accent-brand-500 w-4 h-4 rounded">
                  <div>
                    <p class="text-sm font-medium text-gray-200">Only import issues with a parent</p>
                    <p class="text-xs text-gray-500 mt-0.5">
                      When enabled, only Jira issues that have a parent set (e.g. sub-tasks, child issues, issues under an epic)
                      will be imported. Top-level issues without a parent are skipped.
                    </p>
                  </div>
                </label>

                <label class="flex items-start gap-3 p-3 rounded-lg border border-gray-700 cursor-pointer hover:border-gray-600 transition-colors">
                  <input
v-model="form.importComments" type="checkbox"
                    class="mt-0.5 accent-brand-500 w-4 h-4 rounded">
                  <div>
                    <p class="text-sm font-medium text-gray-200">Import issue comments</p>
                    <p class="text-xs text-gray-500 mt-0.5">
                      Import Jira issue comments as IssuePit comments on the corresponding imported issue.
                    </p>
                  </div>
                </label>
              </div>
            </div>

            <!-- Trigger Mode -->
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Trigger Mode</label>
              <select
v-model.number="form.triggerMode"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option :value="JiraSyncTriggerMode.Off">Off — import disabled</option>
                <option :value="JiraSyncTriggerMode.Manual">Manual — trigger from this page only</option>
                <option :value="JiraSyncTriggerMode.Auto">Auto — import runs on a schedule</option>
              </select>
            </div>

            <p v-if="saveError" class="text-red-400 text-sm">{{ saveError }}</p>
            <p v-if="saveSuccess" class="text-green-400 text-sm">Configuration saved.</p>

            <div class="flex gap-3 pt-1 flex-wrap">
              <button
type="submit" :disabled="syncStore.loading"
                class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
                {{ syncStore.loading ? 'Saving…' : 'Save Configuration' }}
              </button>
              <button
                v-if="form.triggerMode !== JiraSyncTriggerMode.Off"
                type="button"
                :disabled="triggering"
                class="bg-gray-700 hover:bg-gray-600 disabled:opacity-50 text-gray-300 text-sm font-medium px-4 py-2 rounded-lg transition-colors"
                @click="triggerSync(false)"
              >
                {{ triggering ? 'Triggered…' : 'Trigger Import Now' }}
              </button>
              <button
                v-if="form.triggerMode !== JiraSyncTriggerMode.Off"
                type="button"
                :disabled="triggering"
                class="bg-gray-800 hover:bg-gray-700 disabled:opacity-50 text-gray-400 text-sm font-medium px-4 py-2 rounded-lg transition-colors border border-gray-700"
                @click="triggerSync(true)"
              >
                {{ triggering ? 'Running…' : 'Dry Run' }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- ── Import Runs (Audit Log) ─────────────────────────────────────── -->
      <div v-else-if="activeTab === 'Import Runs'">
        <ScheduledTaskRuns
          ref="runsComponent"
          :runs="syncStore.runs"
          :loading="syncStore.loading"
          :triggering="triggering"
          trigger-label="Trigger Import"
          :fetch-run-detail="(runId) => syncStore.fetchRun(id, runId)"
          @trigger="triggerSync"
          @open-run="(runId) => runsComponent?.openRun(runId)"
        />
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400">Project not found</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { JiraSyncTriggerMode, ApiKeyProvider } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useJiraSyncStore } from '~/stores/jira-sync'
import { useConfigStore } from '~/stores/config'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const syncStore = useJiraSyncStore()
const configStore = useConfigStore()

const tabs = ['Configuration', 'Import Runs']

// Initialise from URL query param so deep-linked tabs load correctly on page reload.
const tabParam = route.query.tab as string | undefined
const activeTab = ref<string>(tabParam && tabs.includes(tabParam) ? tabParam : 'Configuration')

const form = reactive({
  jiraBaseUrl: '',
  jiraProjectKey: '',
  jiraEmail: '',
  apiKeyId: '',
  triggerMode: JiraSyncTriggerMode.Off as number,
  onlyImportWithParent: false,
  importComments: true,
})

const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const triggering = ref(false)
const runsComponent = ref<InstanceType<typeof import('~/components/ScheduledTaskRuns.vue').default> | null>(null)

// Filter API keys to only those with Jira provider.
const jiraApiKeys = computed(() =>
  (configStore.apiKeys ?? []).filter(k => k.provider === ApiKeyProvider.Jira),
)

// Populate form whenever the store config is (re)loaded.
// Guard against stale config from a different project (store persists across navigation).
watch(() => syncStore.config, (cfg) => {
  if (cfg && cfg.projectId === id) {
    form.jiraBaseUrl = cfg.jiraBaseUrl ?? ''
    form.jiraProjectKey = cfg.jiraProjectKey ?? ''
    form.jiraEmail = cfg.jiraEmail ?? ''
    form.apiKeyId = cfg.apiKeyId ?? ''
    form.triggerMode = cfg.triggerMode
    form.onlyImportWithParent = cfg.onlyImportWithParent
    form.importComments = cfg.importComments
  }
}, { immediate: true })

// Keep the URL query param in sync with the active tab.
watch(activeTab, (tab) => {
  if (route.query.tab !== tab)
    router.replace({ query: { ...route.query, tab } })
})

async function saveConfig() {
  saveError.value = null
  saveSuccess.value = false
  try {
    await syncStore.saveConfig(id, {
      jiraBaseUrl: form.jiraBaseUrl || null,
      jiraProjectKey: form.jiraProjectKey || null,
      jiraEmail: form.jiraEmail || null,
      apiKeyId: form.apiKeyId || null,
      triggerMode: form.triggerMode,
      onlyImportWithParent: form.onlyImportWithParent,
      importComments: form.importComments,
    })
    saveSuccess.value = true
    setTimeout(() => { saveSuccess.value = false }, 3000)
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : 'Failed to save configuration'
  }
}

async function triggerSync(dry = false) {
  triggering.value = true
  try {
    await syncStore.triggerSync(id, dry)
    activeTab.value = 'Import Runs'
    await new Promise(resolve => setTimeout(resolve, 1500))
    await syncStore.fetchRuns(id)
  } catch {
    // error already in store
  } finally {
    triggering.value = false
  }
}

watch(activeTab, async (tab) => {
  if (tab === 'Import Runs') await syncStore.fetchRuns(id)
})

onMounted(async () => {
  await Promise.all([
    projectsStore.fetchProject(id),
    syncStore.fetchConfig(id),
    configStore.fetchApiKeys(),
  ])

  if (activeTab.value === 'Import Runs') await syncStore.fetchRuns(id)
})
</script>
