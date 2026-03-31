<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Telegram Setup</h2>
        <p class="text-sm text-gray-400 mt-0.5">Pair Telegram chats with IssuePit to send commands and receive notifications.</p>
      </div>
      <button
        class="px-3 py-1.5 text-gray-400 hover:text-gray-200 text-sm border border-gray-700 rounded-lg hover:bg-gray-800 transition-colors"
        @click="refresh"
      >
        Refresh
      </button>
    </div>

    <!-- How it works -->
    <div class="mb-8 rounded-lg border border-gray-800 bg-gray-900/50 p-5">
      <h3 class="text-sm font-medium text-white mb-3">How to pair a Telegram chat</h3>
      <ol class="space-y-2 text-sm text-gray-400 list-decimal list-inside">
        <li>Open Telegram and send <code class="bg-gray-800 px-1.5 py-0.5 rounded text-brand-300 text-xs">/start</code> or <code class="bg-gray-800 px-1.5 py-0.5 rounded text-brand-300 text-xs">/pair</code> to your bot.</li>
        <li>The bot replies with a <span class="text-white font-medium">6-character pairing code</span> (valid for 15 minutes).</li>
        <li>Enter the code below, choose a scope (org or project), and click <span class="text-white font-medium">Link Chat</span>.</li>
      </ol>
      <p class="mt-3 text-xs text-gray-500">
        No bot configured yet? Set one up first under
        <NuxtLink to="/config/telegram-bots" class="text-brand-400 hover:text-brand-300 underline">Configuration → Telegram Bots</NuxtLink>.
      </p>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
      <!-- Pending pairing codes (from Telegram) -->
      <div class="rounded-lg border border-gray-800 p-5">
        <h3 class="text-sm font-medium text-white mb-3">Pending Codes from Telegram</h3>
        <div v-if="configStore.telegramPairingsLoading" class="text-gray-500 text-sm">Loading…</div>
        <div v-else-if="!configStore.telegramPairings.length" class="text-gray-500 text-sm">
          No pending codes. Have a Telegram user send <code class="bg-gray-800 px-1 rounded text-xs text-brand-300">/start</code> to your bot.
        </div>
        <ul v-else class="space-y-2">
          <li
            v-for="pairing in configStore.telegramPairings"
            :key="pairing.id"
            class="flex items-center justify-between bg-gray-800/50 rounded-lg px-3 py-2"
          >
            <div class="min-w-0">
              <span class="font-mono text-base font-bold text-white tracking-widest">{{ pairing.code }}</span>
              <div class="text-xs text-gray-500 mt-0.5">
                <span v-if="pairing.telegramUsername">@{{ pairing.telegramUsername }} · </span>
                Expires <DateDisplay :date="pairing.expiresAt" mode="relative" />
              </div>
            </div>
            <button
              class="ml-3 px-3 py-1 text-xs bg-brand-600 hover:bg-brand-500 text-white rounded-lg transition-colors whitespace-nowrap"
              @click="prefillCode(pairing.code)"
            >
              Use Code
            </button>
          </li>
        </ul>
      </div>

      <!-- Link a Chat form -->
      <div class="rounded-lg border border-gray-800 p-5">
        <h3 class="text-sm font-medium text-white mb-3">Link a Chat</h3>
        <form class="space-y-3" @submit.prevent="handleRedeem">
          <div>
            <label class="block text-xs text-gray-400 mb-1">Pairing Code</label>
            <input
              id="pairing-code"
              v-model="redeemForm.code"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500 font-mono tracking-widest uppercase placeholder-gray-600"
              placeholder="XXXXXX"
              maxlength="6"
              required
            >
          </div>
          <div>
            <label class="block text-xs text-gray-400 mb-1">Bot Token</label>
            <input
              id="pairing-bot-token"
              v-model="redeemForm.botToken"
              type="password"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
              placeholder="1234567890:AAABBBCCC..."
              required
            >
          </div>
          <div>
            <label class="block text-xs text-gray-400 mb-1">Scope</label>
            <select
              v-model="redeemForm.scopeType"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
            >
              <option value="user">Personal (me)</option>
              <option value="org">Organization</option>
              <option value="project">Project</option>
            </select>
          </div>
          <div v-if="redeemForm.scopeType === 'org'">
            <label class="block text-xs text-gray-400 mb-1">Organization</label>
            <select
              id="pairing-org-id"
              v-model="redeemForm.orgId"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
              required
            >
              <option value="">Select an organization…</option>
              <option v-for="org in orgsStore.orgs" :key="org.id" :value="org.id">{{ org.name }}</option>
            </select>
          </div>
          <div v-if="redeemForm.scopeType === 'project'">
            <label class="block text-xs text-gray-400 mb-1">Project</label>
            <select
              id="pairing-project-id"
              v-model="redeemForm.projectId"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
              required
            >
              <option value="">Select a project…</option>
              <option v-for="project in projectsStore.projects" :key="project.id" :value="project.id">{{ project.name }}</option>
            </select>
          </div>
          <div v-if="redeemError" role="alert" aria-live="polite" class="text-xs text-red-400 bg-red-900/20 border border-red-800 rounded-lg px-3 py-2">
            {{ redeemError }}
          </div>
          <button
            type="submit"
            :disabled="redeeming"
            class="w-full px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
          >
            {{ redeeming ? 'Linking…' : 'Link Chat' }}
          </button>
        </form>
      </div>
    </div>

    <!-- Paired Chats -->
    <div>
      <div class="flex items-center justify-between mb-3">
        <h3 class="text-sm font-medium text-white">Paired Chats</h3>
      </div>
      <div v-if="configStore.telegramChatsLoading" class="text-gray-500 text-sm">Loading…</div>
      <div v-else-if="!configStore.telegramChats.length" class="rounded-lg border border-dashed border-gray-700 p-8 text-center">
        <p class="text-gray-500 text-sm">No chats paired yet. Follow the steps above to link your first Telegram chat.</p>
      </div>
      <div v-else class="rounded-lg border border-gray-800 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-gray-900">
            <tr>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Chat</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Scope</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Events</th>
              <th class="text-left px-4 py-3 text-gray-400 font-medium">Paired</th>
              <th class="px-4 py-3" />
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-800">
            <tr v-for="chat in configStore.telegramChats" :key="chat.id" class="hover:bg-gray-900/50 transition-colors">
              <td class="px-4 py-3">
                <div class="text-white font-mono text-xs">{{ chat.telegramChatId }}</div>
                <div v-if="chat.telegramUsername" class="text-gray-500 text-xs">@{{ chat.telegramUsername }}</div>
              </td>
              <td class="px-4 py-3 text-gray-400 text-xs">{{ chatScopeLabel(chat) }}</td>
              <td class="px-4 py-3">
                <div class="flex flex-wrap gap-1">
                  <span
                    v-for="evt in activeChatEvents(chat.events)"
                    :key="evt"
                    class="inline-flex px-1.5 py-0.5 rounded text-xs bg-blue-900/50 text-blue-300"
                  >
                    {{ TelegramNotificationEventLabels[evt] }}
                  </span>
                  <span v-if="!activeChatEvents(chat.events).length" class="text-gray-600 text-xs">None</span>
                </div>
              </td>
              <td class="px-4 py-3 text-gray-400">
                <DateDisplay :date="chat.createdAt" mode="absolute" resolution="date" />
              </td>
              <td class="px-4 py-3 text-right">
                <button
                  class="text-gray-500 hover:text-red-400 transition-colors text-xs"
                  @click="confirmDeleteChat(chat)"
                >
                  Unpair
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Delete confirmation modal -->
    <ConfirmModal
      v-if="deletingChat"
      :title="`Unpair this chat?`"
      :message="`Are you sure you want to unpair the Telegram chat ${deletingChat.telegramUsername ? '@' + deletingChat.telegramUsername + ' ' : ''}(${deletingChat.telegramChatId})? This action cannot be undone.`"
      confirm-label="Unpair"
      @confirm="doDeleteChat"
      @cancel="deletingChat = null"
    />
  </div>
</template>

<script setup lang="ts">
import { TelegramNotificationEvent, TelegramNotificationEventLabels } from '~/types'
import type { TelegramChat } from '~/types'
import { useConfigStore } from '~/stores/config'
import { useOrgsStore } from '~/stores/orgs'
import { useProjectsStore } from '~/stores/projects'

const configStore = useConfigStore()
const orgsStore = useOrgsStore()
const projectsStore = useProjectsStore()

onMounted(async () => {
  await Promise.all([
    configStore.fetchTelegramPairings(),
    configStore.fetchTelegramChats(),
    orgsStore.fetchOrgs(),
    projectsStore.fetchProjects(),
  ])
})

// --- Redeem form ---

const redeemForm = reactive({
  code: '',
  botToken: '',
  scopeType: 'user' as 'user' | 'org' | 'project',
  orgId: '',
  projectId: '',
})

const redeeming = ref(false)
const redeemError = ref<string | null>(null)

function prefillCode(code: string) {
  redeemForm.code = code
}

async function handleRedeem() {
  redeeming.value = true
  redeemError.value = null
  try {
    await configStore.redeemPairingCode({
      code: redeemForm.code.toUpperCase(),
      botToken: redeemForm.botToken,
      orgId: redeemForm.scopeType === 'org' ? redeemForm.orgId || undefined : undefined,
      projectId: redeemForm.scopeType === 'project' ? redeemForm.projectId || undefined : undefined,
    })
    // Reset form on success
    redeemForm.code = ''
    redeemForm.botToken = ''
    redeemForm.scopeType = 'user'
    redeemForm.orgId = ''
    redeemForm.projectId = ''
    await configStore.fetchTelegramPairings()
  } catch (e: unknown) {
    if (e instanceof Error) {
      redeemError.value = e.message
    } else {
      redeemError.value = 'Failed to link chat. Check the pairing code and try again.'
    }
  } finally {
    redeeming.value = false
  }
}

// --- Refresh ---

async function refresh() {
  await Promise.all([
    configStore.fetchTelegramPairings(),
    configStore.fetchTelegramChats(),
  ])
}

// --- Delete chat ---

const deletingChat = ref<TelegramChat | null>(null)

function confirmDeleteChat(chat: TelegramChat) {
  deletingChat.value = chat
}

async function doDeleteChat() {
  if (!deletingChat.value) return
  await configStore.deleteTelegramChat(deletingChat.value.id)
  deletingChat.value = null
}

// --- Helpers ---

function chatScopeLabel(chat: TelegramChat): string {
  if (chat.projectId) {
    const project = projectsStore.projects.find(p => p.id === chat.projectId)
    return project ? `Project: ${project.name}` : `Project: ${chat.projectId.slice(0, 8)}…`
  }
  if (chat.orgId) {
    const org = orgsStore.orgs.find(o => o.id === chat.orgId)
    return org ? `Org: ${org.name}` : `Org: ${chat.orgId.slice(0, 8)}…`
  }
  return 'Personal'
}

function activeChatEvents(eventsMask: number): TelegramNotificationEvent[] {
  return (Object.values(TelegramNotificationEvent).filter(v => typeof v === 'number') as number[])
    .filter(v => (eventsMask & v) !== 0) as TelegramNotificationEvent[]
}
</script>
