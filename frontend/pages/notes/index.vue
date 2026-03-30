<template>
  <div class="p-8 h-full flex flex-col">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6 shrink-0">
      <div class="flex items-center gap-3">
        <h1 class="text-2xl font-bold">Notes</h1>
        <span v-if="store.workspaces.length" class="text-sm text-gray-400">
          {{ store.workspaces.length }} workspace{{ store.workspaces.length !== 1 ? 's' : '' }}
        </span>
      </div>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium transition-colors"
        @click="showCreateWorkspace = true"
      >
        + Workspace
      </button>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="flex-1 flex items-center justify-center">
      <span class="text-gray-400">Loading workspaces…</span>
    </div>

    <!-- Error -->
    <div v-else-if="store.error" class="flex-1 flex items-center justify-center">
      <span class="text-red-400">{{ store.error }}</span>
    </div>

    <!-- Empty State -->
    <div
      v-else-if="!store.workspaces.length"
      class="flex-1 flex flex-col items-center justify-center text-gray-400"
    >
      <svg class="w-16 h-16 mb-4 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          stroke-width="1.5"
          d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"
        />
      </svg>
      <p class="text-lg mb-2">No workspaces yet</p>
      <p class="text-sm mb-4">Create a workspace to start organizing your notes.</p>
      <button
        class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium"
        @click="showCreateWorkspace = true"
      >
        Create First Workspace
      </button>
    </div>

    <!-- Workspace Grid -->
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 overflow-y-auto flex-1">
      <NuxtLink
        v-for="ws in store.workspaces"
        :key="ws.id"
        :to="`/notes/${ws.id}`"
        class="bg-gray-800 border border-gray-700 rounded-lg p-5 hover:border-brand-500 transition-colors group"
      >
        <div class="flex items-start justify-between mb-3">
          <h3 class="text-lg font-semibold group-hover:text-brand-400 transition-colors">
            {{ ws.name }}
          </h3>
          <button
            class="text-gray-500 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-opacity p-1"
            title="Delete workspace"
            @click.prevent="confirmDeleteWorkspace(ws)"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
              />
            </svg>
          </button>
        </div>
        <p v-if="ws.description" class="text-sm text-gray-400 mb-3 line-clamp-2">
          {{ ws.description }}
        </p>
        <div class="flex items-center gap-3 text-xs text-gray-500">
          <span>{{ ws.noteCount }} note{{ ws.noteCount !== 1 ? 's' : '' }}</span>
          <span>·</span>
          <DateDisplay :date="ws.updatedAt" mode="relative" />
        </div>
      </NuxtLink>
    </div>

    <!-- Create Workspace Modal -->
    <div
      v-if="showCreateWorkspace"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      @click.self="showCreateWorkspace = false"
    >
      <div class="bg-gray-800 border border-gray-700 rounded-xl p-6 w-full max-w-md">
        <h2 class="text-lg font-semibold mb-4">Create Workspace</h2>
        <form @submit.prevent="handleCreateWorkspace">
          <div class="mb-4">
            <label class="block text-sm text-gray-300 mb-1">Name</label>
            <input
              v-model="newWorkspaceName"
              type="text"
              required
              class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-sm focus:outline-none focus:border-brand-500"
              placeholder="My Notes"
            >
          </div>
          <div class="mb-4">
            <label class="block text-sm text-gray-300 mb-1">Description</label>
            <textarea
              v-model="newWorkspaceDescription"
              rows="2"
              class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-sm focus:outline-none focus:border-brand-500 resize-none"
              placeholder="Optional description"
            />
          </div>
          <div class="flex justify-end gap-2">
            <button
              type="button"
              class="px-4 py-2 text-sm text-gray-400 hover:text-white"
              @click="showCreateWorkspace = false"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="px-4 py-2 bg-brand-600 hover:bg-brand-700 text-white rounded-lg text-sm font-medium"
            >
              Create
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <div
      v-if="workspaceToDelete"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      @click.self="workspaceToDelete = null"
    >
      <div class="bg-gray-800 border border-gray-700 rounded-xl p-6 w-full max-w-md">
        <h2 class="text-lg font-semibold mb-2">Delete Workspace</h2>
        <p class="text-sm text-gray-400 mb-4">
          Are you sure you want to delete <strong class="text-white">{{ workspaceToDelete.name }}</strong>?
          This will permanently remove all notes in this workspace. This action cannot be undone.
        </p>
        <div class="flex justify-end gap-2">
          <button
            type="button"
            class="px-4 py-2 text-sm text-gray-400 hover:text-white"
            @click="workspaceToDelete = null"
          >
            Cancel
          </button>
          <button
            class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg text-sm font-medium"
            @click="handleDeleteWorkspace"
          >
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { NoteWorkspace } from '~/types'

const store = useNotesStore()
const showCreateWorkspace = ref(false)
const newWorkspaceName = ref('')
const newWorkspaceDescription = ref('')
const workspaceToDelete = ref<NoteWorkspace | null>(null)

onMounted(() => {
  store.fetchWorkspaces()
})

async function handleCreateWorkspace() {
  await store.createWorkspace({
    name: newWorkspaceName.value,
    description: newWorkspaceDescription.value || undefined,
  })
  newWorkspaceName.value = ''
  newWorkspaceDescription.value = ''
  showCreateWorkspace.value = false
}

function confirmDeleteWorkspace(ws: NoteWorkspace) {
  workspaceToDelete.value = ws
}

async function handleDeleteWorkspace() {
  if (!workspaceToDelete.value) return
  await store.deleteWorkspace(workspaceToDelete.value.id)
  workspaceToDelete.value = null
}
</script>
