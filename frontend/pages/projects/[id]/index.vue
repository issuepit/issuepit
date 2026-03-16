<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentProject">
      <!-- Header -->
      <div class="flex items-center justify-between gap-3 mb-2">
        <div class="flex items-center gap-3 min-w-0 flex-wrap">
          <PageBreadcrumb :items="[
            { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
            { label: store.currentProject.name, to: `/projects/${id}`, color: store.currentProject.color || '#4c6ef5' },
          ]" />
          <span v-if="store.currentProject.isPrivate"
            class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full shrink-0">Private</span>
          <!-- Small badges for members and MCP servers -->
          <NuxtLink :to="`/projects/${id}/members`"
            class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-gray-200 px-2 py-0.5 rounded-full shrink-0 transition-colors flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            {{ store.currentProject.memberCount }} members
          </NuxtLink>
          <span v-if="mcpCount > 0"
            class="text-xs bg-gray-800 text-gray-400 px-2 py-0.5 rounded-full shrink-0 flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2" />
            </svg>
            {{ mcpCount }} MCP
          </span>
        </div>
        <!-- Header actions: New Issue, Voice, Settings -->
        <div class="flex items-center gap-2 shrink-0">
          <button @click="showVoiceCreate = true"
            @dragover.prevent="voiceDragOver = true"
            @dragleave="voiceDragOver = false"
            @drop.prevent="handleVoiceFileDrop"
            :class="[
              'flex items-center gap-2 text-gray-300 text-sm font-medium px-4 py-2 rounded-lg transition-colors',
              voiceDragOver ? 'bg-brand-700 ring-2 ring-brand-400' : 'bg-gray-800 hover:bg-gray-700'
            ]"
            title="Create issue from voice (or drop an audio file here)">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
            Voice
          </button>
          <button @click="showCreate = true"
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New Issue
          </button>
        </div>
      </div>
      <p v-if="store.currentProject.description" class="text-gray-400 text-sm mb-6">
        {{ store.currentProject.description }}
      </p>

      <!-- Quick Navigation Menu -->
      <nav class="flex items-center gap-0.5 bg-gray-900 border border-gray-800 rounded-xl px-2 py-1.5 mb-6 overflow-x-auto flex-wrap">
        <NuxtLink :to="`/projects/${id}/code`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-orange-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
          </svg>
          Code
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/code?tab=commits`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap">
          <svg class="w-3.5 h-3.5 text-amber-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          Commits
          <span v-if="commitCountLabel !== '—'" class="text-xs text-gray-600">{{ commitCountLabel }}</span>
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/issues`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-blue-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          Issues
          <span class="text-xs text-gray-600">{{ store.currentProject.issueCount }}</span>
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/kanban`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-purple-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
          </svg>
          Kanban
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/runs`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-sky-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          Runs
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/merge-requests`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-green-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
          </svg>
          MRs
          <span v-if="store.currentProject.openMergeRequestCount > 0" class="text-xs text-gray-600">{{ store.currentProject.openMergeRequestCount }}</span>
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/review`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-teal-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
          </svg>
          Review
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/milestones`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-indigo-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9" />
          </svg>
          Milestones
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/members`"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 text-rose-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          Members
          <span class="text-xs text-gray-600">{{ store.currentProject.memberCount }}</span>
        </NuxtLink>
        <NuxtLink :to="`/projects/${id}/settings`"
          class="ml-auto flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-500 hover:text-gray-300 hover:bg-gray-800 transition-colors text-sm whitespace-nowrap"
          active-class="text-white bg-gray-800">
          <svg class="w-3.5 h-3.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          Settings
        </NuxtLink>
        <button v-if="!isDraftMode" @click="enterDraftMode"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-gray-500 hover:text-amber-400 hover:bg-gray-800 transition-colors text-sm whitespace-nowrap">
          <svg class="w-3.5 h-3.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
          </svg>
          Customize
        </button>
      </nav>

      <!-- Draft mode toolbar -->
      <div v-if="isDraftMode"
        class="mb-4 bg-amber-950/40 border border-amber-700/40 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
        <div class="flex items-center gap-2 text-amber-300 text-sm">
          <svg class="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
          </svg>
          <span class="font-medium">Draft mode</span>
          <span class="text-amber-400/70 text-xs hidden sm:inline">— drag to reorder · configure each section</span>
        </div>
        <div class="flex items-center gap-1.5">
          <button
            draggable="true"
            @click="addRowBreak()"
            @dragstart.stop="(e: DragEvent) => { captureSnapshot(); const id = addRowBreak(); onSectionDragStart(e, id) }"
            aria-label="Add row break to dashboard layout"
            class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1 cursor-grab">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Row break
          </button>
          <button @click="resetLayout"
            class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
            Reset
          </button>
          <button @click="cancelDraftMode"
            class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
            Cancel
          </button>
          <button @click="saveDraftMode"
            class="text-xs bg-amber-600 hover:bg-amber-700 text-white px-3 py-1.5 rounded-lg transition-colors font-medium">
            Save
          </button>
        </div>
      </div>

      <!-- Hidden sections restore row (draft mode) -->
      <div v-if="isDraftMode && layout.configs" class="mb-3 flex flex-wrap items-center gap-2 min-h-0">
        <template v-for="sid in layout.order" :key="sid">
          <button v-if="sectionCfg(sid as SectionId).hidden" @click="showSection(sid as SectionId)"
            class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-green-400 px-2.5 py-1 rounded-lg transition-colors flex items-center gap-1">
            <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            {{ SECTION_LABELS[sid as SectionId] }}
          </button>
        </template>
      </div>

      <!-- Main content grid: 12 columns -->
      <div class="grid grid-cols-12 gap-4 items-start">
        <template v-for="item in renderedItems" :key="item.type === 'section' || item.type === 'rowbreak' ? item.sid : item.key">
          <!-- Row break: forces a new grid row; shows separator handle in draft mode -->
          <template v-if="item.type === 'rowbreak'">
            <div v-if="isDraftMode" class="col-span-12 flex items-center gap-2 py-1 cursor-grab"
              draggable="true"
              @dragstart.stop="(e: DragEvent) => onSectionDragStart(e, item.sid)"
              @dragover.prevent="onSectionDragOver($event, item.sid)"
              @dragenter="onSectionDragEnter($event, item.sid)"
              @dragend="onSectionDragEnd($event)">
              <div class="flex-1 border-t border-dashed border-orange-500/60"></div>
              <span class="text-xs text-orange-500/80 select-none whitespace-nowrap">⋮⋮ row break</span>
              <div class="flex-1 border-t border-dashed border-orange-500/60"></div>
              <button @click="removeRowBreak(item.sid)"
                aria-label="Remove row break"
                class="text-orange-600/60 hover:text-red-400 transition-colors text-xs leading-none px-1">✕</button>
            </div>
            <div v-else class="col-span-12 h-0"></div>
          </template>

          <div v-else
            data-drag-card
            :class="[
              itemColSpanClass(item),
              isDraftMode ? 'select-none relative' : '',
              (item.type === 'section' ? dragSectionId === item.sid : (item.sections ?? []).includes(dragSectionId as SectionId))
                ? 'opacity-50'
                : '',
            ]"
            :draggable="isDraftMode"
            @dragstart="isDraftMode && (item.type === 'section' || item.type === 'stackgroup') ? onSectionDragStart($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
            @dragover.prevent="isDraftMode ? onSectionDragOver($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
            @dragenter="isDraftMode ? onSectionDragEnter($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
            @dragend="isDraftMode ? onSectionDragEnd($event) : undefined">

            <!-- Gap zone sentinels: invisible divs in the CSS gap on each side of the card.
                 Only rendered when dragging; the card itself does NOT trigger reorder — only these do. -->
            <template v-if="isDraftMode && dragSectionId !== null && !((item.type === 'section' ? dragSectionId === item.sid : (item.sections ?? []).includes(dragSectionId as SectionId)))">
              <!-- Left gap sentinel → insert BEFORE this item -->
              <div
                class="absolute inset-y-0 -left-2 w-2 z-30 pointer-events-auto"
                @dragover.prevent
                @dragenter="onSectionGapDragEnter($event, item.type === 'section' ? item.sid : item.sections[0], false)"
                @dragleave="onGapDragLeave()"
              />
              <!-- Right gap sentinel → insert AFTER this item -->
              <div
                class="absolute inset-y-0 -right-2 w-2 z-30 pointer-events-auto"
                @dragover.prevent
                @dragenter="onSectionGapDragEnter($event, item.type === 'section' ? item.sid : item.sections[0], true)"
                @dragleave="onGapDragLeave()"
              />
            </template>

            <!-- Draft mode header: shows for tab group or single section -->
            <template v-if="isDraftMode">
              <!-- For single section: full config bar -->
              <DashboardSectionBar
                v-if="item.type === 'section'"
                :label="SECTION_LABELS[item.sid as SectionId]"
                :display-modes="SECTION_DISPLAY_MODES[item.sid as SectionId]"
                :current-display-mode="sectionCfg(item.sid as SectionId).displayMode"
                :has-max-items="SECTION_HAS_MAX_ITEMS.has(item.sid as SectionId)"
                :max-items-options="MAX_ITEMS_OPTIONS"
                :current-max-items="sectionCfg(item.sid as SectionId).maxItems"
                :widths="PROJECT_WIDTHS"
                :current-width="sectionCfg(item.sid as SectionId).width"
                :current-chart-days="item.sid === 'history' ? (sectionCfg(item.sid as SectionId).chartDays ?? CHART_DAY_DEFAULT) : undefined"
                :chart-height-options="item.sid === 'history' ? CHART_HEIGHT_OPTIONS : undefined"
                :current-chart-height="item.sid === 'history' ? (sectionCfg(item.sid as SectionId).chartHeightKey ?? 'md') : undefined"
                :kanban-boards="item.sid === 'kanban' ? kanbanStore.boards : undefined"
                :selected-kanban-board-id="item.sid === 'kanban' ? sectionCfg('kanban').selectedBoardId : undefined"
                :can-tab="layout.order.indexOf(item.sid) < layout.order.length - 1"
                :is-tabbed="sectionCfg(item.sid as SectionId).tabGroup !== null"
                :can-stack="SECTION_CAN_STACK.has(item.sid as SectionId) && layout.order.indexOf(item.sid) < layout.order.length - 1"
                :is-stacked="sectionCfg(item.sid as SectionId).stackGroup !== null"
                :hidden="sectionCfg(item.sid as SectionId).hidden"
                :drag-hover="dragSectionId !== null && dragHoverSid === item.sid && dragSectionId !== item.sid"
                @display-mode-change="m => updateCfg(item.sid as SectionId, { displayMode: m as SectionDisplayMode })"
                @max-items-change="n => updateCfg(item.sid as SectionId, { maxItems: n })"
                @width-change="w => updateCfg(item.sid as SectionId, { width: w as SectionWidth })"
                @chart-days-change="d => updateCfg(item.sid as SectionId, { chartDays: d })"
                @chart-height-change="k => updateCfg(item.sid as SectionId, { chartHeightKey: k })"
                @kanban-board-change="boardId => updateCfg('kanban', { selectedBoardId: boardId })"
                @tab-toggle="toggleTabGroupWithNext(item.sid as SectionId)"
                @tab-drop="droppedSid => tabWithSection(item.sid, droppedSid)"
                @stack-toggle="toggleStackGroupWithNext(item.sid as SectionId)"
                @stack-drop="droppedSid => stackWithSection(item.sid, droppedSid)"
                @hide="hideSection(item.sid as SectionId)"
                @show="showSection(item.sid as SectionId)"
              />
              <!-- For tab group: label + width + split -->
              <DashboardTabGroupBar
                v-else-if="item.type === 'tabgroup'"
                :sections="item.sections"
                :section-labels="SECTION_LABELS"
                :widths="PROJECT_WIDTHS"
                :current-width="sectionCfg(item.sections[0] as SectionId).width"
                @split="toggleTabGroupWithNext(item.sections[0] as SectionId)"
                @width-change="w => updateCfg(item.sections[0] as SectionId, { width: w as SectionWidth })"
              />
              <!-- For stack group: label + width + unstack -->
              <DashboardStackGroupBar
                v-else-if="item.type === 'stackgroup'"
                :sections="item.sections"
                :section-labels="SECTION_LABELS"
                :widths="PROJECT_WIDTHS"
                :current-width="sectionCfg(item.sections[0] as SectionId).width"
                :is-dragging="!!dragSectionId"
                @split="toggleStackGroupWithNext(item.sections[0] as SectionId)"
                @width-change="w => updateCfg(item.sections[0] as SectionId, { width: w as SectionWidth })"
                @stack-drop="droppedSid => stackWithSection(item.sections[0] as SectionId, droppedSid)"
              />
            </template>

            <!-- Content area -->
            <div :class="[
              isDraftMode && item.type === 'section' && sectionCfg(item.sid as SectionId).hidden ? 'opacity-30 saturate-0 pointer-events-none' : '',
              item.type === 'stackgroup' ? 'flex flex-col gap-2' : '',
            ]">

              <!-- Tab nav header (for tab groups) -->
              <div v-if="item.type === 'tabgroup'"
                class="bg-gray-900 border border-gray-800 rounded-t-xl border-b-0 flex overflow-x-auto">
                <button v-for="sec in item.sections" :key="sec"
                  @click="setActiveTab(item.key, sec)"
                  :class="[
                    getActiveTab(item.key, item.sections) === sec
                      ? 'text-white border-b-2 border-brand-400 bg-gray-800/50'
                      : 'text-gray-500 hover:text-gray-300',
                  ]"
                  class="px-4 py-2.5 text-sm font-medium transition-colors whitespace-nowrap flex-shrink-0">
                  {{ SECTION_LABELS[sec as SectionId] }}
                </button>
              </div>

              <!-- Render each section in the item (for tabgroup: use v-show; for single/stackgroup: always shown) -->
              <template v-for="sid in (item.type === 'tabgroup' || item.type === 'stackgroup' ? item.sections : [item.sid])" :key="sid">
                <div v-show="item.type !== 'tabgroup' || getActiveTab(item.key, item.sections) === sid"
                  :class="item.type === 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-b-xl p-5' : ''">

                  <!-- ── STAT: Issues ── -->
                  <template v-if="sid === 'statIssues'">
                    <NuxtLink :to="`/projects/${id}/issues`"
                      class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group block">
                      <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
                        {{ store.currentProject?.issueCount }}</p>
                      <p class="text-xs text-gray-500 mt-0.5">Issues</p>
                    </NuxtLink>
                  </template>

                  <!-- ── STAT: Commits ── -->
                  <template v-else-if="sid === 'statCommits'">
                    <NuxtLink :to="`/projects/${id}/code?tab=commits`"
                      class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group block">
                      <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
                        {{ commitCountLabel }}</p>
                      <p class="text-xs text-gray-500 mt-0.5">Commits</p>
                    </NuxtLink>
                  </template>

                  <!-- ── STAT: Open MRs ── -->
                  <template v-else-if="sid === 'statMRs'">
                    <NuxtLink :to="`/projects/${id}/merge-requests`"
                      class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 text-center transition-colors group block">
                      <p class="text-2xl font-bold text-white group-hover:text-brand-300 transition-colors">
                        {{ store.currentProject?.openMergeRequestCount }}</p>
                      <p class="text-xs text-gray-500 mt-0.5">Open MRs</p>
                    </NuxtLink>
                  </template>

                  <!-- ── MILESTONES section ── -->
                  <template v-else-if="sid === 'milestones'">
                    <!-- Count mode -->
                    <template v-if="sectionCfg('milestones').displayMode === 'count'">
                      <NuxtLink :to="`/projects/${id}/milestones`"
                        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex flex-col items-center justify-center min-h-24 transition-colors group">
                        <p class="text-4xl font-bold text-white group-hover:text-brand-300">{{ milestones.filter(m => m.status === 'open').length }}</p>
                        <p class="text-sm text-gray-500 mt-1">Open Milestones</p>
                      </NuxtLink>
                    </template>
                    <!-- List mode -->
                    <template v-else-if="sectionCfg('milestones').displayMode === 'list'">
                      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                        <div class="flex items-center justify-between mb-4">
                          <h2 class="font-semibold text-white flex items-center gap-2">
                            <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9" />
                            </svg>
                            Milestones
                          </h2>
                          <NuxtLink :to="`/projects/${id}/milestones`"
                            class="text-xs text-brand-400 hover:text-brand-300 transition-colors">View all →</NuxtLink>
                        </div>
                        <div v-if="openMilestones.length" class="space-y-2">
                          <NuxtLink v-for="m in openMilestones" :key="m.id"
                            :to="`/projects/${id}/milestones/${m.id}`"
                            class="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-800 transition-colors group">
                            <span class="w-2 h-2 rounded-full bg-indigo-400 shrink-0"></span>
                            <span class="text-sm text-gray-200 truncate group-hover:text-brand-300 flex-1">{{ m.title }}</span>
                            <span v-if="m.dueDate" class="text-xs text-gray-500 shrink-0">{{ formatDate(m.dueDate) }}</span>
                          </NuxtLink>
                        </div>
                        <p v-else class="text-sm text-gray-600 py-3 text-center">No open milestones</p>
                      </div>
                    </template>
                    <!-- Block mode (default cards) -->
                    <template v-else>
                      <div class="flex items-center justify-between mb-3">
                        <h2 class="font-semibold text-white flex items-center gap-2">
                          <svg class="w-4 h-4 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              d="M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9" />
                          </svg>
                          Milestones
                        </h2>
                        <NuxtLink :to="`/projects/${id}/milestones`"
                          class="text-xs text-brand-400 hover:text-brand-300 transition-colors">View all →</NuxtLink>
                      </div>
                      <div v-if="openMilestones.length" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                        <NuxtLink v-for="milestone in openMilestones" :key="milestone.id"
                          :to="`/projects/${id}/milestones/${milestone.id}`"
                          class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 transition-colors group">
                          <div class="flex items-start justify-between gap-2 mb-2">
                            <span class="text-sm font-medium text-white group-hover:text-brand-300 transition-colors truncate">
                              {{ milestone.title }}
                            </span>
                            <span class="text-xs bg-green-900/40 text-green-400 px-1.5 py-0.5 rounded-full shrink-0">Open</span>
                          </div>
                          <p v-if="milestone.description" class="text-xs text-gray-500 line-clamp-1 mb-2">{{ milestone.description }}</p>
                          <p v-if="milestone.dueDate" class="text-xs text-gray-500">Due {{ formatDate(milestone.dueDate) }}</p>
                        </NuxtLink>
                      </div>
                      <p v-else class="text-sm text-gray-600 py-2">No open milestones</p>
                    </template>
                  </template>

                  <!-- ── ISSUES section ── -->
                  <template v-else-if="sid === 'issues'">
                    <!-- Count mode -->
                    <template v-if="sectionCfg('issues').displayMode === 'count'">
                      <NuxtLink :to="`/projects/${id}/issues`"
                        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex flex-col items-center justify-center min-h-24 transition-colors group">
                        <p class="text-4xl font-bold text-white group-hover:text-brand-300">{{ recentProjectIssues.length }}</p>
                        <p class="text-sm text-gray-500 mt-1 flex items-center gap-1.5">
                          <svg class="w-3.5 h-3.5 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                          </svg>
                          Issues
                        </p>
                      </NuxtLink>
                    </template>
                    <!-- List mode -->
                    <template v-else>
                      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                        <div class="flex items-center justify-between mb-4">
                          <h2 class="font-semibold text-white flex items-center gap-2">
                            <svg class="w-4 h-4 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                                d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                            </svg>
                            Recent Issues
                          </h2>
                          <NuxtLink :to="`/projects/${id}/issues`" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">View all →</NuxtLink>
                        </div>
                        <div v-if="recentIssues.length" class="space-y-1.5">
                          <NuxtLink v-for="issue in recentIssues" :key="issue.id"
                            :to="`/projects/${id}/issues/${issue.number}`"
                            class="flex items-start gap-2.5 p-2 rounded-lg hover:bg-gray-800 transition-colors group">
                            <span :class="issueStatusDot(issue.status)" class="w-2 h-2 rounded-full shrink-0 mt-1.5"></span>
                            <div class="flex-1 min-w-0">
                              <p class="text-sm text-gray-200 truncate group-hover:text-brand-300 transition-colors">{{ issue.title }}</p>
                              <p class="text-xs text-gray-500">{{ formatIssueId(issue.number, store.currentProject) }} · {{ relativeTime(issue.updatedAt) }}</p>
                            </div>
                            <span :class="issuePriorityBadge(issue.priority)" class="text-xs px-1.5 py-0.5 rounded font-medium shrink-0">
                              {{ issue.priority === 'no_priority' ? '—' : issue.priority }}
                            </span>
                          </NuxtLink>
                        </div>
                        <p v-else class="text-sm text-gray-600 py-4 text-center">No issues yet</p>
                      </div>
                    </template>
                  </template>

                  <!-- ── AGENT RUNS section ── -->
                  <template v-else-if="sid === 'agentRuns'">
                    <!-- Count mode -->
                    <template v-if="sectionCfg('agentRuns').displayMode === 'count'">
                      <NuxtLink :to="`/projects/${id}/runs?tab=agent`"
                        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex flex-col items-center justify-center min-h-24 transition-colors group">
                        <div class="flex items-center gap-2 mb-1">
                          <p class="text-4xl font-bold text-white group-hover:text-brand-300">{{ runsStore.agentSessions.length }}</p>
                          <span v-if="isConnected" class="w-2 h-2 rounded-full bg-green-400 animate-pulse shrink-0"></span>
                        </div>
                        <p class="text-sm text-gray-500 flex items-center gap-1.5">
                          <svg class="w-3.5 h-3.5 text-fuchsia-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
                          </svg>
                          Agent Runs
                        </p>
                      </NuxtLink>
                    </template>
                    <!-- List mode -->
                    <template v-else>
                      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                        <div class="flex items-center justify-between mb-4">
                          <h2 class="font-semibold text-white flex items-center gap-2">
                            <svg class="w-4 h-4 text-fuchsia-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2" />
                            </svg>
                            Recent Agent Runs
                            <span class="text-xs font-normal text-gray-500">({{ runsStore.agentSessions.length }})</span>
                            <span v-if="isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal">
                              <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" /> Live
                            </span>
                            <span v-else class="flex items-center gap-1 text-xs text-gray-600 font-normal">
                              <span class="w-1.5 h-1.5 rounded-full bg-gray-600" /> Offline
                            </span>
                          </h2>
                          <NuxtLink :to="`/projects/${id}/runs?tab=agent`" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">View all →</NuxtLink>
                        </div>
                        <div v-if="visibleAgentSessions.length" class="space-y-2">
                          <div v-for="session in visibleAgentSessions" :key="session.id"
                            class="flex items-center gap-3 py-2 border-b border-gray-800 last:border-0 cursor-pointer"
                            @click="navigateTo(`/projects/${id}/runs/agent-sessions/${session.id}`)">
                            <AgentSessionStatusChip :session="session" class="shrink-0" />
                            <div class="flex-1 min-w-0">
                              <NuxtLink :to="`/projects/${id}/issues/${session.issueNumber}`"
                                class="text-sm text-gray-300 hover:text-brand-300 transition-colors truncate block" @click.stop>
                                #{{ formatIssueId(session.issueNumber, store.currentProject) }} {{ session.issueTitle }}
                              </NuxtLink>
                              <p class="text-xs text-gray-500 truncate">{{ session.agentName }}</p>
                            </div>
                            <span class="text-xs text-gray-600 shrink-0">{{ relativeTime(session.startedAt) }}</span>
                          </div>
                        </div>
                        <p v-else class="text-sm text-gray-600 py-4 text-center">No agent runs yet</p>
                      </div>
                    </template>
                  </template>

                  <!-- ── CI/CD RUNS section ── -->
                  <template v-else-if="sid === 'cicdRuns'">
                    <!-- Count mode -->
                    <template v-if="sectionCfg('cicdRuns').displayMode === 'count'">
                      <NuxtLink :to="`/projects/${id}/runs`"
                        class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 flex flex-col items-center justify-center min-h-24 transition-colors group">
                        <div class="flex items-center gap-2 mb-1">
                          <p class="text-4xl font-bold text-white group-hover:text-brand-300">{{ runsStore.runs.length }}</p>
                          <span v-if="isConnected" class="w-2 h-2 rounded-full bg-green-400 animate-pulse shrink-0"></span>
                        </div>
                        <p class="text-sm text-gray-500 flex items-center gap-1.5">
                          <svg class="w-3.5 h-3.5 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                          </svg>
                          CI/CD Runs
                        </p>
                      </NuxtLink>
                    </template>
                    <!-- List mode -->
                    <template v-else>
                      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
                        <div class="flex items-center justify-between mb-4">
                          <h2 class="font-semibold text-white flex items-center gap-2">
                            <svg class="w-4 h-4 text-sky-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                            </svg>
                            Recent CI/CD Runs
                            <span class="text-xs font-normal text-gray-500">({{ runsStore.runs.length }})</span>
                            <span v-if="isConnected" class="flex items-center gap-1 text-xs text-green-400 font-normal">
                              <span class="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" /> Live
                            </span>
                            <span v-else class="flex items-center gap-1 text-xs text-gray-600 font-normal">
                              <span class="w-1.5 h-1.5 rounded-full bg-gray-600" /> Offline
                            </span>
                          </h2>
                          <NuxtLink :to="`/projects/${id}/runs`" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">View all →</NuxtLink>
                        </div>
                        <div v-if="visibleCiCdRuns.length" class="space-y-2">
                          <div v-for="run in visibleCiCdRuns" :key="run.id"
                            class="flex items-center gap-3 py-2 border-b border-gray-800 last:border-0 cursor-pointer"
                            @click="navigateTo(`/projects/${id}/runs/cicd/${run.id}`)">
                            <CiCdStatusChip :runs="[run]" class="shrink-0" />
                            <div class="flex-1 min-w-0">
                              <p class="text-sm text-gray-300 truncate">{{ run.workflow || run.branch || 'Run' }}</p>
                              <p class="text-xs text-gray-500 font-mono truncate">
                                {{ run.commitSha?.slice(0, 7) || '—' }}<span v-if="run.branch"> · {{ run.branch }}</span>
                              </p>
                            </div>
                            <span class="text-xs text-gray-600 shrink-0">{{ relativeTime(run.startedAt) }}</span>
                          </div>
                        </div>
                        <p v-else class="text-sm text-gray-600 py-4 text-center">No CI/CD runs yet</p>
                      </div>
                    </template>
                  </template>

                  <!-- ── HISTORY section ── -->
                  <template v-else-if="sid === 'history'">
                    <div :class="item.type === 'tabgroup' ? '' : 'bg-gray-900 border border-gray-800 rounded-xl p-5'">
                      <template v-if="item.type !== 'tabgroup'">
                        <h2 class="font-semibold text-white mb-4">Issue &amp; Run History (last {{ sectionCfg('history').chartDays ?? CHART_DAY_DEFAULT }}d)</h2>
                      </template>
                      <div v-if="filteredMetricSnapshots.length" class="overflow-x-auto">
                        <svg :viewBox="`0 0 ${chartWidth} ${chartHeightPx}`" class="w-full" style="min-width:500px">
                          <line v-for="y in gridYValues" :key="y"
                            :x1="chartPad" :y1="yScale(y)" :x2="chartWidth - chartPad" :y2="yScale(y)"
                            stroke="#374151" stroke-width="1" />
                          <text v-for="y in gridYValues" :key="`yl-${y}`"
                            :x="chartPad - 6" :y="yScale(y) + 4" text-anchor="end" fill="#6b7280" font-size="10">{{ y }}</text>
                          <polyline :points="linePoints('openIssues')" fill="none" stroke="#f59e0b" stroke-width="2" stroke-linejoin="round" />
                          <polyline :points="linePoints('inProgressIssues')" fill="none" stroke="#6366f1" stroke-width="2" stroke-linejoin="round" />
                          <polyline :points="linePoints('doneIssues')" fill="none" stroke="#22c55e" stroke-width="2" stroke-linejoin="round" />
                          <polyline :points="linePoints('totalAgentRuns')" fill="none" stroke="#e879f9" stroke-width="2" stroke-linejoin="round" stroke-dasharray="4 2" />
                          <polyline :points="linePoints('totalCiCdRuns')" fill="none" stroke="#38bdf8" stroke-width="2" stroke-linejoin="round" stroke-dasharray="4 2" />
                          <text v-for="(snap, i) in filteredMetricSnapshots" :key="`xl-${i}`"
                            :x="xPos(i)" :y="chartHeightPx - 4" text-anchor="middle" fill="#6b7280" font-size="9">{{ shortTime(snap.recordedAt) }}</text>
                        </svg>
                      </div>
                      <div v-else class="py-8 text-center text-sm text-gray-500">No history yet — snapshots are saved hourly</div>
                      <div class="flex flex-wrap items-center gap-5 mt-3">
                        <span class="flex items-center gap-1.5 text-xs text-gray-400">
                          <span class="w-3 h-0.5 bg-amber-400 rounded-full inline-block"></span> Open
                        </span>
                        <span class="flex items-center gap-1.5 text-xs text-gray-400">
                          <span class="w-3 h-0.5 bg-indigo-400 rounded-full inline-block"></span> In Progress
                        </span>
                        <span class="flex items-center gap-1.5 text-xs text-gray-400">
                          <span class="w-3 h-0.5 bg-green-400 rounded-full inline-block"></span> Done
                        </span>
                        <span class="flex items-center gap-1.5 text-xs text-gray-400">
                          <span class="w-3 h-0.5 inline-block" style="background:repeating-linear-gradient(90deg,#e879f9 0,#e879f9 4px,transparent 4px,transparent 6px)"></span> Agent Runs
                        </span>
                        <span class="flex items-center gap-1.5 text-xs text-gray-400">
                          <span class="w-3 h-0.5 inline-block" style="background:repeating-linear-gradient(90deg,#38bdf8 0,#38bdf8 4px,transparent 4px,transparent 6px)"></span> CI/CD Runs
                        </span>
                      </div>
                    </div>
                  </template>

                  <!-- ── KANBAN section ── -->
                  <template v-else-if="sid === 'kanban'">
                    <div :class="item.type === 'tabgroup' ? '' : 'bg-gray-900 border border-gray-800 rounded-xl p-5'">
                      <template v-if="item.type !== 'tabgroup'">
                        <h2 class="font-semibold text-white mb-4">Kanban Board</h2>
                      </template>
                      <KanbanBoardInline :project-id="id" :board-id="sectionCfg('kanban').selectedBoardId ?? null" />
                    </div>
                  </template>

                </div>
              </template>
            </div>
          </div>
        </template>
      </div>

      <!-- Customize button (when not in draft mode) -->
      <div v-if="!isDraftMode" class="flex justify-center mt-4 mb-2">
        <button @click="enterDraftMode"
          class="flex items-center gap-1.5 text-xs text-gray-600 hover:text-gray-400 transition-colors px-3 py-1.5 rounded-lg hover:bg-gray-800/50">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
          </svg>
          Customize dashboard
        </button>
      </div>
    </template>

    <!-- Not found / Error -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">{{ store.error || 'Project not found' }}</p>
      <NuxtLink to="/projects" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Projects</NuxtLink>
    </div>

    <!-- Create Issue Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="form.title" type="text" placeholder="Issue title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="form.body" rows="4" placeholder="Describe the issue..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Status</label>
              <select v-model="form.status"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="s in statuses" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">Priority</label>
              <select v-model="form.priority"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
                <option v-for="p in priorities" :key="p.value" :value="p.value">{{ p.label }}</option>
              </select>
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Type</label>
            <select v-model="form.type"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option v-for="t in types" :key="t.value" :value="t.value">{{ t.label }}</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate" :disabled="!form.title"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Voice Create Modal -->
    <div v-if="showVoiceCreate"
      class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue from Voice</h2>

        <!-- Recording controls -->
        <div class="flex flex-col items-center gap-4 mb-5">
          <!-- Mic button (also a drop zone for audio files) -->
          <button
            v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            @click="startVoiceRecording"
            @dragover.prevent="modalDragOver = true"
            @dragleave="modalDragOver = false"
            @drop.prevent="handleModalVoiceFileDrop"
            :class="[
              'w-16 h-16 rounded-full flex items-center justify-center transition-all shadow-lg',
              modalDragOver ? 'bg-brand-500 ring-4 ring-brand-300 scale-110' : 'bg-brand-600 hover:bg-brand-700'
            ]"
            title="Click to record or drop an audio file">
            <svg class="w-7 h-7 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
          </button>
          <p v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            class="text-sm text-gray-400">Click to start recording or drop an audio file</p>

          <!-- Recording indicator -->
          <div v-if="voice.recording.value" class="flex flex-col items-center gap-3">
            <div class="relative w-16 h-16">
              <div class="absolute inset-0 rounded-full bg-red-500/20 animate-ping"></div>
              <button @click="stopVoiceRecording"
                class="relative w-16 h-16 rounded-full bg-red-600 hover:bg-red-700 flex items-center justify-center transition-colors shadow-lg">
                <svg class="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 24 24">
                  <rect x="6" y="6" width="12" height="12" rx="1" />
                </svg>
              </button>
            </div>
            <p class="text-sm text-red-400 font-medium">Recording… click to stop</p>
          </div>

          <!-- Uploading / transcribing indicator -->
          <div v-if="voice.uploading.value" class="flex flex-col items-center gap-2">
            <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
            <p class="text-sm text-gray-400">Transcribing…</p>
          </div>
        </div>

        <!-- Transcription result -->
        <div v-if="voice.transcription.value || voiceRecordingDone" class="space-y-3 mb-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Transcription (editable)</label>
            <textarea v-model="voice.transcription.value" rows="4" placeholder="Transcription will appear here…"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
          <!-- Transcription warning (no model configured, no speech detected, or error from backend) -->
          <p v-if="voice.transcriptionWarning.value && !voice.transcription.value"
            class="text-xs text-amber-400">
            {{ voice.transcriptionWarning.value }}
          </p>
        </div>

        <!-- Error -->
        <p v-if="voice.error.value" class="text-sm text-red-400 mb-4">{{ voice.error.value }}</p>

        <!-- Actions -->
        <div class="flex gap-3">
          <button v-if="voiceRecordingDone && !voice.uploading.value" @click="submitVoiceCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="closeVoiceModal"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ProjectMetricSnapshot, Milestone, GitCommit, Issue } from '~/types'
import { AgentSessionStatus, CiCdRunStatus, IssueStatus, IssuePriority, IssueType } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { useIssuesStore } from '~/stores/issues'
import { useKanbanStore } from '~/stores/kanban'
import { formatIssueId } from '~/composables/useIssueFormat'
import { useDashboardLayout } from '~/composables/useDashboardLayout'

const route = useRoute()
const id = route.params.id as string
const store = useProjectsStore()
const runsStore = useCiCdRunsStore()
const issuesStore = useIssuesStore()
const kanbanStore = useKanbanStore()
const api = useApi()

// --- Issue creation ---
const showCreate = ref(false)
const form = reactive({
  title: '',
  body: '',
  status: IssueStatus.Todo,
  priority: IssuePriority.Medium,
  type: IssueType.Issue,
})

const statuses = [
  { value: IssueStatus.Backlog, label: 'Backlog' },
  { value: IssueStatus.Todo, label: 'Todo' },
  { value: IssueStatus.InProgress, label: 'In Progress' },
  { value: IssueStatus.InReview, label: 'In Review' },
  { value: IssueStatus.Done, label: 'Done' },
  { value: IssueStatus.Cancelled, label: 'Cancelled' },
]

const { priorities } = usePriority()

const types = [
  { value: IssueType.Issue, label: '📋 Issue' },
  { value: IssueType.Bug, label: '🐛 Bug' },
  { value: IssueType.Feature, label: '✨ Feature' },
  { value: IssueType.Task, label: '✅ Task' },
  { value: IssueType.Epic, label: '⚡ Epic' },
]

async function submitCreate() {
  if (!form.title) return
  const newIssue = await issuesStore.createIssue(id, form)
  showCreate.value = false
  Object.assign(form, { title: '', body: '', status: IssueStatus.Todo, priority: IssuePriority.Medium, type: IssueType.Issue })
  if (newIssue) {
    await navigateTo(`/projects/${id}/issues/${newIssue.number}`)
  }
}

// --- Voice creation ---
const showVoiceCreate = ref(false)
const voiceRecordingDone = ref(false)
const voiceDragOver = ref(false)
const modalDragOver = ref(false)

const voice = useVoiceRecorder()

async function startVoiceRecording() {
  voiceRecordingDone.value = false
  await voice.startRecording()
}

async function uploadVoiceFile(file: File | Blob) {
  voiceRecordingDone.value = true
  try {
    await voice.uploadRecording(file)
  } catch {
    // voice.error is already set by uploadRecording; the modal stays open so the user sees it
  }
}

async function handleVoiceFileDrop(e: DragEvent) {
  voiceDragOver.value = false
  const file = e.dataTransfer?.files[0]
  if (!file) return
  if (!file.type.startsWith('audio/') && !file.name.toLowerCase().endsWith('.wav')) return
  showVoiceCreate.value = true
  await uploadVoiceFile(file)
}

async function handleModalVoiceFileDrop(e: DragEvent) {
  modalDragOver.value = false
  const file = e.dataTransfer?.files[0]
  if (!file) return
  if (!file.type.startsWith('audio/') && !file.name.toLowerCase().endsWith('.wav')) return
  await uploadVoiceFile(file)
}

async function stopVoiceRecording() {
  const wavBlob = voice.stopRecording()
  if (wavBlob) {
    await uploadVoiceFile(wavBlob)
  } else {
    voiceRecordingDone.value = true
  }
}

async function submitVoiceCreate() {
  const title = `Voice Issue - ${new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })}`
  const newIssue = await issuesStore.createIssue(id, {
    title,
    body: voice.transcription.value,
    status: IssueStatus.Todo,
    priority: IssuePriority.Medium,
    type: IssueType.Issue,
  })
  if (newIssue && voice.lastWavBlob.value) {
    try {
      const audioFile = new File([voice.lastWavBlob.value], 'recording.wav', { type: 'audio/wav' })
      await issuesStore.addAttachment(newIssue.id, audioFile, true, false)
    } catch (e) {
      console.warn('Could not attach voice file to new issue', e)
    }
  }
  closeVoiceModal()
  if (newIssue) {
    await navigateTo(`/projects/${id}/issues/${newIssue.number}`)
  }
}

function closeVoiceModal() {
  voice.reset()
  voiceRecordingDone.value = false
  showVoiceCreate.value = false
}

const metricSnapshots = ref<ProjectMetricSnapshot[]>([])
const milestones = ref<Milestone[]>([])
const mcpCount = ref(0)
const commitCount = ref<number | null>(null)
const hasMoreCommits = ref(false)
const recentProjectIssues = ref<Issue[]>([])

// ── Dashboard layout customization ─────────────────────────────────────────
type SectionId = 'statIssues' | 'statCommits' | 'statMRs' | 'milestones' | 'issues' | 'agentRuns' | 'cicdRuns' | 'history' | 'kanban'
type SectionDisplayMode = 'list' | 'count' | 'block'
type SectionWidth = 'xxs' | 'xs' | 'quarter' | 'sm' | 'md' | 'lg'

const SECTION_LABELS: Record<SectionId, string> = {
  statIssues: 'Issues',
  statCommits: 'Commits',
  statMRs: 'Open MRs',
  milestones: 'Milestones',
  issues: 'Recent Issues',
  agentRuns: 'Recent Agent Runs',
  cicdRuns: 'Recent CI/CD Runs',
  history: 'Issue History',
  kanban: 'Kanban Board',
}
const SECTION_DISPLAY_MODES: Partial<Record<SectionId, SectionDisplayMode[]>> = {
  milestones: ['block', 'list', 'count'],
  issues: ['list', 'count'],
  agentRuns: ['list', 'count'],
  cicdRuns: ['list', 'count'],
}
const SECTION_HAS_MAX_ITEMS: Set<SectionId> = new Set(['milestones', 'issues', 'agentRuns', 'cicdRuns'])
const SECTION_CAN_STACK: Set<SectionId> = new Set(['statIssues', 'statCommits', 'statMRs', 'milestones', 'issues', 'agentRuns', 'cicdRuns', 'history', 'kanban'])
const MAX_ITEMS_OPTIONS = [3, 5, 8, 10]
const WIDTH_LABELS: Record<SectionWidth, string> = { xxs: '1/12', xs: '1/6', quarter: '1/4', sm: '1/3', md: '1/2', lg: 'Full' }
const PROJECT_WIDTHS = (['xxs', 'xs', 'quarter', 'sm', 'md', 'lg'] as SectionWidth[]).map(v => ({ value: v, label: WIDTH_LABELS[v] }))

const DEFAULT_CONFIGS = {
  statIssues:  { hidden: false, displayMode: 'list',  maxItems: 3,  width: 'xs',  tabGroup: null, stackGroup: null },
  statCommits: { hidden: false, displayMode: 'list',  maxItems: 3,  width: 'xs',  tabGroup: null, stackGroup: null },
  statMRs:     { hidden: false, displayMode: 'list',  maxItems: 3,  width: 'xs',  tabGroup: null, stackGroup: null },
  milestones:  { hidden: false, displayMode: 'block', maxItems: 3,  width: 'lg',  tabGroup: null, stackGroup: null },
  issues:      { hidden: false, displayMode: 'list',  maxItems: 5,  width: 'sm',  tabGroup: null, stackGroup: null },
  agentRuns:   { hidden: false, displayMode: 'list',  maxItems: 5,  width: 'sm',  tabGroup: null, stackGroup: null },
  cicdRuns:    { hidden: false, displayMode: 'list',  maxItems: 5,  width: 'sm',  tabGroup: null, stackGroup: null },
  history:     { hidden: false, displayMode: 'list',  maxItems: 5,  width: 'md',  tabGroup: null, stackGroup: null },
  kanban:      { hidden: false, displayMode: 'list',  maxItems: 5,  width: 'md',  tabGroup: null, stackGroup: null },
}
const DEFAULT_ORDER: string[] = ['statIssues', 'statCommits', 'statMRs', 'milestones', 'rowbreak-after-milestones', 'issues', 'agentRuns', 'cicdRuns', 'history', 'kanban']
const DRAFT_LAYOUT_KEY = `project-dashboard-layout-v7-${id}`

const {
  layout,
  isDraftMode,
  dragSectionId,
  dragHoverSid,
  renderedItems,
  sectionCfg: sectionCfgRaw,
  updateCfg: updateCfgRaw,
  hideSection: hideSectionRaw,
  showSection: showSectionRaw,
  loadLayout,
  enterDraftMode,
  saveDraftMode,
  cancelDraftMode,
  resetLayout,
  addRowBreak,
  removeRowBreak,
  captureSnapshot,
  onDragStart: onDragStartRaw,
  onDragOver: onDragOverRaw,
  onDragEnter: onDragEnterRaw,
  onDragEnd: onDragEndRaw,
  onGapDragEnter: onGapDragEnterRaw,
  onGapDragLeave,
  toggleTabGroupWithNext: toggleTabGroupWithNextRaw,
  tabWithSection,
  toggleStackGroupWithNext: toggleStackGroupWithNextRaw,
  stackWithSection,
  getActiveTab,
  setActiveTab,
} = useDashboardLayout({
  defaultOrder: DEFAULT_ORDER,
  defaultConfigs: DEFAULT_CONFIGS,
  storageKey: DRAFT_LAYOUT_KEY,
  filterVisible: (s) => {
    if (s === 'milestones') return openMilestones.value.length > 0
    return true
  },
})

function sectionCfg(s: SectionId) { return sectionCfgRaw(s) }
function updateCfg(s: SectionId, patch: object) { updateCfgRaw(s, patch) }
function hideSection(s: SectionId) { hideSectionRaw(s) }
function showSection(s: SectionId) { showSectionRaw(s) }
function onSectionDragStart(e: DragEvent, id: string) { onDragStartRaw(e, id) }
function onSectionDragOver(e: DragEvent, id: string) { onDragOverRaw(e, id) }
function onSectionDragEnter(e: DragEvent, id: string) { onDragEnterRaw(e, id) }
function onSectionDragEnd(e: DragEvent) { onDragEndRaw(e) }
function onSectionGapDragEnter(e: DragEvent, id: string, after: boolean) { onGapDragEnterRaw(e, id, after) }
function toggleTabGroupWithNext(sid: SectionId) { toggleTabGroupWithNextRaw(sid) }
function toggleStackGroupWithNext(sid: SectionId) { toggleStackGroupWithNextRaw(sid) }

// Column span class based on width (12-col grid)
function colSpanClass(width: SectionWidth): string {
  if (width === 'xxs')     return 'col-span-12 sm:col-span-6 lg:col-span-1'
  if (width === 'xs')      return 'col-span-12 sm:col-span-6 lg:col-span-2'
  if (width === 'quarter') return 'col-span-12 sm:col-span-6 lg:col-span-3'
  if (width === 'sm')      return 'col-span-12 md:col-span-6 lg:col-span-4'
  if (width === 'md')      return 'col-span-12 lg:col-span-6'
  return 'col-span-12'
}
function itemColSpanClass(item: { type: string; sid?: string; sections?: string[] }): string {
  if (item.type === 'rowbreak') return 'col-span-12'
  const firstSid = item.type === 'section' ? item.sid! : item.sections![0]
  return colSpanClass(sectionCfg(firstSid as SectionId).width as SectionWidth)
}

const commitCountLabel = computed(() => {
  if (commitCount.value === null) return '—'
  return hasMoreCommits.value ? `${commitCount.value}+` : String(commitCount.value)
})

const { connection, isConnected, connect } = useSignalR('/hubs/project')

const openMilestones = computed(() =>
  milestones.value.filter(m => m.status === 'open').slice(0, sectionCfg('milestones').maxItems)
)

const recentIssues = computed(() =>
  recentProjectIssues.value
    .slice()
    .sort((a, b) => (b.updatedAt ?? b.createdAt).localeCompare(a.updatedAt ?? a.createdAt))
    .slice(0, sectionCfg('issues').maxItems)
)

const visibleAgentSessions = computed(() => {
  const max = sectionCfg('agentRuns').maxItems
  const sessions = runsStore.agentSessions
  const hasRed = sessions.some(s => s.status === AgentSessionStatus.Failed || s.status === AgentSessionStatus.Cancelled)
  if (hasRed) {
    const seen = new Set<string>()
    const result = []
    for (const s of sessions) {
      const key = s.gitBranch || s.id
      if (!seen.has(key)) {
        seen.add(key)
        result.push(s)
        if (result.length >= max) break
      }
    }
    return result
  }
  return sessions.slice(0, max)
})

const visibleCiCdRuns = computed(() => {
  const max = sectionCfg('cicdRuns').maxItems
  const runs = runsStore.runs
  const hasRed = runs.some(r => r.status === CiCdRunStatus.Failed || r.status === CiCdRunStatus.Cancelled)
  if (hasRed) {
    const seen = new Set<string>()
    const result = []
    for (const r of runs) {
      const key = r.branch || r.id
      if (!seen.has(key)) {
        seen.add(key)
        result.push(r)
        if (result.length >= max) break
      }
    }
    return result
  }
  return runs.slice(0, max)
})

onMounted(async () => {
  // Restore layout preference
  if (import.meta.client) {
    loadLayout()
  }

  await store.fetchProject(id)
  await Promise.all([
    runsStore.fetchRuns(id),
    runsStore.fetchAgentSessions(id),
    api.get<Milestone[]>(`/api/projects/${id}/milestones`)
      .then(data => { milestones.value = data })
      .catch((e) => { console.warn(`Failed to load milestones for project ${id}`, e) }),
    api.get<{ mcpServerId: string }[]>(`/api/projects/${id}/mcp-servers`)
      .then(data => { mcpCount.value = data.length })
      .catch((e) => { console.warn(`Failed to load MCP servers for project ${id}`, e) }),
    api.get<ProjectMetricSnapshot[]>(`/api/dashboard/projects/${id}/metric-history`)
      .then(data => { metricSnapshots.value = data })
      .catch((e) => { console.error(`Failed to load metric history for project ${id}`, e) }),
    api.get<Issue[]>(`/api/issues?projectId=${id}`)
      .then(data => { recentProjectIssues.value = data })
      .catch((e) => { console.warn(`Failed to load recent issues for project ${id}`, e) }),
    api.get<GitCommit[]>(`/api/projects/${id}/git/commits?take=50`)
      .then((data) => {
        commitCount.value = data.length
        hasMoreCommits.value = data.length === 50
      })
      .catch(() => { commitCount.value = null }),
    kanbanStore.fetchBoards(id).catch((e) => { console.warn('Failed to load kanban boards:', e) }),
  ])

  // Connect to SignalR for live run updates
  await connect()
  if (connection.value) {
    await connection.value.invoke('JoinProject', id).catch(() => {})
    connection.value.on('RunsUpdated', async () => {
      await Promise.all([
        runsStore.fetchRuns(id),
        runsStore.fetchAgentSessions(id),
      ])
    })
  }
})

function formatDate(d: string) {
  return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function relativeTime(d: string) {
  const ms = Date.now() - new Date(d).getTime()
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s ago`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

function issueStatusDot(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400',
  }
  return map[status] ?? 'bg-gray-500'
}

function issuePriorityBadge(priority: IssuePriority) {
  const map: Record<IssuePriority, string> = {
    [IssuePriority.Urgent]: 'bg-red-900/60 text-red-300',
    [IssuePriority.High]: 'bg-orange-900/60 text-orange-300',
    [IssuePriority.Medium]: 'bg-yellow-900/60 text-yellow-300',
    [IssuePriority.Low]: 'bg-blue-900/60 text-blue-300',
    [IssuePriority.NoPriority]: 'bg-gray-800 text-gray-500',
  }
  return map[priority] ?? 'bg-gray-800 text-gray-500'
}

// Chart helpers
const chartWidth = 600
const chartPad = 36

const CHART_DAY_DEFAULT = 1  // 1 = last 1 day of snapshots
const CHART_HEIGHT_OPTIONS = [
  { value: 'xs', label: 'XS' }, { value: 'sm', label: 'S' }, { value: 'md', label: 'M' },
  { value: 'lg', label: 'L' }, { value: 'xl', label: 'XL' },
]
const CHART_HEIGHT_PX: Record<string, number> = { xs: 80, sm: 120, md: 180, lg: 260, xl: 360 }

const chartHeightPx = computed(() =>
  CHART_HEIGHT_PX[sectionCfg('history').chartHeightKey ?? 'md'] ?? 160,
)

const filteredMetricSnapshots = computed(() => {
  const days = sectionCfg('history').chartDays ?? CHART_DAY_DEFAULT
  if (!metricSnapshots.value.length) return metricSnapshots.value
  const cutoff = new Date()
  cutoff.setDate(cutoff.getDate() - days)
  return metricSnapshots.value.filter(s => new Date(s.recordedAt) >= cutoff)
})

const chartMaxY = computed(() => {
  const max = Math.max(
    ...filteredMetricSnapshots.value.flatMap(s => [
      s.openIssues, s.inProgressIssues, s.doneIssues, s.totalAgentRuns, s.totalCiCdRuns,
    ]),
    1,
  )
  return Math.ceil(max / 5) * 5 || 5
})

const gridYValues = computed(() => {
  const step = Math.ceil(chartMaxY.value / 4)
  return [0, step, step * 2, step * 3, step * 4].filter(v => v <= chartMaxY.value + step)
})

function yScale(val: number) {
  const plotH = chartHeightPx.value - 30
  return plotH - (val / chartMaxY.value) * plotH + 5
}

function xPos(i: number) {
  const n = filteredMetricSnapshots.value.length
  const plotW = chartWidth - chartPad * 2
  return chartPad + (n > 1 ? (i / (n - 1)) * plotW : plotW / 2)
}

function linePoints(key: keyof ProjectMetricSnapshot) {
  return filteredMetricSnapshots.value.map((s, i) => `${xPos(i)},${yScale(s[key] as number)}`).join(' ')
}

function shortTime(d: string) {
  const dt = new Date(d)
  return `${dt.getHours().toString().padStart(2, '0')}:00`
}
</script>

