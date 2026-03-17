<template>
  <div class="p-6 max-w-5xl">
    <div class="flex items-center gap-3 mb-6">
      <NuxtLink to="/demo" class="text-gray-500 hover:text-gray-300 text-sm">← Demo</NuxtLink>
      <span class="text-gray-700">/</span>
      <h1 class="text-xl font-bold text-white">Kanban Dashboard Card</h1>
    </div>
    <p class="text-gray-400 text-sm mb-6">
      The kanban card can be added to the project dashboard multiple times — each instance bound to a different board.
      Drag the <strong class="text-gray-200">+ Kanban Board</strong> button from the dashboard toolbar directly onto the layout,
      or use the click-to-add flow.
    </p>
    <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
      <p class="text-sm text-gray-500">
        Live kanban dashboard cards are shown on any
        <NuxtLink to="/projects" class="text-brand-400 hover:text-brand-300">project dashboard</NuxtLink>
        in <strong class="text-gray-300">chart mode</strong>. Add one via
        <strong class="text-gray-300">Customize dashboard → drag "+ Kanban Board"</strong> onto the layout.
      </p>
    </div>
  </div>
</template>
