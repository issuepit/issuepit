<template>
  <div class="p-8 max-w-2xl">
    <h1 class="text-2xl font-bold text-white mb-2">Settings</h1>
    <p class="text-gray-400 mb-8">Configure your IssuePit workspace.</p>

    <div class="space-y-6">
      <!-- API Section -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">API Configuration</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Backend API URL</label>
            <input :value="apiBase" disabled type="text"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-400 font-mono" />
            <p class="text-xs text-gray-600 mt-1">Set via NUXT_PUBLIC_API_BASE environment variable</p>
          </div>
        </div>
      </div>

      <!-- Appearance -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Appearance</h2>
        <p class="text-sm text-gray-500">Dark mode is the only theme currently supported.</p>
      </div>

      <!-- About -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-2">About</h2>
        <p class="text-sm text-gray-400">IssuePit v0.1.0 — Agent Orchestration Platform with Issue Tracking</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const apiBase = config.public.apiBase
</script>
