<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-8">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Admin', to: '/admin/tenants', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4' },
          { label: 'Hetzner Servers', to: '/admin/hetzner-servers', icon: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01' },
        ]" />
        <p class="text-gray-400 mt-1 text-sm">Hetzner Cloud servers provisioned by IssuePit for CI/CD workloads.</p>
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
    <div v-if="loading && !servers.length" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <!-- Empty -->
    <div v-else-if="!servers.length" class="text-center py-20 text-gray-500">
      <svg class="w-12 h-12 mx-auto mb-4 text-gray-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2" />
      </svg>
      <p class="text-sm">No Hetzner servers found.</p>
      <p class="text-xs mt-1 text-gray-600">Servers appear here when a CI/CD run uses the Hetzner runtime.</p>
    </div>

    <!-- Servers table -->
    <div v-else class="rounded-xl border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Org</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Type / Location</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">IPv6</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Jobs</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">CPU / RAM</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Uptime</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="s in servers" :key="s.id" class="hover:bg-gray-900/50 transition-colors">
            <!-- Name + Hetzner ID -->
            <td class="px-4 py-3">
              <div class="font-medium text-white text-xs">{{ s.name }}</div>
              <div class="text-gray-500 text-xs font-mono">id={{ s.hetznerServerId }}</div>
            </td>
            <!-- Org -->
            <td class="px-4 py-3 text-gray-300 text-xs">{{ s.orgName }}</td>
            <!-- Type / Location -->
            <td class="px-4 py-3">
              <div class="text-gray-300 text-xs">{{ s.serverType }}</div>
              <div class="text-gray-500 text-xs">{{ s.location }}</div>
            </td>
            <!-- Status badge -->
            <td class="px-4 py-3">
              <span :class="statusBadgeClass(s.status)" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium">
                <span v-if="s.status === 'Running'" class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
                {{ statusLabel(s.status) }}
              </span>
              <div v-if="s.lastError" class="text-red-400 text-xs mt-1 max-w-[200px] truncate" :title="s.lastError">
                {{ s.lastError }}
              </div>
            </td>
            <!-- IPv6 -->
            <td class="px-4 py-3 text-gray-400 font-mono text-xs">
              <span v-if="s.ipv6Address">{{ s.ipv6Address }}</span>
              <span v-else class="text-gray-600">—</span>
            </td>
            <!-- Job counts -->
            <td class="px-4 py-3">
              <div class="text-xs text-gray-300">
                <span v-if="s.activeJobCount > 0" class="text-green-400 font-medium">{{ s.activeJobCount }} active</span>
                <span v-else class="text-gray-500">idle</span>
              </div>
              <div class="text-xs text-gray-500">{{ s.totalJobCount }} total</div>
            </td>
            <!-- CPU / RAM -->
            <td class="px-4 py-3">
              <template v-if="s.cpuLoadPercent !== null || s.ramUsedMb !== null">
                <div class="text-xs text-gray-300">
                  <span v-if="s.cpuLoadPercent !== null">CPU {{ s.cpuLoadPercent?.toFixed(1) }}%</span>
                </div>
                <div class="text-xs text-gray-500">
                  <span v-if="s.ramUsedMb !== null && s.ramTotalMb !== null">
                    RAM {{ s.ramUsedMb }} / {{ s.ramTotalMb }} MB
                  </span>
                </div>
              </template>
              <span v-else class="text-gray-600 text-xs">—</span>
            </td>
            <!-- Created -->
            <td class="px-4 py-3 text-gray-400 text-xs">
              <DateDisplay :date="s.createdAt" mode="relative" />
              <div v-if="s.setupDurationSeconds" class="text-gray-500 text-xs">
                setup {{ s.setupDurationSeconds }}s
              </div>
            </td>
            <!-- Uptime -->
            <td class="px-4 py-3 text-gray-400 text-xs">
              <template v-if="s.deletedAt">
                <DateDisplay :date="s.deletedAt" mode="relative" />
                <span class="text-gray-600"> (deleted)</span>
              </template>
              <template v-else-if="s.readyAt">
                {{ uptimeLabel(s.readyAt) }}
              </template>
              <span v-else class="text-gray-600">—</span>
            </td>
            <!-- Actions -->
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-2">
                <button
                  v-if="!['Deleted', 'Deleting'].includes(s.status)"
                  class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                  :disabled="actionLoading === s.id"
                  @click="markDeleted(s)"
                >
                  Mark Deleted
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Summary stats -->
    <div v-if="servers.length" class="mt-6 grid grid-cols-2 sm:grid-cols-4 gap-4">
      <div class="bg-gray-900 border border-gray-800 rounded-lg p-4">
        <div class="text-2xl font-bold text-white">{{ servers.length }}</div>
        <div class="text-xs text-gray-500 mt-1">Total servers</div>
      </div>
      <div class="bg-gray-900 border border-gray-800 rounded-lg p-4">
        <div class="text-2xl font-bold text-green-400">{{ activeCount }}</div>
        <div class="text-xs text-gray-500 mt-1">Active / running</div>
      </div>
      <div class="bg-gray-900 border border-gray-800 rounded-lg p-4">
        <div class="text-2xl font-bold text-white">{{ totalJobs }}</div>
        <div class="text-xs text-gray-500 mt-1">Total jobs run</div>
      </div>
      <div class="bg-gray-900 border border-gray-800 rounded-lg p-4">
        <div class="text-2xl font-bold text-white">{{ avgSetupSeconds > 0 ? avgSetupSeconds + 's' : '—' }}</div>
        <div class="text-xs text-gray-500 mt-1">Avg setup time</div>
      </div>
    </div>

    <!-- Confirm delete modal -->
    <ConfirmModal
      v-if="confirmDeleteServer"
      title="Mark server as deleted?"
      :message="`Mark '${confirmDeleteServer.name}' (id=${confirmDeleteServer.hetznerServerId}) as deleted in IssuePit's database? The actual Hetzner Cloud server must be removed separately via the Hetzner console or API. Use this action only if the server has already been deleted from Hetzner or to reconcile stale records. This action cannot be undone.`"
      confirm-label="Mark Deleted"
      confirm-class="bg-red-600 hover:bg-red-700"
      @confirm="doMarkDeleted"
      @cancel="confirmDeleteServer = null"
    />
  </div>
</template>

<script setup lang="ts">
interface HetznerServerDto {
  id: string
  hetznerServerId: number
  orgId: string
  orgName: string
  name: string
  serverType: string
  location: string
  ipv6Address: string | null
  ipv4Address: string | null
  status: string
  createdAt: string
  readyAt: string | null
  lastJobEndedAt: string | null
  deletedAt: string | null
  activeJobCount: number
  totalJobCount: number
  cpuLoadPercent: number | null
  ramUsedMb: number | null
  ramTotalMb: number | null
  setupDurationSeconds: number | null
  lastError: string | null
}

const { $api } = useNuxtApp()

const servers = ref<HetznerServerDto[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const actionLoading = ref<string | null>(null)
const confirmDeleteServer = ref<HetznerServerDto | null>(null)

const activeCount = computed(() =>
  servers.value.filter(s => ['Running', 'Idle', 'Initializing', 'Provisioning', 'SpinningDown'].includes(s.status)).length
)
const totalJobs = computed(() => servers.value.reduce((sum, s) => sum + s.totalJobCount, 0))
const avgSetupSeconds = computed(() => {
  const withSetup = servers.value.filter(s => s.setupDurationSeconds)
  if (!withSetup.length) return 0
  return Math.round(withSetup.reduce((sum, s) => sum + (s.setupDurationSeconds ?? 0), 0) / withSetup.length)
})

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const data = await $api<HetznerServerDto[]>('/api/admin/hetzner-servers')
    servers.value = data ?? []
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load servers'
  }
  finally {
    loading.value = false
  }
}

function markDeleted(server: HetznerServerDto) {
  confirmDeleteServer.value = server
}

async function doMarkDeleted() {
  if (!confirmDeleteServer.value) return
  const server = confirmDeleteServer.value
  confirmDeleteServer.value = null
  actionLoading.value = server.id
  try {
    await $api(`/api/admin/hetzner-servers/${server.id}/mark-deleted`, { method: 'POST' })
    await loadData()
  }
  catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Action failed'
  }
  finally {
    actionLoading.value = null
  }
}

function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    Provisioning: 'Provisioning',
    Initializing: 'Initializing',
    Idle: 'Idle',
    Running: 'Running',
    SpinningDown: 'Spinning Down',
    Deleting: 'Deleting',
    Deleted: 'Deleted',
    Failed: 'Failed',
  }
  return labels[status] ?? status
}

function statusBadgeClass(status: string): string {
  const classes: Record<string, string> = {
    Provisioning: 'bg-yellow-900/30 text-yellow-400',
    Initializing: 'bg-blue-900/30 text-blue-400',
    Idle: 'bg-gray-800 text-gray-400',
    Running: 'bg-green-900/30 text-green-400',
    SpinningDown: 'bg-orange-900/30 text-orange-400',
    Deleting: 'bg-red-900/20 text-red-400',
    Deleted: 'bg-gray-800 text-gray-500',
    Failed: 'bg-red-900/30 text-red-400',
  }
  return classes[status] ?? 'bg-gray-800 text-gray-400'
}

function uptimeLabel(readyAt: string): string {
  const seconds = Math.floor((Date.now() - new Date(readyAt).getTime()) / 1000)
  if (seconds < 60) return `${seconds}s`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`
  return `${Math.floor(seconds / 3600)}h ${Math.floor((seconds % 3600) / 60)}m`
}

onMounted(() => loadData())
</script>
