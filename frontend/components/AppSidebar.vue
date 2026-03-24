<template>
  <aside :class="['bg-gray-900 border-r border-gray-800 flex flex-col shrink-0 transition-all duration-200', sidebarCollapsed ? 'w-10' : 'w-60']">
    <!-- Logo / Header -->
    <div class="h-14 flex items-center border-b border-gray-800" :class="sidebarCollapsed ? 'justify-center px-0' : 'px-4'">
      <div v-if="!sidebarCollapsed" class="flex items-center gap-2 flex-1">
        <div class="w-7 h-7 rounded-md bg-brand-600 flex items-center justify-center">
          <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18m0 0h10a2 2 0 002-2V9M9 21H5a2 2 0 01-2-2V9m0 0h18" />
          </svg>
        </div>
        <span class="font-bold text-white text-sm tracking-wide">IssuePit</span>
      </div>
      <button
        class="text-gray-500 hover:text-gray-300 transition-colors"
        :class="sidebarCollapsed ? '' : 'ml-auto'"
        :title="sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="toggleSidebar"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="sidebarCollapsed ? 'M9 5l7 7-7 7' : 'M15 19l-7-7 7-7'" />
        </svg>
      </button>
    </div>

    <!-- Nav -->
    <nav v-if="!sidebarCollapsed" class="flex-1 p-3 space-y-0.5 overflow-y-auto">
      <SidebarNavLink to="/" icon="dashboard" label="Dashboard" />
      <SidebarNavLink :to="runsLink" icon="runs" label="Runs" />
      <SidebarNavLink to="/todos" icon="todos" label="Todos" />

      <!-- Issues section -->
      <SidebarSection label="Issues" icon="issues" :default-open="false">
        <SidebarNavLink to="/issues?filter=my" icon="my-issues" label="My Issues" :exact="false" />
        <SidebarNavLink to="/issues?filter=open" icon="open-issues" label="Open Issues" :exact="false" />
        <SidebarNavLink to="/issues?filter=unassigned" icon="unassigned" label="Unassigned" :exact="false" />
        <SidebarNavLink to="/issues?filter=waiting" icon="waiting" label="Waiting for Human" :exact="false" />
      </SidebarSection>

      <!-- Projects section (lazy loaded) -->
      <SidebarSection label="Projects" icon="projects" :default-open="true" :lazy="true" @open="loadProjects">
        <div v-if="projectsLoading" class="px-2 py-1">
          <div class="h-3 bg-gray-800 rounded animate-pulse w-3/4 mb-1.5"/>
          <div class="h-3 bg-gray-800 rounded animate-pulse w-1/2"/>
        </div>
        <template v-else>
          <SidebarNavLink
            v-for="project in projects"
            :key="project.id"
            :to="getProjectLink(project.id)"
            icon="project-item"
            :label="project.name"
            :color="project.color"
          />
          <div v-if="projects.length === 0" class="px-2 py-1 text-xs text-gray-600">No projects</div>
          <NuxtLink to="/projects" class="flex items-center gap-2 px-2 py-1 text-xs text-gray-500 hover:text-gray-400 rounded transition-colors">
            All projects →
          </NuxtLink>
        </template>
      </SidebarSection>

      <!-- Organizations section (lazy loaded) -->
      <SidebarSection label="Organizations" icon="orgs" :lazy="true" @open="loadOrgs">
        <div v-if="orgsLoading" class="px-2 py-1">
          <div class="h-3 bg-gray-800 rounded animate-pulse w-3/4 mb-1.5"/>
          <div class="h-3 bg-gray-800 rounded animate-pulse w-1/2"/>
        </div>
        <template v-else>
          <SidebarNavLink
            v-for="org in orgs"
            :key="org.id"
            :to="`/orgs/${org.id}`"
            icon="org-item"
            :label="org.name"
          />
          <div v-if="orgs.length === 0" class="px-2 py-1 text-xs text-gray-600">No organizations</div>
          <NuxtLink to="/orgs" class="flex items-center gap-2 px-2 py-1 text-xs text-gray-500 hover:text-gray-400 rounded transition-colors">
            All organizations →
          </NuxtLink>
        </template>
      </SidebarSection>

      <!-- Agents section -->
      <SidebarSection label="Agents" icon="agents" :default-open="false">
        <!-- Modes sub-section (lazy loaded) -->
        <SidebarSection label="Modes" icon="agent-item" :lazy="true" @open="loadAgents">
          <div v-if="agentsLoading" class="px-2 py-1">
            <div class="h-3 bg-gray-800 rounded animate-pulse w-3/4 mb-1.5"/>
            <div class="h-3 bg-gray-800 rounded animate-pulse w-1/2"/>
          </div>
          <template v-else>
            <SidebarNavLink
              v-for="agent in agents"
              :key="agent.id"
              :to="`/agents/${agent.id}`"
              icon="agent-item"
              :label="agent.name"
            />
            <div v-if="agents.length === 0" class="px-2 py-1 text-xs text-gray-600">No agents</div>
            <NuxtLink to="/agents" class="flex items-center gap-2 px-2 py-1 text-xs text-gray-500 hover:text-gray-400 rounded transition-colors">
              All agents →
            </NuxtLink>
          </template>
        </SidebarSection>
        <SidebarNavLink to="/skills" icon="skills" label="Skills" />
        <SidebarSection label="MCP" icon="mcp" :default-open="true">
          <SidebarNavLink to="/config/mcp-servers" icon="mcp" label="MCP Servers" />
          <SidebarNavLink to="/config/mcp-keys" icon="mcp" label="MCP Keys" />
          <SidebarNavLink to="/config/mcp-playground" icon="mcp-playground" label="Playground" />
        </SidebarSection>
        <SidebarNavLink to="/config/runtimes" icon="config" label="Runtimes" />
      </SidebarSection>

      <!-- System section -->
      <SidebarSection label="System" icon="config" :default-open="false">
        <SidebarNavLink to="/config/keys" icon="config" label="API Keys" />
        <SidebarNavLink to="/config/github-identities" icon="github" label="GitHub Identities" />
        <SidebarNavLink to="/config/telegram-bots" icon="config" label="Telegram Bots" />
        <SidebarNavLink to="/config/ci-cd" icon="runs" label="CI/CD" />
        <SidebarNavLink to="/config/git-server" icon="config" label="Git Server" />
        <SidebarNavLink to="/config/scheduled-tasks" icon="runs" label="Scheduled Tasks" />
        <SidebarNavLink to="/settings" icon="settings" label="Visuals" />
        <SidebarNavLink to="/about" icon="tenants" label="About" />
      </SidebarSection>

      <!-- Admin section -->
      <SidebarSection label="Admin" icon="tenants" :default-open="false">
        <SidebarNavLink to="/orgs" icon="orgs" label="Organizations" />
        <SidebarNavLink to="/admin/tenants" icon="tenants" label="Tenants" />
        <SidebarNavLink to="/admin/users" icon="users" label="Users" />
        <SidebarNavLink to="/admin/docker-images" icon="tenants" label="Docker Images" />
      </SidebarSection>
    </nav>

    <!-- Footer -->
    <div v-if="!sidebarCollapsed" class="p-3 border-t border-gray-800">
      <div v-if="auth.user" class="flex items-center gap-2 px-2 py-1.5 rounded-md">
        <NuxtLink to="/profile" class="flex items-center gap-2 flex-1 min-w-0 hover:opacity-80 transition-opacity">
          <div class="w-6 h-6 rounded-full bg-brand-600 flex items-center justify-center text-xs font-bold shrink-0">
            {{ initials }}
          </div>
          <span class="text-sm text-gray-300 truncate">{{ displayName }}</span>
        </NuxtLink>
        <button
          class="text-gray-500 hover:text-gray-300 transition-colors ml-1"
          title="Sign out"
          @click="auth.logout()">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
        </button>
      </div>
      <NuxtLink v-else to="/login" class="flex items-center gap-2 px-2 py-1.5 rounded-md hover:bg-gray-800 cursor-pointer">
        <div class="w-6 h-6 rounded-full bg-gray-700 flex items-center justify-center text-xs font-bold">?</div>
        <span class="text-sm text-gray-400">Sign in</span>
      </NuxtLink>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'
import { useProjectsStore } from '~/stores/projects'
import { useOrgsStore } from '~/stores/orgs'
import { useAgentsStore } from '~/stores/agents'
import type { Project, Organization, Agent } from '~/types'

const auth = useAuthStore()
const route = useRoute()

const displayName = computed(() => auth.user?.username ?? auth.user?.email?.split('@')[0] ?? 'User')
const initials = computed(() => displayName.value.slice(0, 2).padEnd(2, displayName.value[0] ?? 'U').toUpperCase())

// Smart "Runs" sidebar link:
// - If inside a project but not on the project runs page → go to project runs
// - If already on the project runs page → go to global runs
// - Otherwise → global runs
const runsLink = computed(() => {
  const match = route.path.match(/^\/projects\/([^/]+)(\/.*)?$/)
  if (match) {
    const projectId = match[1]
    const subPath = match[2] ?? ''
    if (subPath.startsWith('/runs')) return '/runs'
    return `/projects/${projectId}/runs`
  }
  return '/runs'
})

// Returns the sidebar link for a project, preserving the current sub-page when possible.
// e.g. /projects/OLD/kanban → /projects/NEW/kanban
// e.g. /projects/OLD/issues/8 → /projects/NEW/issues  (specific item pages fall back to the list)
// e.g. /projects/OLD/runs/cicd/UUID → /projects/NEW/runs?tab=cicd  (run detail pages fall back to the tabbed list)
// e.g. /projects/OLD/runs/agent-sessions/UUID → /projects/NEW/runs?tab=agent  (agent detail pages fall back to the tabbed list)
// e.g. /projects/OLD/runs/test-history → /projects/NEW/runs/test-history  (stable sub-views are preserved)
// If the project is already open (same project ID), always navigate to its dashboard.

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const NUMERIC_RE = /^\d+$/

// Sub-paths that have no index page of their own; map them to the correct tabbed URL.
const VIRTUAL_PATH_REDIRECTS: Record<string, string> = {
  'runs/cicd': 'runs?tab=cicd',
  'runs/agent-sessions': 'runs?tab=agent',
}

function isItemId(segment: string): boolean {
  return NUMERIC_RE.test(segment) || UUID_RE.test(segment)
}

function getProjectLink(projectId: string): string {
  const match = route.path.match(/^\/projects\/([^/]+)\/(.+)$/)
  if (!match) return `/projects/${projectId}`
  // If this project is already open, go to its dashboard
  if (match[1] === projectId) return `/projects/${projectId}`
  const subPath = match[2]
  const parts = subPath.split('/')
  // If the last segment looks like a specific item ID (a number or UUID), navigate one level up.
  // Some parent paths have no index route and must be redirected to their tabbed equivalent.
  if (parts.length > 1 && isItemId(parts[parts.length - 1])) {
    const parentPath = parts.slice(0, -1).join('/')
    const redirect = VIRTUAL_PATH_REDIRECTS[parentPath]
    if (redirect) return `/projects/${projectId}/${redirect}`
    return `/projects/${projectId}/${parentPath}`
  }
  return `/projects/${projectId}/${subPath}`
}

// Sidebar collapse state
const sidebarCollapsed = ref(
  import.meta.client ? localStorage.getItem('sidebar-collapsed') === 'true' : false
)

function toggleSidebar() {
  sidebarCollapsed.value = !sidebarCollapsed.value
  if (import.meta.client) {
    localStorage.setItem('sidebar-collapsed', String(sidebarCollapsed.value))
  }
}

// Lazy-loaded data
const projects = ref<Project[]>([])
const orgs = ref<Organization[]>([])
const agents = ref<Agent[]>([])
const projectsLoading = ref(false)
const orgsLoading = ref(false)
const agentsLoading = ref(false)
const projectsLoaded = ref(false)
const orgsLoaded = ref(false)
const agentsLoaded = ref(false)

const projectsStore = useProjectsStore()
const orgsStore = useOrgsStore()
const agentsStore = useAgentsStore()

async function loadProjects() {
  if (projectsLoaded.value) return
  projectsLoading.value = true
  try {
    await projectsStore.fetchProjects()
    projects.value = projectsStore.projects
    projectsLoaded.value = true
  } finally {
    projectsLoading.value = false
  }
}

async function loadOrgs() {
  if (orgsLoaded.value) return
  orgsLoading.value = true
  try {
    await orgsStore.fetchOrgs()
    orgs.value = orgsStore.orgs
    orgsLoaded.value = true
  } finally {
    orgsLoading.value = false
  }
}

async function loadAgents() {
  if (agentsLoaded.value) return
  agentsLoading.value = true
  try {
    await agentsStore.fetchAgents()
    agents.value = agentsStore.agents
    agentsLoaded.value = true
  } finally {
    agentsLoading.value = false
  }
}

// --- Icon paths ---
const iconPaths: Record<string, string> = {
  dashboard: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
  runs: 'M13 10V3L4 14h7v7l9-11h-7z',
  todos: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4',
  projects: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10',
  issues: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2',
  'my-issues': 'M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z',
  'open-issues': 'M5 13l4 4L19 7',
  'unassigned': 'M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z',
  'waiting': 'M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z',
  agents: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2',
  config: 'M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z',
  github: 'M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207',
  settings: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
  tenants: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4',
  orgs: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z',
  users: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z',
  mcp: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01',
  'mcp-playground': 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
  'project-item': 'M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z',
  'org-item': 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4',
  'agent-item': 'M13 10V3L4 14h7v7l9-11h-7z',
  skills: 'M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z',
}

// --- SidebarNavLink component ---
const SidebarNavLink = defineComponent({
  props: {
    to: { type: String, required: true },
    icon: { type: String, required: true },
    label: { type: String, required: true },
    color: { type: String, default: null },
    // When true, only highlight this link for an exact path match (no prefix matching)
    exact: { type: Boolean, default: false },
  },
  setup(props) {
    const route = useRoute()
    const isActive = computed(() => {
      if (props.to === '/') return route.path === '/'
      if (props.exact) return route.path === props.to
      // For routes with query params (e.g. /issues?filter=my), match both path and query
      const qIdx = props.to.indexOf('?')
      if (qIdx !== -1) {
        const path = props.to.slice(0, qIdx)
        const filterVal = new URLSearchParams(props.to.slice(qIdx + 1)).get('filter')
        return route.path === path && route.query.filter === filterVal
      }
      return route.path.startsWith(props.to)
    })

    return () => h(resolveComponent('NuxtLink'), {
      to: props.to,
      class: [
        'flex items-center gap-2.5 px-2 py-1.5 rounded-md text-sm transition-colors',
        isActive.value ? 'bg-gray-800 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
      ]
    }, () => [
      h('svg', {
        class: 'w-4 h-4 shrink-0',
        fill: 'none',
        stroke: props.color || 'currentColor',
        viewBox: '0 0 24 24'
      }, h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', 'stroke-width': '2', d: iconPaths[props.icon] ?? iconPaths.issues })),
      h('span', { class: 'truncate', style: props.color ? { color: props.color } : undefined }, props.label)
    ])
  }
})

// --- SidebarSection component ---
const SidebarSection = defineComponent({
  props: {
    label: { type: String, required: true },
    icon: { type: String, required: true },
    defaultOpen: { type: Boolean, default: false },
    lazy: { type: Boolean, default: false },
  },
  emits: ['open'],
  setup(props, { slots, emit }) {
    const storageKey = `sidebar-section-${props.label.toLowerCase().replace(/\s+/g, '-')}`
    const stored = import.meta.client
      ? (sessionStorage.getItem(storageKey) ?? localStorage.getItem(storageKey))
      : null
    const isOpen = ref(stored !== null ? stored === 'true' : props.defaultOpen)
    const hasLoaded = ref(false)

    function triggerLoad() {
      if (props.lazy && !hasLoaded.value) {
        hasLoaded.value = true
        emit('open')
      }
    }

    onMounted(() => {
      if (isOpen.value) triggerLoad()
    })

    function toggle() {
      isOpen.value = !isOpen.value
      if (import.meta.client) {
        sessionStorage.setItem(storageKey, String(isOpen.value))
        localStorage.setItem(storageKey, String(isOpen.value))
      }
      if (isOpen.value) triggerLoad()
    }

    return () => h('div', { class: 'pt-1' }, [
      h('button', {
        class: 'w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-xs font-medium text-gray-500 hover:text-gray-300 uppercase tracking-wider transition-colors group',
        onClick: toggle
      }, [
        h('svg', { class: 'w-3.5 h-3.5 shrink-0 transition-transform', style: { transform: isOpen.value ? 'rotate(90deg)' : 'rotate(0deg)' }, fill: 'none', stroke: 'currentColor', viewBox: '0 0 24 24' },
          h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', 'stroke-width': '2', d: 'M9 5l7 7-7 7' })
        ),
        h('span', props.label)
      ]),
      isOpen.value
        ? h('div', { class: 'mt-0.5 space-y-0.5 pl-2' }, slots.default?.())
        : null
    ])
  }
})
</script>
