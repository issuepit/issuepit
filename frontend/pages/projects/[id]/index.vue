<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentProject">
      <!-- Header -->
      <div class="flex items-center justify-between gap-3 mb-2">
        <div class="flex items-center gap-3 min-w-0">
          <NuxtLink to="/projects" class="text-gray-500 hover:text-gray-300 transition-colors shrink-0">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
            </svg>
          </NuxtLink>
          <div :style="{ background: store.currentProject.color || '#4c6ef5' }"
            class="w-8 h-8 rounded-md flex items-center justify-center text-white font-bold shrink-0">
            {{ store.currentProject.name.charAt(0).toUpperCase() }}
          </div>
          <h1 class="text-2xl font-bold text-white truncate">{{ store.currentProject.name }}</h1>
          <span v-if="store.currentProject.isPrivate"
            class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full shrink-0">Private</span>
        </div>
        <!-- Header actions: Members + Settings -->
        <div class="flex items-center gap-2 shrink-0">
          <NuxtLink :to="`/projects/${id}/members`"
            class="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            Members
          </NuxtLink>
          <NuxtLink :to="`/projects/${id}/settings`"
            class="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-lg hover:bg-gray-800 transition-colors"
            title="Project Settings">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            Settings
          </NuxtLink>
        </div>
      </div>
      <p v-if="store.currentProject.description" class="text-gray-400 text-sm mb-6 ml-16">
        {{ store.currentProject.description }}
      </p>

      <!-- Stats Row -->
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 mb-6">
        <NuxtLink :to="`/projects/${id}/issues`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group">
          <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
            {{ store.currentProject.issueCount }}
          </p>
          <p class="text-xs text-gray-500 mt-0.5">Issues</p>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/members`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group">
          <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
            {{ store.currentProject.memberCount }}
          </p>
          <p class="text-xs text-gray-500 mt-0.5">Members</p>
        </NuxtLink>

        <!-- Agent Runs with live indicator -->
        <NuxtLink :to="`/projects/${id}/runs?tab=agent`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group relative">
          <div class="flex items-center justify-center gap-1.5">
            <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
              {{ runsStore.agentSessions.length }}
            </p>
            <span v-if="isConnected" class="w-2 h-2 rounded-full bg-green-400 animate-pulse shrink-0" title="Live updates connected" />
            <span v-else class="w-2 h-2 rounded-full bg-gray-600 shrink-0" title="Live updates disconnected" />
          </div>
          <p class="text-xs text-gray-500 mt-0.5">Agent Runs</p>
        </NuxtLink>

        <!-- CI/CD Runs with live indicator -->
        <NuxtLink :to="`/projects/${id}/runs`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group">
          <div class="flex items-center justify-center gap-1.5">
            <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
              {{ runsStore.runs.length }}
            </p>
            <span v-if="isConnected" class="w-2 h-2 rounded-full bg-green-400 animate-pulse shrink-0" title="Live updates connected" />
            <span v-else class="w-2 h-2 rounded-full bg-gray-600 shrink-0" title="Live updates disconnected" />
          </div>
          <p class="text-xs text-gray-500 mt-0.5">CI/CD Runs</p>
        </NuxtLink>

        <!-- MCP Servers -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 text-center">
          <p class="text-2xl font-bold text-white">{{ mcpCount }}</p>
          <p class="text-xs text-gray-500 mt-0.5">MCP Servers</p>
        </div>
      </div>

      <!-- Quick Navigation -->
      <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
        <NuxtLink :to="`/projects/${id}/code`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-orange-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Code</span>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/issues`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-blue-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Issues</span>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/kanban`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-purple-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Kanban</span>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/runs`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-yellow-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Runs</span>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/review`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-teal-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-teal-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Review</span>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/badges`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 flex items-center gap-3 transition-colors group">
          <div class="w-8 h-8 bg-pink-900/30 rounded-lg flex items-center justify-center shrink-0">
            <svg class="w-4 h-4 text-pink-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
            </svg>
          </div>
          <span class="font-medium text-sm text-white group-hover:text-brand-300 transition-colors">Badges</span>
        </NuxtLink>
      </div>

      <!-- Milestones (shown if any exist) -->
      <div v-if="openMilestones.length" class="mb-6">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-white flex items-center gap-2">
            <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9" />
            </svg>
            Milestones
          </h2>
          <NuxtLink :to="`/projects/${id}/milestones`"
            class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
            View all →
          </NuxtLink>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          <NuxtLink v-for="milestone in openMilestones" :key="milestone.id"
            :to="`/projects/${id}/milestones/${milestone.id}`"
            class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 transition-colors group">
            <div class="flex items-start justify-between gap-2 mb-2">
              <span class="text-sm font-medium text-white group-hover:text-brand-300 transition-colors truncate">
                {{ milestone.title }}
              </span>
              <span class="text-xs bg-green-900/40 text-green-400 px-1.5 py-0.5 rounded-full shrink-0">Open</span>
            </div>
            <p v-if="milestone.description" class="text-xs text-gray-500 line-clamp-1 mb-2">
              {{ milestone.description }}
            </p>
            <p v-if="milestone.dueDate" class="text-xs text-gray-500">
              Due {{ formatDate(milestone.dueDate) }}
            </p>
          </NuxtLink>
        </div>
      </div>

      <!-- Recent Runs -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        <!-- Agent Runs -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-semibold text-white flex items-center gap-2">
              <svg class="w-4 h-4 text-fuchsia-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
              </svg>
              Recent Agent Runs
              <!-- WS connection indicator -->
              <span v-if="isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal">
                <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
                Live
              </span>
              <span v-else class="flex items-center gap-1 text-xs text-gray-600 font-normal">
                <span class="w-1.5 h-1.5 rounded-full bg-gray-600" />
                Offline
              </span>
            </h2>
            <NuxtLink :to="`/projects/${id}/runs?tab=agent`"
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
              View all →
            </NuxtLink>
          </div>
          <div v-if="recentAgentSessions.length" class="space-y-2">
            <div v-for="session in recentAgentSessions" :key="session.id"
              class="flex items-center gap-3 py-2 border-b border-gray-800 last:border-0">
              <span :class="agentStatusClass(session.status)"
                class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium shrink-0">
                <span :class="agentStatusDot(session.status)" class="w-1.5 h-1.5 rounded-full" />
                {{ session.statusName }}
              </span>
              <div class="flex-1 min-w-0">
                <NuxtLink :to="`/projects/${id}/issues/${session.issueId}`"
                  class="text-sm text-gray-300 hover:text-brand-300 transition-colors truncate block">
                  #{{ session.issueNumber }} {{ session.issueTitle }}
                </NuxtLink>
                <p class="text-xs text-gray-500 truncate">{{ session.agentName }}</p>
              </div>
              <span class="text-xs text-gray-600 shrink-0">{{ relativeTime(session.startedAt) }}</span>
            </div>
          </div>
          <p v-else class="text-sm text-gray-600 py-4 text-center">No agent runs yet</p>
        </div>

        <!-- CI/CD Runs -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-semibold text-white flex items-center gap-2">
              <svg class="w-4 h-4 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              Recent CI/CD Runs
              <!-- WS connection indicator -->
              <span v-if="isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal">
                <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
                Live
              </span>
              <span v-else class="flex items-center gap-1 text-xs text-gray-600 font-normal">
                <span class="w-1.5 h-1.5 rounded-full bg-gray-600" />
                Offline
              </span>
            </h2>
            <NuxtLink :to="`/projects/${id}/runs`"
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
              View all →
            </NuxtLink>
          </div>
          <div v-if="recentCiCdRuns.length" class="space-y-2">
            <div v-for="run in recentCiCdRuns" :key="run.id"
              class="flex items-center gap-3 py-2 border-b border-gray-800 last:border-0 cursor-pointer"
              @click="navigateTo(`/projects/${id}/runs/cicd/${run.id}`)">
              <span :class="cicdStatusClass(run.status)"
                class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium shrink-0">
                <span :class="cicdStatusDot(run.status)" class="w-1.5 h-1.5 rounded-full" />
                {{ run.statusName }}
              </span>
              <div class="flex-1 min-w-0">
                <p class="text-sm text-gray-300 truncate">
                  {{ run.workflow || run.branch || 'Run' }}
                </p>
                <p class="text-xs text-gray-500 font-mono truncate">
                  {{ run.commitSha?.slice(0, 7) || '—' }}
                  <span v-if="run.branch"> · {{ run.branch }}</span>
                </p>
              </div>
              <span class="text-xs text-gray-600 shrink-0">{{ relativeTime(run.startedAt) }}</span>
            </div>
          </div>
          <p v-else class="text-sm text-gray-600 py-4 text-center">No CI/CD runs yet</p>
        </div>
      </div>

      <!-- Metric History Chart -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Issue &amp; Run History (last 24 h)</h2>
        <div v-if="metricSnapshots.length" class="overflow-x-auto">
          <svg :viewBox="`0 0 ${chartWidth} ${chartHeight}`" class="w-full" style="min-width:500px">
            <!-- Grid lines -->
            <line v-for="y in gridYValues" :key="y"
              :x1="chartPad" :y1="yScale(y)" :x2="chartWidth - chartPad" :y2="yScale(y)"
              stroke="#374151" stroke-width="1" />
            <!-- Y labels -->
            <text v-for="y in gridYValues" :key="`yl-${y}`"
              :x="chartPad - 6" :y="yScale(y) + 4"
              text-anchor="end" fill="#6b7280" font-size="10">{{ y }}</text>
            <!-- Open issues line -->
            <polyline :points="linePoints('openIssues')" fill="none" stroke="#f59e0b" stroke-width="2" stroke-linejoin="round" />
            <!-- In-progress issues line -->
            <polyline :points="linePoints('inProgressIssues')" fill="none" stroke="#6366f1" stroke-width="2" stroke-linejoin="round" />
            <!-- Done issues line -->
            <polyline :points="linePoints('doneIssues')" fill="none" stroke="#22c55e" stroke-width="2" stroke-linejoin="round" />
            <!-- Agent runs line -->
            <polyline :points="linePoints('totalAgentRuns')" fill="none" stroke="#e879f9" stroke-width="2" stroke-linejoin="round" stroke-dasharray="4 2" />
            <!-- CI/CD runs line -->
            <polyline :points="linePoints('totalCiCdRuns')" fill="none" stroke="#38bdf8" stroke-width="2" stroke-linejoin="round" stroke-dasharray="4 2" />
            <!-- X labels -->
            <text v-for="(snap, i) in metricSnapshots" :key="`xl-${i}`"
              :x="xPos(i)" :y="chartHeight - 4"
              text-anchor="middle" fill="#6b7280" font-size="9">{{ shortTime(snap.recordedAt) }}</text>
          </svg>
        </div>
        <div v-else class="py-8 text-center text-sm text-gray-500">No history yet — snapshots are saved hourly</div>
        <!-- Legend -->
        <div class="flex flex-wrap items-center gap-5 mt-3">
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-0.5 bg-amber-400 rounded-full inline-block"></span> Open
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-0.5 bg-indigo-400 rounded-full inline-block"></span> In Progress
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-0.5 bg-green-400 rounded-full inline-block"></span> Done
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-0.5 bg-fuchsia-400 rounded-full inline-block" style="background: repeating-linear-gradient(90deg,#e879f9 0,#e879f9 4px,transparent 4px,transparent 6px)"></span> Agent Runs
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-400">
            <span class="w-3 h-0.5 bg-sky-400 rounded-full inline-block" style="background: repeating-linear-gradient(90deg,#38bdf8 0,#38bdf8 4px,transparent 4px,transparent 6px)"></span> CI/CD Runs
          </span>
        </div>
      </div>
    </template>

    <!-- Not found / Error -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">{{ store.error || 'Project not found' }}</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ProjectMetricSnapshot, Milestone } from '~/types'
import { AgentSessionStatus, CiCdRunStatus } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useCiCdRunsStore } from '~/stores/cicdRuns'

const route = useRoute()
const id = route.params.id as string
const store = useProjectsStore()
const runsStore = useCiCdRunsStore()
const api = useApi()

const metricSnapshots = ref<ProjectMetricSnapshot[]>([])
const milestones = ref<Milestone[]>([])
const mcpCount = ref(0)

const { connection, isConnected, connect } = useSignalR('/hubs/project')

const openMilestones = computed(() => milestones.value.filter(m => m.status === 'open').slice(0, 3))
const recentAgentSessions = computed(() => runsStore.agentSessions.slice(0, 5))
const recentCiCdRuns = computed(() => runsStore.runs.slice(0, 5))

onMounted(async () => {
  await store.fetchProject(id)
  await Promise.all([
    runsStore.fetchRuns(id),
    runsStore.fetchAgentSessions(id),
    api.get<Milestone[]>(`/api/projects/${id}/milestones`)
      .then(data => { milestones.value = data })
      .catch((e) => { console.warn(`Failed to load milestones for project ${id}`, e) }),
    api.get<{ mcpServerId: string }[]>(`/api/projects/${id}/mcp-servers`)
      .then(data => { mcpCount.value = data.length })
      .catch((e) => { console.warn(`Failed to load MCP servers for project ${id}`, e) }),
    api.get<ProjectMetricSnapshot[]>(`/api/dashboard/projects/${id}/metric-history`)
      .then(data => { metricSnapshots.value = data })
      .catch((e) => { console.error(`Failed to load metric history for project ${id}`, e) }),
  ])

  // Connect to SignalR for live run updates
  await connect()
  if (connection.value) {
    await connection.value.invoke('JoinProject', id).catch(() => {})
    connection.value.on('RunsUpdated', async () => {
      await Promise.all([
        runsStore.fetchRuns(id),
        runsStore.fetchAgentSessions(id),
      ])
    })
  }
})

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function relativeTime(d: string) {
  const ms = Date.now() - new Date(d).getTime()
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s ago`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

// Agent session status helpers
function agentStatusClass(status: AgentSessionStatus) {
  switch (status) {
    case AgentSessionStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case AgentSessionStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case AgentSessionStatus.Failed: return 'bg-red-900/30 text-red-400'
    case AgentSessionStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function agentStatusDot(status: AgentSessionStatus) {
  switch (status) {
    case AgentSessionStatus.Succeeded: return 'bg-green-400'
    case AgentSessionStatus.Running: return 'bg-blue-400 animate-pulse'
    case AgentSessionStatus.Failed: return 'bg-red-400'
    case AgentSessionStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}

// CI/CD run status helpers
function cicdStatusClass(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-900/30 text-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-900/30 text-blue-400'
    case CiCdRunStatus.Failed: return 'bg-red-900/30 text-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-800 text-gray-400'
    default: return 'bg-yellow-900/30 text-yellow-400'
  }
}

function cicdStatusDot(status: CiCdRunStatus) {
  switch (status) {
    case CiCdRunStatus.Succeeded: return 'bg-green-400'
    case CiCdRunStatus.Running: return 'bg-blue-400 animate-pulse'
    case CiCdRunStatus.Failed: return 'bg-red-400'
    case CiCdRunStatus.Cancelled: return 'bg-gray-500'
    default: return 'bg-yellow-400'
  }
}

// Chart helpers
const chartWidth = 600
const chartHeight = 160
const chartPad = 36

const chartMaxY = computed(() => {
  const max = Math.max(
    ...metricSnapshots.value.flatMap(s => [
      s.openIssues, s.inProgressIssues, s.doneIssues, s.totalAgentRuns, s.totalCiCdRuns,
    ]),
    1,
  )
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
  const n = metricSnapshots.value.length
  const plotW = chartWidth - chartPad * 2
  return chartPad + (n > 1 ? (i / (n - 1)) * plotW : plotW / 2)
}

function linePoints(key: keyof ProjectMetricSnapshot) {
  return metricSnapshots.value.map((s, i) => `${xPos(i)},${yScale(s[key] as number)}`).join(' ')
}

function shortTime(d: string) {
  const dt = new Date(d)
  return `${dt.getHours().toString().padStart(2, '0')}:00`
}
</script>

