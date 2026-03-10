<template>
  <div class="p-8">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div class="flex items-center gap-3">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
          { label: 'Milestones', to: `/projects/${id}/milestones`, icon: 'M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9' },
        ]" />
        <span class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">
          {{ store.milestones.length }}
        </span>
      </div>
      <button @click="showCreate = true"
        class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        New Milestone
      </button>
    </div>

    <!-- Filter tabs -->
    <div class="flex gap-2 mb-5">
      <button v-for="tab in tabs" :key="tab.value" @click="activeTab = tab.value"
        :class="[
          'text-sm px-3 py-1.5 rounded-lg transition-colors',
          activeTab === tab.value ? 'bg-gray-700 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
        ]">
        {{ tab.label }}
        <span class="ml-1 text-xs text-gray-500">{{ tab.count }}</span>
      </button>
    </div>

    <!-- Error -->
    <ErrorBox :error="store.error" />

    <!-- Loading -->
    <div v-if="store.loading" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <!-- Milestones List -->
    <div v-else class="space-y-3">
      <div v-if="filteredMilestones.length === 0" class="py-16 text-center bg-gray-900 border border-gray-800 rounded-xl">
        <p class="text-gray-400">No milestones found</p>
        <button @click="showCreate = true" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">
          Create the first milestone →
        </button>
      </div>

      <div v-for="milestone in filteredMilestones" :key="milestone.id"
        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 transition-colors">
        <div class="flex items-start justify-between gap-4">
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-2 mb-1">
              <span :class="milestone.status === 'open' ? 'bg-green-900/40 text-green-400' : 'bg-gray-800 text-gray-500'"
                class="text-xs px-2 py-0.5 rounded-full font-medium">
                {{ milestone.status === 'open' ? 'Open' : 'Closed' }}
              </span>
              <NuxtLink :to="`/projects/${id}/milestones/${milestone.id}`"
                class="text-base font-semibold text-white hover:text-brand-300 transition-colors">
                {{ milestone.title }}
              </NuxtLink>
            </div>
            <p v-if="milestone.description" class="text-sm text-gray-400 mt-1 line-clamp-2">
              {{ milestone.description }}
            </p>
            <div class="flex items-center gap-4 mt-2 text-xs text-gray-500">
              <span v-if="milestone.dueDate">
                Due {{ formatDate(milestone.dueDate) }}
              </span>
              <span>Created {{ formatDate(milestone.createdAt) }}</span>
            </div>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <button @click="openEdit(milestone)"
              class="text-gray-500 hover:text-gray-300 transition-colors p-1.5 rounded hover:bg-gray-800">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
            </button>
            <button @click="confirmDelete(milestone.id)"
              class="text-gray-500 hover:text-red-400 transition-colors p-1.5 rounded hover:bg-red-900/20">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">New Milestone</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="form.title" type="text" placeholder="Milestone title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.description" rows="3" placeholder="Describe this milestone..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Due Date</label>
            <input v-model="form.dueDate" type="date"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Milestone
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Edit Modal -->
    <div v-if="editMilestone" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Edit Milestone</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="editForm.title" type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="editForm.description" rows="3"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Due Date</label>
            <input v-model="editForm.dueDate" type="date"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Status</label>
            <select v-model="editForm.status"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="open">Open</option>
              <option value="closed">Closed</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitEdit"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Save Changes
          </button>
          <button @click="editMilestone = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
<!-- milestones.vue acts as a transparent parent so that both
     milestones/index.vue (list) and milestones/[milestoneId].vue (detail)
     are rendered as Nuxt child routes. Without this wrapper the detail page
     URL would change but the list view would never unmount. -->
