<template>
  <div class="p-8">
    <!-- Header -->
    <div class="mb-6 flex items-start justify-between gap-3">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Dashboard', to: '/', icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6' },
        ]" class="mb-1" />
        <p class="text-gray-400 text-sm">Welcome back — here's what's happening.</p>
      </div>
    </div>

    <!-- Draft mode toolbar -->
    <div v-if="isDraftMode"
      class="mb-4 bg-amber-950/40 border border-amber-700/40 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
      <div class="flex items-center gap-2 text-amber-300 text-sm">
        <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
        </svg>
        <span class="font-medium">Draft mode</span>
        <span class="text-amber-400/70 text-xs hidden sm:inline">— drag to reorder · click Hide to remove a section</span>
      </div>
      <div class="flex items-center gap-1.5">
        <button @click="resetMainLayout"
          class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
          Reset
        </button>
        <button @click="cancelDraftMode"
          class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
          Cancel
        </button>
        <button @click="saveDraftMode"
          class="text-xs bg-amber-600 hover:bg-amber-700 text-white px-3 py-1.5 rounded-lg transition-colors font-medium">
          Save
        </button>
      </div>
    </div>

    <!-- Restore hidden sections (draft mode) -->
    <div v-if="isDraftMode && mainHiddenSections.size > 0" class="mb-4 flex flex-wrap items-center gap-2">
      <span class="text-xs text-gray-600">Hidden:</span>
      <button v-for="sid in mainHiddenSections" :key="sid" @click="showMainSection(sid as MainSectionId)"
        class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-gray-200 px-2.5 py-1 rounded-lg transition-colors flex items-center gap-1">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        {{ MAIN_SECTION_LABELS[sid as MainSectionId] }}
      </button>
    </div>

    <!-- Sections: ordered, draggable in draft mode -->
    <template v-for="sectionId in mainSectionOrder" :key="sectionId">
      <template v-if="isDraftMode || !mainHiddenSections.has(sectionId)">
        <div class="relative"
          :class="[
            isDraftMode ? 'select-none' : '',
            isDraftMode && mainHiddenSections.has(sectionId) ? 'opacity-40 saturate-50' : '',
            dragMainSectionId === sectionId ? 'opacity-50' : '',
          ]"
          :draggable="isDraftMode"
          @dragstart="isDraftMode ? onMainSectionDragStart($event, sectionId as MainSectionId) : undefined"
          @dragover.prevent="isDraftMode ? onMainSectionDragOver($event, sectionId as MainSectionId) : undefined"
          @dragend="isDraftMode ? onMainSectionDragEnd() : undefined">

          <!-- Draft mode section controls -->
          <div v-if="isDraftMode" class="flex items-center justify-between mb-1.5 px-1">
            <div class="flex items-center gap-2 text-xs text-amber-400/80 cursor-grab">
              <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                <circle cx="9" cy="6" r="1.5" /><circle cx="9" cy="12" r="1.5" /><circle cx="9" cy="18" r="1.5" />
                <circle cx="15" cy="6" r="1.5" /><circle cx="15" cy="12" r="1.5" /><circle cx="15" cy="18" r="1.5" />
              </svg>
              <span class="font-medium">{{ MAIN_SECTION_LABELS[sectionId as MainSectionId] }}</span>
            </div>
            <button
              @click.stop="mainHiddenSections.has(sectionId as MainSectionId) ? showMainSection(sectionId as MainSectionId) : hideMainSection(sectionId as MainSectionId)"
              class="text-xs px-2.5 py-1 rounded-lg bg-gray-800 hover:bg-gray-700 transition-colors"
              :class="mainHiddenSections.has(sectionId as MainSectionId) ? 'text-green-400' : 'text-gray-400 hover:text-red-400'">
              {{ mainHiddenSections.has(sectionId as MainSectionId) ? '+ Show' : '✕ Hide' }}
            </button>
          </div>

          <!-- ── Stats section ── -->
          <template v-if="sectionId === 'stats'">
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
              <StatCard label="Projects" :value="stats.projects" icon="projects" color="blue" to="/projects" />
              <StatCard label="Open Issues" :value="stats.openIssues" icon="issues" color="amber"
                to="/issues?status=open" />
              <StatCard label="In Progress" :value="stats.inProgress" icon="progress" color="indigo"
                to="/issues?status=in_progress" />
              <StatCard label="Agent Runs" :value="stats.agentRuns" icon="agents" color="green" to="/runs" />
            </div>
          </template>

          <!-- ── Activity section (Recent Issues + Projects) ── -->
          <template v-else-if="sectionId === 'activity'">
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
              <!-- Recent Issues -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                <div class="flex items-center justify-between mb-4">
                  <h2 class="font-semibold text-white">Recent Issues</h2>
                  <NuxtLink to="/issues" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                </div>
                <div class="space-y-2">
                  <NuxtLink v-for="issue in recentIssues" :key="issue.id"
                    :to="`/projects/${issue.projectId}/issues/${issue.number}`"
                    class="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-800 transition-colors block">
                    <span :class="statusDot(issue.status)" class="w-2 h-2 rounded-full shrink-0"></span>
                    <div class="flex-1 min-w-0">
                      <p class="text-sm text-gray-200 truncate">{{ issue.title }}</p>
                      <p class="text-xs text-gray-500">{{ issue.projectName }}</p>
                    </div>
                    <span :class="priorityBadge(issue.priority)" class="text-xs px-1.5 py-0.5 rounded font-medium">
                      {{ issue.priority }}
                    </span>
                  </NuxtLink>
                  <p v-if="recentIssues.length === 0" class="text-sm text-gray-500 py-4 text-center">No recent issues
                  </p>
                </div>
              </div>

              <!-- Projects Overview -->
              <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                <div class="flex items-center justify-between mb-4">
                  <h2 class="font-semibold text-white">Projects</h2>
                  <NuxtLink to="/projects" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                </div>
                <div class="space-y-2">
                  <NuxtLink v-for="project in projectsStore.projects.slice(0, 5)" :key="project.id"
                    :to="`/projects/${project.id}`"
                    class="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-800 transition-colors block">
                    <div :style="{ background: project.color || '#4c6ef5' }"
                      class="w-7 h-7 rounded-md flex items-center justify-center text-white text-xs font-bold shrink-0">
                      {{ project.name.charAt(0).toUpperCase() }}
                    </div>
                    <div class="flex-1 min-w-0">
                      <p class="text-sm text-gray-200 truncate">{{ project.name }}</p>
                      <p class="text-xs text-gray-500">{{ project.issueCount }} issues</p>
                    </div>
                    <svg class="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                    </svg>
                  </NuxtLink>
                  <p v-if="projectsStore.projects.length === 0" class="text-sm text-gray-500 py-4 text-center">No
                    projects yet</p>
                </div>
              </div>
            </div>
          </template>

          <!-- ── Chart section ── -->
          <template v-else-if="sectionId === 'chart'">
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
              <h2 class="font-semibold text-white mb-4">Issue Activity (last 14 days)</h2>
              <div v-if="issueHistory.length" class="overflow-x-auto">
                <svg :viewBox="`0 0 ${chartWidth} ${chartHeight}`" class="w-full" style="min-width:500px">
                  <line v-for="y in gridYValues" :key="y"
                    :x1="chartPad" :y1="yScale(y)" :x2="chartWidth - chartPad" :y2="yScale(y)"
                    stroke="#374151" stroke-width="1" />
                  <text v-for="y in gridYValues" :key="`yl-${y}`"
                    :x="chartPad - 6" :y="yScale(y) + 4"
                    text-anchor="end" fill="#6b7280" font-size="10">{{ y }}</text>
                  <polyline :points="linePoints('open')" fill="none" stroke="#f59e0b" stroke-width="2"
                    stroke-linejoin="round" />
                  <polyline :points="linePoints('inProgress')" fill="none" stroke="#6366f1" stroke-width="2"
                    stroke-linejoin="round" />
                  <polyline :points="linePoints('done')" fill="none" stroke="#22c55e" stroke-width="2"
                    stroke-linejoin="round" />
                  <text v-for="(entry, i) in issueHistory" :key="`xl-${i}`"
                    :x="xPos(i)" :y="chartHeight - 4"
                    text-anchor="middle" fill="#6b7280" font-size="9">{{ shortDate(entry.date) }}</text>
                </svg>
              </div>
              <div v-else class="py-8 text-center text-sm text-gray-500">No activity data yet</div>
              <div class="flex items-center gap-5 mt-3">
                <span class="flex items-center gap-1.5 text-xs text-gray-400">
                  <span class="w-3 h-0.5 bg-amber-400 rounded-full inline-block"></span> Open
                </span>
                <span class="flex items-center gap-1.5 text-xs text-gray-400">
                  <span class="w-3 h-0.5 bg-indigo-400 rounded-full inline-block"></span> In Progress
                </span>
                <span class="flex items-center gap-1.5 text-xs text-gray-400">
                  <span class="w-3 h-0.5 bg-green-400 rounded-full inline-block"></span> Done
                </span>
              </div>
            </div>
          </template>

          <!-- ── Runs section ── -->
          <template v-else-if="sectionId === 'runs'">
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
              <div class="flex items-center justify-between mb-4">
                <h2 class="font-semibold text-white">Recent Runs</h2>
                <div class="flex gap-1">
                  <button v-for="tab in runTabs" :key="tab"
                    :class="[
                      'px-3 py-1 text-xs font-medium rounded-md transition-colors',
                      activeRunTab === tab ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'
                    ]"
                    @click="activeRunTab = tab">{{ tab }}</button>
                </div>
              </div>

              <!-- CI/CD Runs -->
              <template v-if="activeRunTab === 'CI/CD'">
                <div v-if="runsStore.runs.length" class="rounded-lg border border-gray-800 overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-800/50">
                      <tr>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Status</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium hidden md:table-cell">Project
                        </th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Workflow</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium hidden md:table-cell">Branch
                        </th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium hidden md:table-cell">Commit
                        </th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Started</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Duration</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-800">
                      <tr v-for="run in runsStore.runs.slice(0, 10)" :key="run.id"
                        class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                        @click="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
                        <td class="px-3 py-2">
                          <CiCdStatusChip :runs="[run]" />
                        </td>
                        <td class="px-3 py-2 text-xs hidden md:table-cell">
                          <NuxtLink :to="`/projects/${run.projectId}/runs`"
                            class="text-brand-400 hover:text-brand-300 transition-colors"
                            @click.stop>
                            {{ run.projectName || '—' }}
                          </NuxtLink>
                        </td>
                        <td class="px-3 py-2 text-gray-300 text-xs">{{ run.workflow || '—' }}</td>
                        <td class="px-3 py-2 text-gray-300 font-mono text-xs hidden md:table-cell">{{ run.branch || '—'
                          }}</td>
                        <td class="px-3 py-2 text-gray-300 font-mono text-xs hidden md:table-cell">{{
                          run.commitSha?.slice(0, 7) || '—' }}</td>
                        <td class="px-3 py-2 text-gray-400 text-xs">{{ formatDate(run.startedAt) }}</td>
                        <td class="px-3 py-2 text-gray-400 text-xs">{{ run.status === CiCdRunStatus.WaitingForApproval
                          ? '—' : duration(run.startedAt, run.endedAt) }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <p v-else class="text-sm text-gray-500 py-6 text-center">No CI/CD runs yet</p>
              </template>

              <!-- Agent Runs -->
              <template v-else>
                <div v-if="runsStore.dashboardSessions.length" class="rounded-lg border border-gray-800 overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-800/50">
                      <tr>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Status</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Agent</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Issue</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium hidden md:table-cell">Project
                        </th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Started</th>
                        <th class="text-left px-3 py-2 text-xs text-gray-400 font-medium">Duration</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-800">
                      <tr v-for="session in runsStore.dashboardSessions.slice(0, 10)" :key="session.id"
                        class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                        @click="navigateTo(`/projects/${session.projectId}/runs/agent-sessions/${session.id}`)">
                        <td class="px-3 py-2">
                          <AgentSessionStatusChip :session="session" />
                        </td>
                        <td class="px-3 py-2 text-gray-300 text-xs">{{ session.agentName }}</td>
                        <td class="px-3 py-2 text-xs">
                          <NuxtLink :to="`/projects/${session.projectId}/issues/${session.issueNumber}`"
                            class="text-brand-400 hover:text-brand-300 transition-colors"
                            @click.stop>
                            #{{ formatIssueId(session.issueNumber, projectsStore.projects.find(p => p.id ===
                              session.projectId)) }} {{ session.issueTitle }}
                          </NuxtLink>
                        </td>
                        <td class="px-3 py-2 text-gray-400 text-xs hidden md:table-cell">{{ session.projectName }}</td>
                        <td class="px-3 py-2 text-gray-400 text-xs">{{ formatDate(session.startedAt) }}</td>
                        <td class="px-3 py-2 text-gray-400 text-xs">{{ duration(session.startedAt, session.endedAt) }}
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <p v-else class="text-sm text-gray-500 py-6 text-center">No agent runs yet</p>
              </template>
            </div>
          </template>
        </div>
      </template>
    </template>

    <!-- Customize button (when not in draft mode) -->
    <div v-if="!isDraftMode" class="flex justify-center mt-2 mb-4">
      <button @click="enterDraftMode"
        class="flex items-center gap-1.5 text-xs text-gray-600 hover:text-gray-400 transition-colors px-3 py-1.5 rounded-lg hover:bg-gray-800/50">
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M4 6h16M4 10h16M4 14h16M4 18h16" />
        </svg>
        Customize dashboard
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority, type IssueHistoryEntry, CiCdRunStatus } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useIssuesStore } from '~/stores/issues'
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { formatIssueId } from '~/composables/useIssueFormat'

const projectsStore = useProjectsStore()
const issuesStore = useIssuesStore()
const runsStore = useCiCdRunsStore()

const api = useApi()
const issueHistory = ref<IssueHistoryEntry[]>([])

const runTabs = ['CI/CD', 'Agent Runs'] as const
const activeRunTab = ref<typeof runTabs[number]>('CI/CD')

// ── Draft / Layout customization ──────────────────────────────────────────
type MainSectionId = 'stats' | 'activity' | 'chart' | 'runs'
const MAIN_LAYOUT_KEY = 'main-dashboard-layout'
const DEFAULT_MAIN_ORDER: MainSectionId[] = ['stats', 'activity', 'chart', 'runs']
const MAIN_SECTION_LABELS: Record<MainSectionId, string> = {
  stats: 'Overview Stats',
  activity: 'Recent Issues & Projects',
  chart: 'Issue Activity Chart',
  runs: 'Recent Runs',
}

const isDraftMode = ref(false)
const mainSectionOrder = ref<MainSectionId[]>([...DEFAULT_MAIN_ORDER])
const mainHiddenSections = ref<Set<MainSectionId>>(new Set())
let _draftSnapshot = { order: [...DEFAULT_MAIN_ORDER], hidden: [] as MainSectionId[] }

function loadMainLayout() {
  if (!import.meta.client) return
  try {
    const saved = localStorage.getItem(MAIN_LAYOUT_KEY)
    if (saved) {
      const { order, hidden } = JSON.parse(saved) as { order: MainSectionId[]; hidden: MainSectionId[] }
      if (Array.isArray(order) && order.length) mainSectionOrder.value = order
      if (Array.isArray(hidden)) mainHiddenSections.value = new Set(hidden)
    }
  } catch { /* ignore */ }
}

function saveMainLayout() {
  if (!import.meta.client) return
  localStorage.setItem(MAIN_LAYOUT_KEY, JSON.stringify({
    order: mainSectionOrder.value,
    hidden: [...mainHiddenSections.value],
  }))
}

function enterDraftMode() {
  _draftSnapshot = { order: [...mainSectionOrder.value], hidden: [...mainHiddenSections.value] }
  isDraftMode.value = true
}

function saveDraftMode() {
  saveMainLayout()
  isDraftMode.value = false
}

function cancelDraftMode() {
  mainSectionOrder.value = _draftSnapshot.order
  mainHiddenSections.value = new Set(_draftSnapshot.hidden)
  isDraftMode.value = false
}

function resetMainLayout() {
  mainSectionOrder.value = [...DEFAULT_MAIN_ORDER]
  mainHiddenSections.value = new Set()
}

function hideMainSection(id: MainSectionId) {
  mainHiddenSections.value = new Set([...mainHiddenSections.value, id])
}

function showMainSection(id: MainSectionId) {
  const s = new Set(mainHiddenSections.value)
  s.delete(id)
  mainHiddenSections.value = s
}

const dragMainSectionId = ref<MainSectionId | null>(null)

function onMainSectionDragStart(e: DragEvent, id: MainSectionId) {
  dragMainSectionId.value = id
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onMainSectionDragOver(_e: DragEvent, id: MainSectionId) {
  if (!dragMainSectionId.value || id === dragMainSectionId.value) return
  const from = mainSectionOrder.value.indexOf(dragMainSectionId.value)
  const to = mainSectionOrder.value.indexOf(id)
  if (from === -1 || to === -1 || from === to) return
  const newOrder = [...mainSectionOrder.value]
  newOrder.splice(from, 1)
  newOrder.splice(to, 0, dragMainSectionId.value)
  mainSectionOrder.value = newOrder
}

function onMainSectionDragEnd() {
  dragMainSectionId.value = null
}

onMounted(async () => {
  if (import.meta.client) loadMainLayout()
  await Promise.allSettled([
    projectsStore.fetchProjects(),
    issuesStore.fetchIssues(),
    runsStore.fetchRuns(),
    runsStore.fetchDashboardSessions(),
    api.get<IssueHistoryEntry[]>('/api/dashboard/issue-history').then(data => { issueHistory.value = data }).catch((e) => { console.error('Failed to load issue history', e) }),
  ])
})

const stats = computed(() => ({
  projects: projectsStore.projects.length,
  openIssues: issuesStore.issues.filter(i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled).length,
  inProgress: issuesStore.issues.filter(i => i.status === IssueStatus.InProgress).length,
  agentRuns: runsStore.dashboardSessions.length
}))

const recentIssues = computed(() =>
  issuesStore.issues.slice(0, 5).map(i => ({
    ...i,
    projectName: projectsStore.projects.find(p => p.id === i.projectId)?.name ?? ''
  }))
)

// Chart helpers
const chartWidth = 600
const chartHeight = 160
const chartPad = 36

const chartMaxY = computed(() => {
  const max = Math.max(...issueHistory.value.flatMap(e => [e.open, e.inProgress, e.done]), 1)
  return Math.ceil(max / 5) * 5 || 5
})

const gridYValues = computed(() => {
  const step = Math.ceil(chartMaxY.value / 4)
  return [0, step, step * 2, step * 3, step * 4].filter(v => v <= chartMaxY.value + step)
})

function yScale(val: number) {
  const plotH = chartHeight - 30
  return plotH - (val / chartMaxY.value) * plotH + 5
}

function xPos(i: number) {
  const n = issueHistory.value.length
  const plotW = chartWidth - chartPad * 2
  return chartPad + (n > 1 ? (i / (n - 1)) * plotW : plotW / 2)
}

function linePoints(key: 'open' | 'inProgress' | 'done') {
  return issueHistory.value.map((e, i) => `${xPos(i)},${yScale(e[key])}`).join(' ')
}

function shortDate(d: string) {
  const dt = new Date(d)
  return `${dt.getMonth() + 1}/${dt.getDate()}`
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function duration(start: string, end?: string) {
  const ms = (end ? new Date(end).getTime() : Date.now()) - new Date(start).getTime()
  if (ms < 0) return '—'
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ${s % 60}s`
  return `${Math.floor(m / 60)}h ${m % 60}m`
}

function statusDot(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

function priorityBadge(priority: IssuePriority) {
  const map: Record<IssuePriority, string> = {
    [IssuePriority.Urgent]: 'bg-red-900/60 text-red-300',
    [IssuePriority.High]: 'bg-orange-900/60 text-orange-300',
    [IssuePriority.Medium]: 'bg-yellow-900/60 text-yellow-300',
    [IssuePriority.Low]: 'bg-blue-900/60 text-blue-300',
    [IssuePriority.NoPriority]: 'bg-gray-800 text-gray-400'
  }
  return map[priority] ?? 'bg-gray-800 text-gray-400'
}

// Stat card component
const StatCard = defineComponent({
  props: { label: String, value: Number, icon: String, color: String, to: String },
  setup(props) {
    const colorMap: Record<string, string> = {
      blue: 'bg-blue-900/30 text-blue-400',
      amber: 'bg-amber-900/30 text-amber-400',
      indigo: 'bg-indigo-900/30 text-indigo-400',
      green: 'bg-green-900/30 text-green-400'
    }
    const baseClass = 'bg-gray-900 border border-gray-800 rounded-xl p-5'
    const inner = () => h('div', { class: 'flex items-center justify-between' }, [
      h('div', [
        h('p', { class: 'text-sm text-gray-400' }, props.label),
        h('p', { class: 'text-3xl font-bold text-white mt-1' }, props.value ?? 0)
      ]),
      h('div', { class: `w-10 h-10 rounded-lg flex items-center justify-center ${colorMap[props.color!] ?? colorMap.blue}` },
        h('svg', { class: 'w-5 h-5', fill: 'none', stroke: 'currentColor', viewBox: '0 0 24 24' },
          h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', 'stroke-width': '2', d: 'M13 7h8m0 0v8m0-8l-8 8-4-4-6 6' })
        )
      )
    ])
    return () => props.to
      ? h(resolveComponent('NuxtLink'), { to: props.to, class: `${baseClass} block hover:border-gray-700 transition-colors` }, { default: inner })
      : h('div', { class: baseClass }, inner())
  }
})
</script>
