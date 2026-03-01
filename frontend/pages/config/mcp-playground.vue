<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-lg font-semibold text-white">IssuePit MCP Playground</h2>
        <p class="text-sm text-gray-400 mt-0.5">
          Interactive tool tester for the built-in IssuePit MCP server.
          Endpoint: <code class="text-green-300 font-mono text-xs">{{ mcpBase }}/mcp</code>
        </p>
      </div>
      <a
        :href="mcpBase"
        target="_blank"
        rel="noopener"
        class="flex items-center gap-2 px-4 py-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium rounded-lg transition-colors border border-gray-700"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
        </svg>
        Open in new tab
      </a>
    </div>

    <div class="rounded-xl overflow-hidden border border-gray-700" style="height: calc(100vh - 220px);">
      <iframe
        :src="mcpBase"
        sandbox="allow-scripts allow-same-origin allow-forms"
        class="w-full h-full bg-gray-950"
        title="IssuePit MCP Playground"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
const config = useRuntimeConfig()
const mcpBase = config.public.mcpBase as string
</script>
