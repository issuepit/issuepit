<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Agent Auth Backups</h2>
        <p class="text-sm text-gray-400 mt-0.5">
          Manage opencode <code class="text-xs bg-gray-800 px-1 rounded">auth.json</code> snapshots
          captured from manual terminal sessions.
          Enable "Restore on agent runs" to inject saved credentials into autonomous agent containers automatically.
        </p>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && !store.auths.length" class="text-gray-500 text-sm py-8 text-center">Loading…</div>

    <!-- Error -->
    <div v-else-if="store.error" class="rounded-lg bg-red-900/30 border border-red-800 px-4 py-3 text-sm text-red-400 mb-4">
      {{ store.error }}
    </div>

    <!-- Empty -->
    <div v-else-if="!store.auths.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No auth backups yet.</p>
      <p class="text-gray-600 text-xs mt-2">
        To create a backup, open a manual-mode agent session terminal and authenticate with
        <code class="text-xs bg-gray-800 px-1 rounded">opencode</code>, then click "Backup auth.json" on the session page.
      </p>
    </div>

    <!-- Auth list -->
    <div v-else class="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900/50">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Label</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Captured</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Last Used</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium text-xs">Restore on Runs</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr
            v-for="auth in store.auths"
            :key="auth.id"
            class="hover:bg-gray-800/40 transition-colors">
            <td class="px-4 py-3 text-gray-200 text-sm font-medium">{{ auth.label }}</td>
            <td class="px-4 py-3 text-gray-400 text-xs">
              <DateDisplay :date="auth.capturedAt" mode="auto" />
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs">
              <DateDisplay v-if="auth.lastUsedAt" :date="auth.lastUsedAt" mode="auto" />
              <span v-else class="text-gray-600">Never</span>
            </td>
            <td class="px-4 py-3">
              <button
                :class="[
                  'flex items-center gap-1.5 text-xs px-2 py-1 rounded-md border transition-colors',
                  auth.restoreOnAgentRuns
                    ? 'border-green-700 text-green-400 bg-green-900/20'
                    : 'border-gray-700 text-gray-500 hover:border-gray-600'
                ]"
                :disabled="togglingId === auth.id"
                @click="toggleRestore(auth)">
                <span class="w-2 h-2 rounded-full" :class="auth.restoreOnAgentRuns ? 'bg-green-400' : 'bg-gray-600'" />
                {{ auth.restoreOnAgentRuns ? 'Enabled' : 'Disabled' }}
              </button>
            </td>
            <td class="px-4 py-3 text-right">
              <div class="flex items-center justify-end gap-2">
                <button
                  class="text-xs text-gray-400 hover:text-white transition-colors"
                  @click="openDetail(auth)">
                  View
                </button>
                <button
                  class="text-xs text-red-500 hover:text-red-400 transition-colors"
                  @click="confirmDelete(auth)">
                  Delete
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Detail modal -->
    <Teleport to="body">
      <div
        v-if="selectedAuth"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
        @mousedown.self="selectedAuth = null">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-2xl max-h-[80vh] flex flex-col">
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-base font-semibold text-white">{{ selectedAuth.label }}</h3>
            <button class="text-gray-500 hover:text-white" @click="selectedAuth = null">✕</button>
          </div>
          <div class="flex items-center gap-3 mb-4">
            <span class="text-xs text-gray-400">Captured: <DateDisplay :date="selectedAuth.capturedAt" mode="absolute" resolution="datetime" /></span>
            <span v-if="selectedAuth.agentSessionId" class="text-xs text-gray-500">Session: {{ selectedAuth.agentSessionId.slice(0, 8) }}…</span>
          </div>
          <div class="flex-1 overflow-auto">
            <pre class="bg-gray-950 rounded-lg p-4 text-xs text-green-300 font-mono overflow-auto whitespace-pre-wrap break-all max-h-96">{{ formatJson(selectedAuth.authJsonContent) }}</pre>
          </div>
          <div class="mt-4 flex justify-end">
            <button
              class="px-4 py-2 text-sm text-gray-300 hover:text-white border border-gray-700 hover:border-gray-600 rounded-lg transition-colors"
              @click="selectedAuth = null">
              Close
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Confirm delete modal -->
    <Teleport to="body">
      <div
        v-if="deleteTarget"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
        @mousedown.self="deleteTarget = null">
        <div class="bg-gray-900 border border-gray-700 rounded-xl shadow-xl p-6 w-full max-w-md">
          <h3 class="text-base font-semibold text-white mb-2">Delete Auth Backup</h3>
          <p class="text-sm text-gray-400 mb-4">
            Are you sure you want to delete <span class="text-white font-medium">"{{ deleteTarget.label }}"</span>?
            This action cannot be undone.
          </p>
          <div class="flex justify-end gap-3">
            <button
              class="px-4 py-2 text-sm text-gray-400 hover:text-white border border-gray-700 hover:border-gray-600 rounded-lg transition-colors"
              @click="deleteTarget = null">
              Cancel
            </button>
            <button
              class="px-4 py-2 text-sm font-medium bg-red-700 hover:bg-red-600 text-white rounded-lg transition-colors"
              :disabled="deleting"
              @click="doDelete">
              {{ deleting ? 'Deleting…' : 'Delete' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import type { AgentAuth, AgentAuthDetail } from '~/types'
import { useAgentAuthStore } from '~/stores/agentAuth'

const store = useAgentAuthStore()

const selectedAuth = ref<AgentAuthDetail | null>(null)
const deleteTarget = ref<AgentAuth | null>(null)
const togglingId = ref<string | null>(null)
const deleting = ref(false)

onMounted(() => {
  store.fetchAuths()
})

async function openDetail(auth: AgentAuth) {
  await store.fetchAuth(auth.id)
  selectedAuth.value = store.currentAuth
}

async function toggleRestore(auth: AgentAuth) {
  togglingId.value = auth.id
  try {
    await store.updateAuth(auth.id, { restoreOnAgentRuns: !auth.restoreOnAgentRuns })
  }
  finally {
    togglingId.value = null
  }
}

function confirmDelete(auth: AgentAuth) {
  deleteTarget.value = auth
}

async function doDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  try {
    await store.deleteAuth(deleteTarget.value.id)
    deleteTarget.value = null
  }
  finally {
    deleting.value = false
  }
}

function formatJson(content: string): string {
  try {
    return JSON.stringify(JSON.parse(content), null, 2)
  }
  catch {
    return content
  }
}
</script>
