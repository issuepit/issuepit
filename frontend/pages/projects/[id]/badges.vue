<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center gap-2 mb-2">
      <PageBreadcrumb :items="[
        { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
        { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
        { label: 'Status Badges', to: `/projects/${id}/badges`, icon: 'M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z' },
      ]" />
    </div>
    <p class="text-gray-500 text-sm mb-6">Embed live status badges into your README</p>

    <!-- Badge Builder -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
      <!-- Controls -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 space-y-5">
        <h2 class="font-semibold text-white">Customize Badge</h2>

        <!-- Metric -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Metric</label>
          <div class="grid grid-cols-2 gap-2">
            <button
              v-for="m in metrics"
              :key="m.value"
              :class="metric === m.value
                ? 'bg-brand-700 border-brand-500 text-white'
                : 'bg-gray-800 border-gray-700 text-gray-400 hover:text-gray-200 hover:border-gray-600'"
              class="flex items-center gap-2 px-3 py-2 rounded-lg border text-sm transition-colors text-left"
              @click="metric = m.value"
            >
              <span>{{ m.label }}</span>
            </button>
          </div>
        </div>

        <!-- Style -->
        <div>
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Style</label>
          <div class="flex gap-2">
            <button
              v-for="s in styles"
              :key="s.value"
              :class="style === s.value
                ? 'bg-brand-700 border-brand-500 text-white'
                : 'bg-gray-800 border-gray-700 text-gray-400 hover:text-gray-200 hover:border-gray-600'"
              class="flex-1 px-3 py-2 rounded-lg border text-sm transition-colors"
              @click="style = s.value"
            >
              {{ s.label }}
            </button>
          </div>
        </div>

        <!-- Branch (sessions and cicd metrics) -->
        <div v-if="metric === 'sessions' || metric.startsWith('cicd-')">
          <label class="block text-sm font-medium text-gray-300 mb-1.5">Branch <span class="text-gray-500">(optional)</span></label>
          <input
            v-model="branch"
            type="text"
            placeholder="e.g. main"
            class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
        </div>
      </div>

      <!-- Preview + Code -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-6 space-y-5">
        <h2 class="font-semibold text-white">Preview</h2>

        <!-- Live badge preview -->
        <div class="bg-gray-950 border border-gray-800 rounded-lg p-4 flex items-center justify-center min-h-[56px]">
          <img :key="badgeUrl" :src="badgeUrl" alt="Badge preview" class="max-h-6">
        </div>

        <!-- Markdown snippet -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label class="text-sm font-medium text-gray-300">Markdown</label>
            <button
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
              @click="copy(markdownSnippet)"
            >
              {{ copied === 'md' ? '✓ Copied' : 'Copy' }}
            </button>
          </div>
          <pre class="bg-gray-950 border border-gray-800 rounded-lg px-4 py-3 text-xs text-gray-300 font-mono overflow-x-auto whitespace-pre-wrap break-all">{{ markdownSnippet }}</pre>
        </div>

        <!-- Direct URL -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label class="text-sm font-medium text-gray-300">Image URL</label>
            <button
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
              @click="copy(badgeUrl)"
            >
              {{ copied === 'url' ? '✓ Copied' : 'Copy' }}
            </button>
          </div>
          <pre class="bg-gray-950 border border-gray-800 rounded-lg px-4 py-3 text-xs text-gray-300 font-mono overflow-x-auto whitespace-pre-wrap break-all">{{ badgeUrl }}</pre>
        </div>
      </div>
    </div>

    <!-- All Variants Gallery -->
    <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
      <h2 class="font-semibold text-white mb-4">All Variants</h2>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-gray-800">
              <th class="text-left text-gray-400 font-medium py-2 pr-4 min-w-[140px]">Metric</th>
              <th class="text-left text-gray-400 font-medium py-2 pr-4 min-w-[100px]">Style</th>
              <th class="text-left text-gray-400 font-medium py-2 pr-4">Badge</th>
              <th class="text-left text-gray-400 font-medium py-2 min-w-[200px]">Markdown</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800/50">
            <tr v-for="variant in allVariants" :key="variant.key" class="group">
              <td class="py-3 pr-4 text-gray-300">{{ variant.metricLabel }}</td>
              <td class="py-3 pr-4 text-gray-400 capitalize">{{ variant.style }}</td>
              <td class="py-3 pr-4">
                <img :key="variant.url" :src="variant.url" :alt="variant.metricLabel" class="max-h-5">
              </td>
              <td class="py-3">
                <div class="flex items-center gap-2">
                  <code class="text-xs text-gray-500 font-mono truncate max-w-[240px]">{{ variant.markdown }}</code>
                  <button
                    class="opacity-0 group-hover:opacity-100 text-xs text-brand-400 hover:text-brand-300 transition-all shrink-0"
                    @click="copy(variant.markdown, variant.key)"
                  >
                    {{ copied === variant.key ? '✓' : 'Copy' }}
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Documentation -->
    <div class="mt-6 bg-gray-900 border border-gray-800 rounded-xl p-6">
      <h2 class="font-semibold text-white mb-3">Query Parameters</h2>
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-gray-800">
              <th class="text-left text-gray-400 font-medium py-2 pr-6">Parameter</th>
              <th class="text-left text-gray-400 font-medium py-2 pr-6">Values</th>
              <th class="text-left text-gray-400 font-medium py-2">Description</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800/50 text-gray-400">
            <tr>
              <td class="py-2.5 pr-6 font-mono text-xs text-gray-300">project</td>
              <td class="py-2.5 pr-6 text-xs">UUID</td>
              <td class="py-2.5 text-xs">Project identifier (required)</td>
            </tr>
            <tr>
              <td class="py-2.5 pr-6 font-mono text-xs text-gray-300">metric</td>
              <td class="py-2.5 pr-6 text-xs font-mono">agents · sessions · issues · health · cicd-runs · cicd-failures · cicd-failure-rate</td>
              <td class="py-2.5 text-xs">Metric to display (default: <code class="font-mono">agents</code>)</td>
            </tr>
            <tr>
              <td class="py-2.5 pr-6 font-mono text-xs text-gray-300">style</td>
              <td class="py-2.5 pr-6 text-xs font-mono">flat · flat-square · plastic</td>
              <td class="py-2.5 text-xs">Visual style (default: <code class="font-mono">flat</code>)</td>
            </tr>
            <tr>
              <td class="py-2.5 pr-6 font-mono text-xs text-gray-300">branch</td>
              <td class="py-2.5 pr-6 text-xs">branch name</td>
              <td class="py-2.5 text-xs">Filter by git branch (for <code class="font-mono">sessions</code>, <code class="font-mono">cicd-runs</code>, <code class="font-mono">cicd-failures</code>, and <code class="font-mono">cicd-failure-rate</code>)</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div v-for="m in metrics" :key="m.value" class="bg-gray-800/50 rounded-lg p-3">
          <p class="text-xs font-semibold text-gray-300 mb-0.5 font-mono">{{ m.value }}</p>
          <p class="text-xs text-gray-500">{{ m.description }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
const route = useRoute()
const id = route.params.id as string
const config = useRuntimeConfig()
const apiBase = config.public.apiBase as string
const projectsStore = useProjectsStore()

// --- state ---
const metric = ref('agents')
const style = ref('flat')
const branch = ref('')
const copied = ref<string | null>(null)

const metrics = [
  { value: 'agents',            label: '🤖 Agents',            description: 'Number of currently active (Running) agent sessions' },
  { value: 'sessions',          label: '⏱ Sessions',           description: 'Agent sessions started in the last 24 h, optionally filtered by branch' },
  { value: 'issues',            label: '📋 Issues',            description: 'Count of open (non-done, non-cancelled) issues' },
  { value: 'health',            label: '❤️ Health',            description: 'Success rate of completed agent sessions over the last 7 days' },
  { value: 'cicd-runs',         label: '🔁 CI/CD Runs',        description: 'CI/CD pipeline runs triggered in the last 7 days, optionally filtered by branch' },
  { value: 'cicd-failures',     label: '❌ CI/CD Failures',    description: 'Number of failed CI/CD runs in the last 7 days, optionally filtered by branch' },
  { value: 'cicd-failure-rate', label: '📉 CI/CD Failure Rate', description: 'Percentage of failed CI/CD runs over the last 7 days, optionally filtered by branch' },
]

const styles = [
  { value: 'flat',        label: 'Flat' },
  { value: 'flat-square', label: 'Flat Square' },
  { value: 'plastic',     label: 'Plastic' },
]

// --- computed URLs ---
const badgeUrl = computed(() => {
  const params = new URLSearchParams({ project: id, metric: metric.value, style: style.value })
  if ((metric.value === 'sessions' || metric.value.startsWith('cicd-')) && branch.value.trim())
    params.set('branch', branch.value.trim())
  return `${apiBase}/api/badges?${params.toString()}`
})

const markdownSnippet = computed(() => {
  const m = metrics.find(x => x.value === metric.value)
  return `![${m?.label ?? metric.value}](${badgeUrl.value})`
})

// --- all variants for the gallery ---
const allVariants = computed(() =>
  metrics.flatMap(m =>
    styles.map(s => {
      const params = new URLSearchParams({ project: id, metric: m.value, style: s.value })
      const url = `${apiBase}/api/badges?${params.toString()}`
      const key = `${m.value}-${s.value}`
      return {
        key,
        metricLabel: m.label,
        style: s.label,
        url,
        markdown: `![${m.label}](${url})`,
      }
    })
  )
)

// --- copy helper ---
let copyTimer: ReturnType<typeof setTimeout> | null = null
function copy(text: string, key?: string) {
  navigator.clipboard.writeText(text).catch(() => {})
  copied.value = key ?? (text === badgeUrl.value ? 'url' : 'md')
  if (copyTimer) clearTimeout(copyTimer)
  copyTimer = setTimeout(() => { copied.value = null }, 2000)
}

onUnmounted(() => {
  if (copyTimer) clearTimeout(copyTimer)
})
</script>
