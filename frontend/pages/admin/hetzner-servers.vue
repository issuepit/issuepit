<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Admin', to: '/admin/tenants', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4' },
          { label: 'Hetzner Servers', to: '/admin/hetzner-servers', icon: serverIcon },
        ]" />
        <p class="text-gray-400 mt-1 text-sm">View and manage Hetzner Cloud CI/CD servers provisioned by IssuePit.</p>
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

    <!-- Summary cards -->
    <div v-if="summary" class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-8">
      <div
        v-for="(label, key) in statusLabels"
        :key="key"
        class="rounded-xl border border-gray-800 bg-gray-900/40 p-4"
      >
        <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">{{ label }}</div>
        <div class="text-2xl font-bold" :class="statusColor(key)">
          {{ summary.byStatus[key] ?? 0 }}
        </div>
      </div>
      <div class="rounded-xl border border-gray-800 bg-gray-900/40 p-4">
        <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">Active Runs</div>
        <div class="text-2xl font-bold text-brand-400">{{ summary.totalActiveRuns }}</div>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading && !servers.length" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Empty state -->
    <div v-else-if="!servers.length && !loading" class="rounded-xl border border-dashed border-gray-700 p-12 text-center">
      <svg class="w-10 h-10 text-gray-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" :d="serverIcon" />
      </svg>
      <p class="text-gray-500 text-sm">No Hetzner servers have been provisioned yet.</p>
      <p class="text-gray-600 text-xs mt-1">Set <code class="font-mono bg-gray-800 px-1 rounded">CiCd__Runtime=Hetzner</code> to enable the Hetzner CI/CD runtime.</p>
    </div>

    <!-- Server table -->
    <div v-else-if="servers.length" class="rounded-xl border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Type / Location</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Runs</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">CPU / RAM</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Setup</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr
            v-for="server in servers"
            :key="server.id"
            class="hover:bg-gray-900/50 transition-colors"
          >
            <!-- Name / IP -->
            <td class="px-4 py-3">
              <div class="text-white font-medium">{{ server.name }}</div>
              <div class="text-gray-500 text-xs font-mono mt-0.5">
                {{ server.ipv6Address || server.ipv4Address || '—' }}
              </div>
            </td>

            <!-- Type / Location -->
            <td class="px-4 py-3">
              <div class="text-gray-200">{{ server.serverType }}</div>
              <div class="text-gray-500 text-xs">{{ server.location }}</div>
            </td>

            <!-- Status badge -->
            <td class="px-4 py-3">
              <span
                class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium"
                :class="statusBadgeClass(server.status)"
              >
                <span class="w-1.5 h-1.5 rounded-full" :class="statusDotClass(server.status)" />
                {{ server.statusName }}
              </span>
              <div v-if="server.errorMessage" class="text-red-400 text-xs mt-0.5 max-w-[200px] truncate" :title="server.errorMessage">
                {{ server.errorMessage }}
              </div>
            </td>

            <!-- Run counts -->
            <td class="px-4 py-3">
              <div class="text-white">{{ server.activeRunCount }} active</div>
              <div class="text-gray-500 text-xs">{{ server.totalRunCount }} total</div>
            </td>

            <!-- CPU / RAM -->
            <td class="px-4 py-3">
              <template v-if="server.cpuLoadPercent !== null && server.cpuLoadPercent !== undefined">
                <div class="flex items-center gap-2">
                  <div class="w-16 h-1.5 rounded-full bg-gray-800">
                    <div
                      class="h-full rounded-full transition-all"
                      :class="loadBarColor(server.cpuLoadPercent)"
                      :style="{ width: `${Math.min(100, server.cpuLoadPercent)}%` }"
                    />
                  </div>
                  <span class="text-gray-300 text-xs">{{ server.cpuLoadPercent.toFixed(0) }}%</span>
                </div>
                <div v-if="server.ramUsedBytes && server.ramTotalBytes" class="text-gray-500 text-xs mt-0.5">
                  RAM: {{ formatBytes(server.ramUsedBytes) }} / {{ formatBytes(server.ramTotalBytes) }}
                </div>
              </template>
              <span v-else class="text-gray-600 text-xs">—</span>
            </td>

            <!-- Setup time -->
            <td class="px-4 py-3">
              <span v-if="server.setupTimeSeconds !== null && server.setupTimeSeconds !== undefined" class="text-gray-300 text-xs">
                {{ server.setupTimeSeconds }}s
              </span>
              <span v-else class="text-gray-600 text-xs">—</span>
            </td>

            <!-- Created -->
            <td class="px-4 py-3 text-gray-400 text-xs">
              <DateDisplay :date="server.createdAt" mode="relative" />
            </td>

            <!-- Actions -->
            <td class="px-4 py-3">
              <div class="flex items-center gap-1">
                <button
                  v-if="canReboot(server)"
                  class="p-1.5 text-gray-400 hover:text-yellow-400 hover:bg-yellow-900/20 rounded transition-colors"
                  title="Reboot"
                  @click="performAction(server, 'reboot')"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </button>
                <button
                  v-if="canStop(server)"
                  class="p-1.5 text-gray-400 hover:text-orange-400 hover:bg-orange-900/20 rounded transition-colors"
                  title="Stop"
                  @click="performAction(server, 'stop')"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z" />
                  </svg>
                </button>
                <button
                  class="p-1.5 text-gray-400 hover:text-red-400 hover:bg-red-900/20 rounded transition-colors"
                  title="Delete server record"
                  @click="confirmDelete(server)"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Delete confirmation modal -->
    <div v-if="deleteTarget" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div class="bg-gray-900 border border-gray-700 rounded-xl p-6 w-full max-w-md shadow-2xl">
        <h3 class="text-lg font-semibold text-white mb-2">Delete Server Record</h3>
        <p class="text-gray-400 text-sm mb-1">
          Remove <strong class="text-white">{{ deleteTarget.name }}</strong> from the dashboard?
        </p>
        <p class="text-gray-500 text-xs mb-6">
          This only removes the database record. The server in Hetzner Cloud will not be deleted automatically
          unless it is in <em>Draining</em> status (handled by the reconciler).
        </p>
        <div class="flex justify-end gap-3">
          <button class="px-4 py-2 text-sm text-gray-300 hover:text-white border border-gray-700 rounded-lg transition-colors" @click="deleteTarget = null">
            Cancel
          </button>
          <button class="px-4 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-500 rounded-lg transition-colors" @click="deleteRecord">
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface HetznerServer {
  id: string
  hetznerServerId: number
  name: string
  ipv4Address: string | null
  ipv6Address: string | null
  serverType: string
  location: string
  status: number
  statusName: string
  activeRunCount: number
  totalRunCount: number
  cpuLoadPercent: number | null
  ramUsedBytes: number | null
  ramTotalBytes: number | null
  createdAt: string
  lastIdleAt: string | null
  setupTimeSeconds: number | null
  errorMessage: string | null
  orgId: string | null
}

interface HetznerSummary {
  byStatus: Record<string, number>
  totalActiveRuns: number
}

const serverIcon = 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01'

// Matches the backend HetznerServerStatus enum values.
const STATUS_PROVISIONING = 0
const STATUS_INITIALIZING = 1
const STATUS_IDLE = 2
const STATUS_RUNNING = 3
const STATUS_DRAINING = 4
const STATUS_DELETED = 5
const STATUS_ERROR = 6

const statusLabels: Record<string, string> = {
  Running: 'Running',
  Idle: 'Idle',
  Initializing: 'Initializing',
  Draining: 'Draining',
  Error: 'Error',
  Deleted: 'Deleted',
}

const { get, post, del } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const servers = ref<HetznerServer[]>([])
const summary = ref<HetznerSummary | null>(null)
const deleteTarget = ref<HetznerServer | null>(null)

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const [s, sum] = await Promise.all([
      get<HetznerServer[]>('/api/admin/hetzner/servers'),
      get<HetznerSummary>('/api/admin/hetzner/servers/summary'),
    ])
    servers.value = s
    summary.value = sum
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function performAction(server: HetznerServer, action: string) {
  try {
    await post(`/api/admin/hetzner/servers/${server.id}/actions/${action}`, {})
    // Optimistic UI update
    if (action === 'stop') server.status = STATUS_DRAINING
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

function confirmDelete(server: HetznerServer) {
  deleteTarget.value = server
}

async function deleteRecord() {
  if (!deleteTarget.value) return
  try {
    await del(`/api/admin/hetzner/servers/${deleteTarget.value.id}`)
    servers.value = servers.value.filter(s => s.id !== deleteTarget.value!.id)
    deleteTarget.value = null
    await loadData()
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
    deleteTarget.value = null
  }
}

function canReboot(server: HetznerServer): boolean {
  return server.status <= STATUS_DRAINING && server.status !== STATUS_DELETED
}

function canStop(server: HetznerServer): boolean {
  return server.status <= STATUS_RUNNING && server.status !== STATUS_DELETED
}

function statusBadgeClass(status: number): string {
  switch (status) {
    case STATUS_PROVISIONING: return 'bg-blue-900/30 text-blue-400 border border-blue-700/40'
    case STATUS_INITIALIZING: return 'bg-yellow-900/30 text-yellow-400 border border-yellow-700/40'
    case STATUS_IDLE: return 'bg-green-900/30 text-green-400 border border-green-700/40'
    case STATUS_RUNNING: return 'bg-brand-900/30 text-brand-400 border border-brand-700/40'
    case STATUS_DRAINING: return 'bg-orange-900/30 text-orange-400 border border-orange-700/40'
    case STATUS_DELETED: return 'bg-gray-800 text-gray-500 border border-gray-700'
    case STATUS_ERROR: return 'bg-red-900/30 text-red-400 border border-red-700/40'
    default: return 'bg-gray-800 text-gray-400 border border-gray-700'
  }
}

function statusDotClass(status: number): string {
  switch (status) {
    case STATUS_PROVISIONING: return 'bg-blue-400'
    case STATUS_INITIALIZING: return 'bg-yellow-400 animate-pulse'
    case STATUS_IDLE: return 'bg-green-400'
    case STATUS_RUNNING: return 'bg-brand-400 animate-pulse'
    case STATUS_DRAINING: return 'bg-orange-400'
    case STATUS_DELETED: return 'bg-gray-600'
    case STATUS_ERROR: return 'bg-red-400'
    default: return 'bg-gray-500'
  }
}

function statusColor(key: string): string {
  switch (key) {
    case 'Running': return 'text-brand-400'
    case 'Idle': return 'text-green-400'
    case 'Initializing': return 'text-yellow-400'
    case 'Draining': return 'text-orange-400'
    case 'Error': return 'text-red-400'
    case 'Deleted': return 'text-gray-500'
    default: return 'text-white'
  }
}

function loadBarColor(percent: number): string {
  if (percent >= 90) return 'bg-red-500'
  if (percent >= 70) return 'bg-yellow-500'
  return 'bg-green-500'
}

function formatBytes(bytes: number): string {
  if (bytes >= 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`
  if (bytes >= 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(0)} MB`
  return `${(bytes / 1024).toFixed(0)} KB`
}

onMounted(loadData)

// Auto-refresh every 15 seconds while there are active/initializing servers.
let refreshInterval: ReturnType<typeof setInterval> | null = null
watch(servers, (s) => {
  const hasActive = s.some(sv => sv.status <= 3)
  if (hasActive && !refreshInterval)
    refreshInterval = setInterval(loadData, 15_000)
  else if (!hasActive && refreshInterval) {
    clearInterval(refreshInterval)
    refreshInterval = null
  }
})
onUnmounted(() => { if (refreshInterval) clearInterval(refreshInterval) })
</script>
