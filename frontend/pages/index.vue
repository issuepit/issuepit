<template>
  <div class="p-8">
    <!-- Header -->
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-white">Dashboard</h1>
      <p class="text-gray-400 mt-1">Welcome back — here's what's happening.</p>
    </div>

    <!-- Stats -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
      <StatCard label="Projects" :value="stats.projects" icon="projects" color="blue" to="/projects" />
      <StatCard label="Open Issues" :value="stats.openIssues" icon="issues" color="amber" to="/projects" />
      <StatCard label="In Progress" :value="stats.inProgress" icon="progress" color="indigo" to="/projects" />
      <StatCard label="Agents" :value="stats.agents" icon="agents" color="green" to="/agents" />
    </div>

    <!-- Recent Activity -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <!-- Recent Issues -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <div class="flex items-center justify-between mb-4">
          <h2 class="font-semibold text-white">Recent Issues</h2>
          <NuxtLink to="/projects" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
        </div>
        <div class="space-y-2">
          <NuxtLink v-for="issue in recentIssues" :key="issue.id"
            :to="`/projects/${issue.projectId}/issues/${issue.id}`"
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
          <p v-if="recentIssues.length === 0" class="text-sm text-gray-500 py-4 text-center">No recent issues</p>
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
          <p v-if="projectsStore.projects.length === 0" class="text-sm text-gray-500 py-4 text-center">No projects yet</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useIssuesStore } from '~/stores/issues'
import { useAgentsStore } from '~/stores/agents'

const projectsStore = useProjectsStore()
const issuesStore = useIssuesStore()
const agentsStore = useAgentsStore()

onMounted(async () => {
  await Promise.allSettled([
    projectsStore.fetchProjects(),
    agentsStore.fetchAgents()
  ])
})

const stats = computed(() => ({
  projects: projectsStore.projects.length,
  openIssues: issuesStore.issues.filter(i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled).length,
  inProgress: issuesStore.issues.filter(i => i.status === IssueStatus.InProgress).length,
  agents: agentsStore.agents.length
}))

const recentIssues = computed(() =>
  issuesStore.issues.slice(0, 5).map(i => ({
    ...i,
    projectName: projectsStore.projects.find(p => p.id === i.projectId)?.name ?? ''
  }))
)

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
