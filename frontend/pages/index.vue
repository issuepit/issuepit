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
        <span class="text-amber-400/70 text-xs hidden sm:inline">— drag to reorder · configure each section</span>
      </div>
      <div class="flex items-center gap-1.5">
        <button @click="resetLayout"
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
    <div v-if="isDraftMode && hiddenSections.size > 0" class="mb-4 flex flex-wrap items-center gap-2">
      <span class="text-xs text-gray-600">Hidden:</span>
      <button v-for="sid in hiddenSections" :key="sid" @click="showSection(sid as MainSectionId)"
        class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-gray-200 px-2.5 py-1 rounded-lg transition-colors flex items-center gap-1">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        {{ SECTION_LABELS[sid as MainSectionId] }}
      </button>
    </div>

    <!-- 12-column grid layout -->
    <div class="grid grid-cols-12 gap-4 items-start">
      <template v-for="item in renderedItems" :key="item.type === 'tabgroup' ? item.key : item.sid">
        <div
          :class="[
            itemColSpanClass(item),
            isDraftMode ? 'select-none' : '',
            item.type === 'section' && isDraftMode && sectionCfg(item.sid).hidden ? 'opacity-40 saturate-50' : '',
            dragSectionId === (item.type === 'section' ? item.sid : item.sections[0]) && isDraftMode ? 'opacity-50' : '',
          ]"
          :draggable="isDraftMode"
          @dragstart="isDraftMode && item.type === 'section' ? onDragStart($event, item.sid) : undefined"
          @dragover.prevent="isDraftMode ? onDragOver($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
          @dragend="isDraftMode ? onDragEnd() : undefined">

          <!-- Draft mode config bar -->
          <template v-if="isDraftMode">
            <!-- Single section config bar -->
            <template v-if="item.type === 'section'">
              <div class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
                <!-- Drag handle + label -->
                <div class="flex items-center gap-1.5 text-xs text-amber-400/80 cursor-grab mr-auto">
                  <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
                    <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
                    <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
                  </svg>
                  <span class="font-semibold">{{ SECTION_LABELS[item.sid] }}</span>
                </div>
                <!-- Display mode pills -->
                <div v-if="SECTION_DISPLAY_MODES[item.sid]?.length" class="flex items-center gap-0.5">
                  <button v-for="mode in SECTION_DISPLAY_MODES[item.sid]" :key="mode"
                    @click.stop="updateCfg(item.sid, { displayMode: mode as MainDisplayMode })"
                    :class="sectionCfg(item.sid).displayMode === mode ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
                    class="text-xs px-1.5 py-0.5 rounded transition-colors capitalize">{{ mode }}</button>
                </div>
                <!-- Max items -->
                <div v-if="SECTION_HAS_MAX_ITEMS.has(item.sid) && sectionCfg(item.sid).displayMode !== 'count'" class="flex items-center gap-0.5">
                  <span class="text-xs text-gray-600">#</span>
                  <button v-for="n in [3,5,8,10]" :key="n"
                    @click.stop="updateCfg(item.sid, { maxItems: n })"
                    :class="sectionCfg(item.sid).maxItems === n ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
                    class="text-xs w-5 h-5 flex items-center justify-center rounded transition-colors">{{ n }}</button>
                </div>
                <!-- Width -->
                <div class="flex items-center gap-0.5">
                  <button v-for="w in (['xs','sm','md','lg'] as MainWidth[])" :key="w"
                    @click.stop="updateCfg(item.sid, { width: w })"
                    :class="sectionCfg(item.sid).width === w ? 'bg-gray-600 text-white' : 'text-gray-500 hover:text-gray-300'"
                    class="text-xs px-1.5 py-0.5 rounded transition-colors">{{ WIDTH_LABELS[w] }}</button>
                </div>
                <!-- Tab with next -->
                <button v-if="SECTION_CAN_TAB.has(item.sid) && layout.order.indexOf(item.sid) < layout.order.length - 1"
                  @click.stop="toggleTabGroupWithNext(item.sid)"
                  class="text-xs px-1.5 py-0.5 rounded transition-colors bg-gray-800 hover:bg-gray-700"
                  :class="sectionCfg(item.sid).tabGroup !== null ? 'text-brand-400' : 'text-gray-500 hover:text-gray-300'"
                  :title="sectionCfg(item.sid).tabGroup !== null ? 'Ungroup from next' : 'Combine with next as tabs'">
                  {{ sectionCfg(item.sid).tabGroup !== null ? '⊖ Ungroup' : '⊕ Tab with ↓' }}
                </button>
                <!-- Hide/Show -->
                <button @click.stop="sectionCfg(item.sid).hidden ? showSection(item.sid) : hideSection(item.sid)"
                  :class="sectionCfg(item.sid).hidden ? 'text-green-400' : 'text-gray-400 hover:text-red-400'"
                  class="text-xs px-1.5 py-0.5 rounded bg-gray-800 hover:bg-gray-700 transition-colors">
                  {{ sectionCfg(item.sid).hidden ? '+ Show' : '✕ Hide' }}
                </button>
              </div>
            </template>
            <!-- Tab group config bar -->
            <template v-else>
              <div class="mb-1.5 rounded-lg bg-gray-900/60 border border-gray-800 px-2 py-1.5 flex items-center gap-3 flex-wrap">
                <div class="flex items-center gap-1.5 text-xs text-amber-400/80 cursor-grab">
                  <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
                    <circle cx="9" cy="6" r="1.5"/><circle cx="9" cy="12" r="1.5"/><circle cx="9" cy="18" r="1.5"/>
                    <circle cx="15" cy="6" r="1.5"/><circle cx="15" cy="12" r="1.5"/><circle cx="15" cy="18" r="1.5"/>
                  </svg>
                  <span class="font-semibold text-amber-300">Tab group:</span>
                  <span class="text-amber-400/80">{{ item.sections.map(s => SECTION_LABELS[s]).join(' + ') }}</span>
                </div>
                <button @click.stop="toggleTabGroupWithNext(item.sections[0])"
                  class="text-xs px-2 py-0.5 rounded bg-gray-800 hover:bg-gray-700 text-brand-400 hover:text-red-400 transition-colors ml-auto">
                  ⊖ Split tabs
                </button>
              </div>
            </template>
          </template>

          <!-- Content area -->
          <div :class="isDraftMode && item.type === 'section' && sectionCfg(item.sid).hidden ? 'opacity-30 saturate-0 pointer-events-none' : ''">

            <!-- Tab nav (for tabgroup) -->
            <div v-if="item.type === 'tabgroup'"
              class="bg-gray-900 border border-gray-800 rounded-t-xl border-b-0 flex overflow-x-auto">
              <button v-for="sec in item.sections" :key="sec"
                @click="setActiveTab(item.key, sec)"
                :class="[
                  getActiveTab(item.key, item.sections) === sec
                    ? 'text-white border-b-2 border-brand-400 bg-gray-800/50'
                    : 'text-gray-500 hover:text-gray-300',
                ]"
                class="px-4 py-2.5 text-sm font-medium transition-colors whitespace-nowrap flex-shrink-0">
                {{ SECTION_LABELS[sec] }}
              </button>
            </div>

            <!-- Section(s) content -->
            <template v-for="sid in (item.type === 'tabgroup' ? item.sections : [item.sid])" :key="sid">
              <div v-show="item.type !== 'tabgroup' || getActiveTab(item.key, item.sections) === sid"
                :class="item.type === 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-b-xl p-5' : ''">

                <!-- ── statProjects ── -->
                <template v-if="sid === 'statProjects'">
                  <NuxtLink to="/projects"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors mb-4">
                    <p class="text-sm text-gray-400">Projects</p>
                    <p class="text-3xl font-bold text-blue-400 mt-1">{{ stats.projects }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statOpenIssues ── -->
                <template v-else-if="sid === 'statOpenIssues'">
                  <NuxtLink to="/issues?status=open"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors mb-4">
                    <p class="text-sm text-gray-400">Open Issues</p>
                    <p class="text-3xl font-bold text-amber-400 mt-1">{{ stats.openIssues }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statInProgress ── -->
                <template v-else-if="sid === 'statInProgress'">
                  <NuxtLink to="/issues?status=in_progress"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors mb-4">
                    <p class="text-sm text-gray-400">In Progress</p>
                    <p class="text-3xl font-bold text-indigo-400 mt-1">{{ stats.inProgress }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statAgentRuns ── -->
                <template v-else-if="sid === 'statAgentRuns'">
                  <NuxtLink to="/runs"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors mb-4">
                    <p class="text-sm text-gray-400">Agent Runs</p>
                    <p class="text-3xl font-bold text-green-400 mt-1">{{ stats.agentRuns }}</p>
                  </NuxtLink>
                </template>

                <!-- ── recentIssues ── -->
                <template v-else-if="sid === 'recentIssues'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Recent Issues</h2>
                      <NuxtLink to="/issues" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('recentIssues').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ stats.openIssues }}
                        <p class="text-sm text-gray-400 font-normal mt-1">open issues</p>
                      </div>
                    </template>
                    <template v-else>
                      <div class="space-y-1">
                        <NuxtLink v-for="issue in recentIssues" :key="issue.id"
                          :to="`/projects/${issue.projectId}/issues/${issue.number}`"
                          class="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-800 transition-colors block">
                          <span :class="statusDot(issue.status)" class="w-2 h-2 rounded-full shrink-0"></span>
                          <div class="flex-1 min-w-0">
                            <p class="text-sm text-gray-200 truncate">{{ issue.title }}</p>
                            <p class="text-xs text-gray-500">{{ issue.projectName }}</p>
                          </div>
                          <span :class="priorityBadge(issue.priority)" class="text-xs px-1.5 py-0.5 rounded font-medium shrink-0">
                            {{ issue.priority }}
                          </span>
                        </NuxtLink>
                        <p v-if="recentIssues.length === 0" class="text-sm text-gray-500 py-4 text-center">No recent issues</p>
                      </div>
                    </template>
                  </div>
                </template>

                <!-- ── recentProjects ── -->
                <template v-else-if="sid === 'recentProjects'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Projects</h2>
                      <NuxtLink to="/projects" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('recentProjects').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ stats.projects }}
                        <p class="text-sm text-gray-400 font-normal mt-1">projects</p>
                      </div>
                    </template>
                    <template v-else>
                      <div class="space-y-1">
                        <NuxtLink v-for="project in projectsStore.projects.slice(0, sectionCfg('recentProjects').maxItems)" :key="project.id"
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
                          <svg class="w-4 h-4 text-gray-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                          </svg>
                        </NuxtLink>
                        <p v-if="projectsStore.projects.length === 0" class="text-sm text-gray-500 py-4 text-center">No projects yet</p>
                      </div>
                    </template>
                  </div>
                </template>

                <!-- ── chart ── -->
                <template v-else-if="sid === 'chart'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
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

                <!-- ── cicdRuns ── -->
                <template v-else-if="sid === 'cicdRuns'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">CI/CD Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('cicdRuns').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.runs.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">CI/CD runs</p>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="cicdRunsItems.length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="run in cicdRunsItems" :key="run.id"
                              class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                              @click="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
                              <td class="px-3 py-2"><CiCdStatusChip :runs="[run]" /></td>
                              <td class="px-3 py-2 text-gray-300 text-xs truncate max-w-[8rem]">{{ run.workflow || '—' }}</td>
                              <td class="px-3 py-2 text-gray-300 font-mono text-xs hidden md:table-cell">{{ run.branch || '—' }}</td>
                              <td class="px-3 py-2 text-gray-400 text-xs">{{ formatDate(run.startedAt) }}</td>
                            </tr>
                          </tbody>
                        </table>
                      </div>
                      <p v-else class="text-sm text-gray-500 py-6 text-center">No CI/CD runs yet</p>
                    </template>
                  </div>
                </template>

                <!-- ── agentRunsList ── -->
                <template v-else-if="sid === 'agentRunsList'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Agent Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('agentRunsList').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.dashboardSessions.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">agent runs</p>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="agentRunsItems.length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="session in agentRunsItems" :key="session.id"
                              class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                              @click="navigateTo(`/projects/${session.projectId}/runs/agent-sessions/${session.id}`)">
                              <td class="px-3 py-2"><AgentSessionStatusChip :session="session" /></td>
                              <td class="px-3 py-2 text-xs">
                                <NuxtLink :to="`/projects/${session.projectId}/issues/${session.issueNumber}`"
                                  class="text-brand-400 hover:text-brand-300 transition-colors" @click.stop>
                                  #{{ formatIssueId(session.issueNumber, projectsStore.projects.find(p => p.id === session.projectId)) }} {{ session.issueTitle }}
                                </NuxtLink>
                              </td>
                              <td class="px-3 py-2 text-gray-400 text-xs hidden md:table-cell">{{ session.projectName }}</td>
                              <td class="px-3 py-2 text-gray-400 text-xs">{{ formatDate(session.startedAt) }}</td>
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
          </div>
        </div>
      </template>
    </div>

    <!-- Customize button -->
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
import { IssueStatus, IssuePriority, type IssueHistoryEntry } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useIssuesStore } from '~/stores/issues'
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { formatIssueId } from '~/composables/useIssueFormat'

const projectsStore = useProjectsStore()
const issuesStore = useIssuesStore()
const runsStore = useCiCdRunsStore()

const api = useApi()
const issueHistory = ref<IssueHistoryEntry[]>([])

// ── Layout / Draft mode ───────────────────────────────────────────────────
type MainSectionId =
  | 'statProjects' | 'statOpenIssues' | 'statInProgress' | 'statAgentRuns'
  | 'recentIssues' | 'recentProjects' | 'chart' | 'cicdRuns' | 'agentRunsList'

type MainWidth = 'xs' | 'sm' | 'md' | 'lg'
type MainDisplayMode = 'list' | 'count'

interface MainSectionConfig {
  hidden: boolean
  width: MainWidth
  displayMode: MainDisplayMode
  maxItems: number
  tabGroup: string | null
}

interface MainLayout {
  order: MainSectionId[]
  configs: Record<MainSectionId, MainSectionConfig>
}

const MAIN_LAYOUT_KEY = 'main-dashboard-layout-v2'

const DEFAULT_ORDER: MainSectionId[] = [
  'statProjects', 'statOpenIssues', 'statInProgress', 'statAgentRuns',
  'recentIssues', 'recentProjects', 'chart', 'cicdRuns', 'agentRunsList',
]

const DEFAULT_CONFIGS: Record<MainSectionId, MainSectionConfig> = {
  statProjects:   { hidden: false, width: 'xs', displayMode: 'list', maxItems: 5, tabGroup: null },
  statOpenIssues: { hidden: false, width: 'xs', displayMode: 'list', maxItems: 5, tabGroup: null },
  statInProgress: { hidden: false, width: 'xs', displayMode: 'list', maxItems: 5, tabGroup: null },
  statAgentRuns:  { hidden: false, width: 'xs', displayMode: 'list', maxItems: 5, tabGroup: null },
  recentIssues:   { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null },
  recentProjects: { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null },
  chart:          { hidden: false, width: 'lg', displayMode: 'list', maxItems: 5, tabGroup: null },
  cicdRuns:       { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null },
  agentRunsList:  { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null },
}

const SECTION_LABELS: Record<MainSectionId, string> = {
  statProjects:   'Projects',
  statOpenIssues: 'Open Issues',
  statInProgress: 'In Progress',
  statAgentRuns:  'Agent Runs',
  recentIssues:   'Recent Issues',
  recentProjects: 'Recent Projects',
  chart:          'Issue Activity Chart',
  cicdRuns:       'CI/CD Runs',
  agentRunsList:  'Agent Runs List',
}

const WIDTH_LABELS: Record<MainWidth, string> = { xs: '1/4', sm: '1/3', md: '1/2', lg: 'Full' }

const SECTION_DISPLAY_MODES: Partial<Record<MainSectionId, MainDisplayMode[]>> = {
  recentIssues:   ['list', 'count'],
  recentProjects: ['list', 'count'],
  cicdRuns:       ['list', 'count'],
  agentRunsList:  ['list', 'count'],
}

const SECTION_HAS_MAX_ITEMS = new Set<MainSectionId>(['recentIssues', 'recentProjects', 'cicdRuns', 'agentRunsList'])
const SECTION_CAN_TAB = new Set<MainSectionId>(['recentIssues', 'recentProjects', 'cicdRuns', 'agentRunsList'])

const layout = ref<MainLayout>({
  order: [...DEFAULT_ORDER],
  configs: JSON.parse(JSON.stringify(DEFAULT_CONFIGS)) as Record<MainSectionId, MainSectionConfig>,
})

const isDraftMode = ref(false)
let _draftSnapshot: MainLayout | null = null

const hiddenSections = computed(() =>
  new Set(layout.value.order.filter(s => layout.value.configs[s]?.hidden))
)

function sectionCfg(s: MainSectionId): MainSectionConfig {
  return layout.value.configs[s] ?? { ...DEFAULT_CONFIGS[s] }
}

function updateCfg(s: MainSectionId, patch: Partial<MainSectionConfig>) {
  layout.value.configs[s] = { ...sectionCfg(s), ...patch }
}

function hideSection(id: MainSectionId) { updateCfg(id, { hidden: true }) }
function showSection(id: MainSectionId) { updateCfg(id, { hidden: false }) }

function loadLayout() {
  if (!import.meta.client) return
  try {
    const saved = localStorage.getItem(MAIN_LAYOUT_KEY)
    if (saved) {
      const parsed = JSON.parse(saved) as MainLayout
      if (Array.isArray(parsed.order) && parsed.order.length) {
        const valid = parsed.order.filter(s => s in DEFAULT_CONFIGS) as MainSectionId[]
        const missing = DEFAULT_ORDER.filter(s => !valid.includes(s))
        layout.value.order = [...valid, ...missing]
      }
      if (parsed.configs) {
        for (const sid of DEFAULT_ORDER) {
          if (parsed.configs[sid]) {
            layout.value.configs[sid] = { ...DEFAULT_CONFIGS[sid], ...parsed.configs[sid] }
          }
        }
      }
    }
  } catch { /* ignore */ }
}

function saveLayout() {
  if (!import.meta.client) return
  localStorage.setItem(MAIN_LAYOUT_KEY, JSON.stringify(layout.value))
}

function enterDraftMode() {
  _draftSnapshot = JSON.parse(JSON.stringify(layout.value))
  isDraftMode.value = true
}

function saveDraftMode() {
  saveLayout()
  isDraftMode.value = false
}

function cancelDraftMode() {
  if (_draftSnapshot) layout.value = JSON.parse(JSON.stringify(_draftSnapshot))
  isDraftMode.value = false
}

function resetLayout() {
  layout.value = { order: [...DEFAULT_ORDER], configs: JSON.parse(JSON.stringify(DEFAULT_CONFIGS)) }
}

// ── Drag & drop ──────────────────────────────────────────────────────────
const dragSectionId = ref<MainSectionId | null>(null)

function onDragStart(e: DragEvent, id: MainSectionId) {
  dragSectionId.value = id
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onDragOver(_e: DragEvent, id: MainSectionId) {
  if (!dragSectionId.value || id === dragSectionId.value) return
  const from = layout.value.order.indexOf(dragSectionId.value)
  const to = layout.value.order.indexOf(id)
  if (from === -1 || to === -1 || from === to) return
  const newOrder = [...layout.value.order]
  newOrder.splice(from, 1)
  newOrder.splice(to, 0, dragSectionId.value)
  layout.value.order = newOrder
}

function onDragEnd() {
  dragSectionId.value = null
}

// ── Tab group logic ──────────────────────────────────────────────────────
function toggleTabGroupWithNext(sid: MainSectionId) {
  const cfg = sectionCfg(sid)
  if (cfg.tabGroup !== null) {
    const grp = cfg.tabGroup
    updateCfg(sid, { tabGroup: null })
    for (const s of layout.value.order) {
      if (s !== sid && sectionCfg(s).tabGroup === grp) updateCfg(s, { tabGroup: null })
    }
  } else {
    const visible = layout.value.order.filter(s => !sectionCfg(s).hidden)
    const idx = visible.indexOf(sid)
    const nextSid = idx >= 0 && idx + 1 < visible.length ? visible[idx + 1] : null
    if (!nextSid) return
    const nextCfg = sectionCfg(nextSid)
    const grp = nextCfg.tabGroup ?? `grp-${Date.now()}`
    updateCfg(sid, { tabGroup: grp })
    if (nextCfg.tabGroup === null) updateCfg(nextSid, { tabGroup: grp })
  }
}

const activeTabInGroup = ref<Record<string, MainSectionId>>({})

function getActiveTab(key: string, sections: MainSectionId[]): MainSectionId {
  return activeTabInGroup.value[key] || sections[0]
}

function setActiveTab(key: string, sid: MainSectionId) {
  activeTabInGroup.value[key] = sid
}

// ── Rendered items (with tab group collapsing) ───────────────────────────
type RenderItem =
  | { type: 'section'; sid: MainSectionId }
  | { type: 'tabgroup'; key: string; sections: MainSectionId[] }

const renderedItems = computed((): RenderItem[] => {
  const visible = layout.value.order.filter(s => isDraftMode.value || !sectionCfg(s).hidden)
  const items: RenderItem[] = []
  let i = 0
  while (i < visible.length) {
    const sid = visible[i]
    const grp = sectionCfg(sid).tabGroup
    if (grp !== null) {
      const grpSids: MainSectionId[] = [sid]
      let j = i + 1
      while (j < visible.length && sectionCfg(visible[j]).tabGroup === grp) {
        grpSids.push(visible[j])
        j++
      }
      if (grpSids.length > 1) {
        if (!activeTabInGroup.value[grp]) activeTabInGroup.value[grp] = grpSids[0]
        items.push({ type: 'tabgroup', key: grp, sections: grpSids })
        i = j
        continue
      }
    }
    items.push({ type: 'section', sid })
    i++
  }
  return items
})

function mainColSpanClass(width: MainWidth): string {
  if (width === 'xs') return 'col-span-12 sm:col-span-6 lg:col-span-3'
  if (width === 'sm') return 'col-span-12 sm:col-span-6 lg:col-span-4'
  if (width === 'md') return 'col-span-12 lg:col-span-6'
  return 'col-span-12'
}

function itemColSpanClass(item: RenderItem): string {
  if (item.type === 'tabgroup') return 'col-span-12'
  return mainColSpanClass(sectionCfg(item.sid).width)
}

// ── Data ─────────────────────────────────────────────────────────────────
const stats = computed(() => ({
  projects: projectsStore.projects.length,
  openIssues: issuesStore.issues.filter(i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled).length,
  inProgress: issuesStore.issues.filter(i => i.status === IssueStatus.InProgress).length,
  agentRuns: runsStore.dashboardSessions.length
}))

const recentIssues = computed(() =>
  issuesStore.issues.slice(0, sectionCfg('recentIssues').maxItems).map(i => ({
    ...i,
    projectName: projectsStore.projects.find(p => p.id === i.projectId)?.name ?? ''
  }))
)

const cicdRunsItems = computed(() => runsStore.runs.slice(0, sectionCfg('cicdRuns').maxItems))
const agentRunsItems = computed(() => runsStore.dashboardSessions.slice(0, sectionCfg('agentRunsList').maxItems))

// ── Chart helpers ─────────────────────────────────────────────────────────
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


onMounted(async () => {
  if (import.meta.client) loadLayout()
  await Promise.allSettled([
    projectsStore.fetchProjects(),
    issuesStore.fetchIssues(),
    runsStore.fetchRuns(),
    runsStore.fetchDashboardSessions(),
    api.get<IssueHistoryEntry[]>('/api/dashboard/issue-history')
      .then(data => { issueHistory.value = data })
      .catch((e) => { console.error('Failed to load issue history', e) }),
  ])
})
</script>
