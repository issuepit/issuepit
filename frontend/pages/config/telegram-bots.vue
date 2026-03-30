<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">Telegram Bots</h2>
        <p class="text-sm text-gray-400 mt-0.5">Configure Telegram bots to receive notifications for orgs and projects.</p>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white text-sm font-medium rounded-lg transition-colors"
        @click="openCreate"
      >
        Add Bot
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.telegramBotsLoading" class="text-gray-500 text-sm">Loading…</div>

    <!-- Empty -->
    <div v-else-if="!store.telegramBots.length" class="rounded-lg border border-dashed border-gray-700 p-12 text-center">
      <p class="text-gray-500 text-sm">No Telegram bots configured yet.</p>
      <button class="mt-3 text-brand-400 hover:text-brand-300 text-sm" @click="openCreate">Add your first bot →</button>
    </div>

    <!-- Bots table -->
    <div v-else class="rounded-lg border border-gray-800 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-gray-900">
          <tr>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Chat ID</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Scope</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Events</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Notifications</th>
            <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
            <th class="px-4 py-3" />
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-800">
          <tr v-for="bot in store.telegramBots" :key="bot.id" class="hover:bg-gray-900/50 transition-colors">
            <td class="px-4 py-3 text-white font-medium">{{ bot.name }}</td>
            <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ bot.chatId }}</td>
            <td class="px-4 py-3 text-gray-400">{{ scopeLabel(bot) }}</td>
            <td class="px-4 py-3">
              <div class="flex flex-wrap gap-1">
                <span
                  v-for="evt in activeEvents(bot.events)"
                  :key="evt"
                  class="inline-flex px-1.5 py-0.5 rounded text-xs bg-blue-900/50 text-blue-300"
                >
                  {{ TelegramNotificationEventLabels[evt] }}
                </span>
                <span v-if="!activeEvents(bot.events).length" class="text-gray-600 text-xs">None</span>
              </div>
            </td>
            <td class="px-4 py-3 text-gray-400 text-xs">
              {{ DigestIntervalLabels[bot.digestInterval] }}{{ bot.isSilent ? ' · Silent' : '' }}
            </td>
            <td class="px-4 py-3 text-gray-400"><DateDisplay :date="bot.createdAt" mode="absolute" resolution="date" /></td>
            <td class="px-4 py-3 text-right space-x-3">
              <button class="text-gray-500 hover:text-brand-400 transition-colors text-xs" @click="openEdit(bot)">Edit</button>
              <button class="text-gray-500 hover:text-red-400 transition-colors text-xs" @click="confirmDelete(bot.id, bot.name)">Delete</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create / Edit modal -->
    <div v-if="showForm" class="fixed inset-0 z-50 flex items-center justify-center bg-black/60">
      <div class="bg-gray-900 rounded-xl border border-gray-700 w-full max-w-lg p-6 shadow-xl overflow-y-auto max-h-[90vh]">
        <h3 class="text-lg font-semibold text-white mb-5">{{ editingId ? 'Edit Telegram Bot' : 'Add Telegram Bot' }}</h3>
        <form class="space-y-4" @submit.prevent="handleSubmit">
          <div>
            <label class="block text-sm text-gray-400 mb-1">Name</label>
            <input v-model="form.name" type="text" required placeholder="e.g. Team Alerts"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label for="bot-token" class="block text-sm text-gray-400 mb-1">Bot Token{{ editingId ? ' (leave blank to keep current)' : '' }}</label>
            <input id="bot-token" v-model="form.botToken" type="password" :required="!editingId" placeholder="1234567890:AAAA..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label for="chat-id" class="block text-sm text-gray-400 mb-1">Chat ID</label>
            <input id="chat-id" v-model="form.chatId" type="text" required placeholder="-1001234567890"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label for="org-id" class="block text-sm text-gray-400 mb-1">Scope — Organization ID (optional)</label>
            <input id="org-id" v-model="form.orgId" type="text" placeholder="UUID of organization"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label for="project-id" class="block text-sm text-gray-400 mb-1">Scope — Project ID (optional)</label>
            <input id="project-id" v-model="form.projectId" type="text" placeholder="UUID of project"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500" />
          </div>
          <div>
            <label class="block text-sm text-gray-400 mb-2">Events</label>
            <div class="space-y-2">
              <label
                v-for="(label, val) in TelegramNotificationEventLabels"
                :key="val"
                class="flex items-center gap-2 cursor-pointer"
              >
                <input
                  type="checkbox"
                  :checked="isEventChecked(Number(val))"
                  class="w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
                  @change="toggleEvent(Number(val))"
                />
                <span class="text-sm text-gray-300">{{ label }}</span>
              </label>
            </div>
          </div>
          <div>
            <label for="digest-interval" class="block text-sm text-gray-400 mb-1">Notification frequency</label>
            <select
              id="digest-interval"
              v-model.number="form.digestInterval"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-brand-500"
            >
              <option v-for="(label, val) in DigestIntervalLabels" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
            <p class="text-xs text-gray-600 mt-1">Hourly and daily digests batch multiple events into a single message.</p>
          </div>
          <div class="flex items-center gap-3 rounded-lg bg-gray-800/50 border border-gray-700 p-3">
            <input
              id="is-silent"
              v-model="form.isSilent"
              type="checkbox"
              class="mt-0.5 w-4 h-4 rounded border-gray-600 text-brand-600 focus:ring-brand-500 bg-gray-700"
            />
            <label for="is-silent" class="text-sm text-gray-300 cursor-pointer">
              <span class="font-medium">Silent notifications</span>
              <span class="block text-xs text-gray-500 mt-0.5">Messages are delivered without sound.</span>
            </label>
          </div>
          <div class="flex gap-3 pt-2">
            <button type="submit" :disabled="saving"
              class="flex-1 px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors">
              {{ saving ? 'Saving…' : editingId ? 'Update Bot' : 'Add Bot' }}
            </button>
            <button type="button" class="px-4 py-2 text-gray-400 hover:text-gray-200 text-sm" @click="closeForm">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { TelegramNotificationEvent, TelegramNotificationEventLabels, DigestInterval, DigestIntervalLabels, TelegramSilentMode, TelegramSilentModeLabels } from '~/types'
import type { TelegramBot } from '~/types'
import { useConfigStore } from '~/stores/config'

const store = useConfigStore()

onMounted(() => store.fetchTelegramBots())

const showForm = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)

const form = reactive({
  name: '',
  botToken: '',
  chatId: '',
  orgId: '',
  projectId: '',
  events: 0,
  isSilent: false,
  digestInterval: DigestInterval.Immediate,
  silentMode: TelegramSilentMode.None,
  rateLimitCount: 0,
  rateLimitWindowMinutes: 0,
})

function openCreate() {
  editingId.value = null
  Object.assign(form, { name: '', botToken: '', chatId: '', orgId: '', projectId: '', events: 0, isSilent: false, digestInterval: DigestInterval.Immediate, silentMode: TelegramSilentMode.None, rateLimitCount: 0, rateLimitWindowMinutes: 0 })
  showForm.value = true
}

function openEdit(bot: TelegramBot) {
  editingId.value = bot.id
  Object.assign(form, {
    name: bot.name,
    botToken: '',
    chatId: bot.chatId,
    orgId: bot.orgId ?? '',
    projectId: bot.projectId ?? '',
    events: bot.events,
    isSilent: bot.isSilent,
    digestInterval: bot.digestInterval,
    silentMode: bot.silentMode ?? TelegramSilentMode.None,
    rateLimitCount: bot.rateLimitCount ?? 0,
    rateLimitWindowMinutes: bot.rateLimitWindowMinutes ?? 0,
  })
  showForm.value = true
}

function closeForm() {
  showForm.value = false
  editingId.value = null
}

function isEventChecked(val: number) {
  return (form.events & val) !== 0
}

function toggleEvent(val: number) {
  if (isEventChecked(val)) {
    form.events &= ~val
  } else {
    form.events |= val
  }
}

function activeEvents(eventsMask: number): TelegramNotificationEvent[] {
  return (Object.values(TelegramNotificationEvent).filter(v => typeof v === 'number') as number[])
    .filter(v => (eventsMask & v) !== 0) as TelegramNotificationEvent[]
}

function scopeLabel(bot: TelegramBot): string {
  if (bot.projectId) return `Project: ${bot.projectId.slice(0, 8)}…`
  if (bot.orgId) return `Org: ${bot.orgId.slice(0, 8)}…`
  return 'Global'
}

async function handleSubmit() {
  saving.value = true
  try {
    const payload = {
      name: form.name,
      botToken: form.botToken,
      chatId: form.chatId,
      events: form.events,
      isSilent: form.isSilent,
      digestInterval: form.digestInterval,
      silentMode: form.silentMode,
      rateLimitCount: form.rateLimitCount,
      rateLimitWindowMinutes: form.rateLimitWindowMinutes,
      orgId: form.orgId || undefined,
      projectId: form.projectId || undefined,
    }
    if (editingId.value) {
      await store.updateTelegramBot(editingId.value, payload)
    } else {
      await store.createTelegramBot(payload)
    }
    closeForm()
  } finally {
    saving.value = false
  }
}

const deletingBot = ref<TelegramBot | null>(null)

function confirmDelete(bot: TelegramBot) {
  deletingBot.value = bot
}

async function doDelete() {
  if (!deletingBot.value) return
  await store.deleteTelegramBot(deletingBot.value.id)
  deletingBot.value = null
}
</script>
