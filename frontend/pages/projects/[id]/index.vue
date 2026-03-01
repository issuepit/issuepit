<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentProject">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-2">
        <NuxtLink to="/projects" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <div :style="{ background: store.currentProject.color || '#4c6ef5' }"
          class="w-8 h-8 rounded-md flex items-center justify-center text-white font-bold">
          {{ store.currentProject.name.charAt(0).toUpperCase() }}
        </div>
        <h1 class="text-2xl font-bold text-white">{{ store.currentProject.name }}</h1>
        <span v-if="store.currentProject.isPrivate"
          class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full">Private</span>
      </div>
      <p v-if="store.currentProject.description" class="text-gray-400 text-sm mb-8 ml-16">
        {{ store.currentProject.description }}
      </p>

      <!-- Stats Row -->
      <div class="grid grid-cols-3 gap-4 mb-8">
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 text-center">
          <p class="text-2xl font-bold text-white">{{ store.currentProject.issueCount }}</p>
          <p class="text-xs text-gray-500 mt-0.5">Issues</p>
        </div>
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 text-center">
          <p class="text-2xl font-bold text-white">{{ store.currentProject.memberCount }}</p>
          <p class="text-xs text-gray-500 mt-0.5">Members</p>
        </div>
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 text-center">
          <p class="text-xs text-gray-500">Created</p>
          <p class="text-sm font-medium text-white mt-0.5">{{ formatDate(store.currentProject.createdAt) }}</p>
        </div>
      </div>

      <!-- Quick Links -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-4">
        <NuxtLink :to="`/projects/${id}/code`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-orange-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Code</p>
            <p class="text-xs text-gray-500">Browse repository</p>
          </div>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/issues`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-blue-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Issues</p>
            <p class="text-xs text-gray-500">View all issues</p>
          </div>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/kanban`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-purple-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Kanban</p>
            <p class="text-xs text-gray-500">Board view</p>
          </div>
        </NuxtLink>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <NuxtLink :to="`/projects/${id}/members`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-green-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Members</p>
            <p class="text-xs text-gray-500">{{ store.currentProject.memberCount }} members</p>
          </div>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/runs`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-yellow-900/30 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Runs</p>
            <p class="text-xs text-gray-500">CI/CD &amp; agent runs</p>
          </div>
        </NuxtLink>

        <NuxtLink :to="`/projects/${id}/settings`"
          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex items-center gap-4 transition-colors group">
          <div class="w-10 h-10 bg-gray-700/50 rounded-lg flex items-center justify-center">
            <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <div>
            <p class="font-semibold text-white group-hover:text-brand-300 transition-colors">Settings</p>
            <p class="text-xs text-gray-500">Project configuration</p>
          </div>
        </NuxtLink>
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
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const id = route.params.id as string
const store = useProjectsStore()

onMounted(() => store.fetchProject(id))

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}
</script>
