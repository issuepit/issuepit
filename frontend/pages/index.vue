<template>
  <div class="p-8">
    <!-- Header -->
    <div class="mb-6 flex items-start justify-between gap-3">
      <div>
        <PageBreadcrumb :items="[
          { label: 'Dashboard', to: '/', icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6' },
        ]" class="mb-1" />
        <p class="text-gray-400 text-sm">Welcome back — here's what's happening.</p>
      </div>
    </div>

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
      <div class="flex items-center gap-1.5 flex-wrap justify-end">
        <button
          draggable="true"
          @click="addRowBreak()"
          @dragstart.stop="(e: DragEvent) => { captureSnapshot(); const id = addRowBreak(); onDragStart(e, id) }"
          aria-label="Add row break to dashboard layout"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1 cursor-grab">
          <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Row break
        </button>
        <button @click="showLoadModal = true"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          Load
        </button>
        <button @click="handleExportJson"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l4 4m0 0l4-4m-4 4V4" />
          </svg>
          Export
        </button>
        <button @click="resetLayout"
          class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
          Reset
        </button>
        <button @click="cancelDraftMode"
          class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors">
          Cancel
        </button>
        <button @click="showSaveModal = true"
          class="text-xs text-gray-400 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
          </svg>
          Save as…
        </button>
        <button @click="saveDraftMode"
          class="text-xs bg-amber-600 hover:bg-amber-700 text-white px-3 py-1.5 rounded-lg transition-colors font-medium">
          Save
        </button>
      </div>
    </div>

    <!-- Import error -->
    <div v-if="importError" class="mb-3 flex items-center gap-2 bg-red-900/30 border border-red-700/40 rounded-lg px-4 py-2">
      <svg class="w-3.5 h-3.5 shrink-0 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
      </svg>
      <p class="text-xs text-red-400 flex-1">{{ importError }}</p>
      <button @click="importError = null" class="text-red-500 hover:text-red-300 text-xs">✕</button>
    </div>

    <!-- Restore hidden sections (draft mode) -->
    <div v-if="isDraftMode && hiddenSections.size > 0" class="mb-4 flex flex-wrap items-center gap-2">
      <span class="text-xs text-gray-600">Hidden:</span>
      <button v-for="sid in hiddenSections" :key="sid" @click="showSection(sid as MainSectionId)"
        class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-gray-200 px-2.5 py-1 rounded-lg transition-colors flex items-center gap-1">
        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
        </svg>
        {{ SECTION_LABELS[sid as MainSectionId] }}
      </button>
    </div>

    <!-- 12-column grid layout -->
    <div class="grid grid-cols-12 gap-4 items-start">
      <template v-for="item in renderedItems" :key="item.type === 'section' || item.type === 'rowbreak' ? item.sid : item.key">
        <!-- Row break: forces a new grid row; shows separator handle in draft mode -->
        <template v-if="item.type === 'rowbreak'">
          <div v-if="isDraftMode" class="col-span-12 flex items-center gap-2 py-1 cursor-grab"
            draggable="true"
            @dragstart.stop="(e: DragEvent) => onDragStart(e, item.sid)"
            @dragover.prevent="onDragOver($event, item.sid)"
            @dragenter="onDragEnter($event, item.sid)"
            @dragend="onDragEnd($event)">
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
            item.type === 'section' && isDraftMode && sectionCfg(item.sid as MainSectionId).hidden ? 'opacity-40 saturate-50' : '',
            (item.type === 'section' ? dragSectionId === item.sid : (item.sections ?? []).includes(dragSectionId as MainSectionId)) && isDraftMode
              ? 'opacity-50'
              : '',
          ]"
          :draggable="isDraftMode"
          @dragstart="isDraftMode && (item.type === 'section' || item.type === 'stackgroup') ? onDragStart($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
          @dragover.prevent="isDraftMode ? onDragOver($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
          @dragenter="isDraftMode ? onDragEnter($event, item.type === 'section' ? item.sid : item.sections[0]) : undefined"
          @dragend="isDraftMode ? onDragEnd($event) : undefined">

          <!-- Gap zone sentinels: invisible divs in the CSS gap on each side of the card.
               Only rendered when dragging; the card itself does NOT trigger reorder — only these do. -->
          <template v-if="isDraftMode && dragSectionId !== null && !((item.type === 'section' ? dragSectionId === item.sid : (item.sections ?? []).includes(dragSectionId as MainSectionId)))">
            <!-- Left gap sentinel → insert BEFORE this item -->
            <div
              class="absolute inset-y-0 -left-2 w-2 z-30 pointer-events-auto"
              @dragover.prevent
              @dragenter="onGapDragEnter($event, item.type === 'section' ? item.sid : item.sections[0], false)"
              @dragleave="onGapDragLeave()"
            />
            <!-- Right gap sentinel → insert AFTER this item -->
            <div
              class="absolute inset-y-0 -right-2 w-2 z-30 pointer-events-auto"
              @dragover.prevent
              @dragenter="onGapDragEnter($event, item.type === 'section' ? item.sid : item.sections[0], true)"
              @dragleave="onGapDragLeave()"
            />
          </template>

          <!-- Draft mode config bar -->
          <template v-if="isDraftMode">
            <!-- Single section config bar -->
            <DashboardSectionBar
              v-if="item.type === 'section'"
              :label="SECTION_LABELS[item.sid as MainSectionId]"
              :display-modes="SECTION_DISPLAY_MODES[item.sid as MainSectionId]"
              :current-display-mode="sectionCfg(item.sid as MainSectionId).displayMode"
              :has-max-items="SECTION_HAS_MAX_ITEMS.has(item.sid as MainSectionId)"
              :max-items-options="[3,5,8,10]"
              :current-max-items="sectionCfg(item.sid as MainSectionId).maxItems"
              :widths="MAIN_WIDTHS"
              :current-width="sectionCfg(item.sid as MainSectionId).width"
              :current-chart-days="item.sid === 'chart' ? (sectionCfg(item.sid as MainSectionId).chartDays ?? CHART_DAY_DEFAULT) : undefined"
              :chart-height-options="item.sid === 'chart' ? CHART_HEIGHT_OPTIONS : undefined"
              :current-chart-height="item.sid === 'chart' ? (sectionCfg(item.sid as MainSectionId).chartHeightKey ?? 'md') : undefined"
              :can-tab="SECTION_CAN_TAB.has(item.sid as MainSectionId) && layout.order.indexOf(item.sid) < layout.order.length - 1"
              :is-tabbed="sectionCfg(item.sid as MainSectionId).tabGroup !== null"
              :can-stack="SECTION_CAN_STACK.has(item.sid as MainSectionId) && layout.order.indexOf(item.sid) < layout.order.length - 1"
              :is-stacked="sectionCfg(item.sid as MainSectionId).stackGroup !== null"
              :hidden="sectionCfg(item.sid as MainSectionId).hidden"
              :drag-hover="dragSectionId !== null && dragHoverSid === item.sid && dragSectionId !== item.sid"
              @display-mode-change="m => updateCfg(item.sid as MainSectionId, { displayMode: m as MainDisplayMode })"
              @max-items-change="n => updateCfg(item.sid as MainSectionId, { maxItems: n })"
              @width-change="w => updateCfg(item.sid as MainSectionId, { width: w as MainWidth })"
              @chart-days-change="d => updateCfg(item.sid as MainSectionId, { chartDays: d })"
              @chart-height-change="k => updateCfg(item.sid as MainSectionId, { chartHeightKey: k })"
              @tab-toggle="toggleTabGroupWithNext(item.sid as MainSectionId)"
              @tab-drop="droppedSid => tabWithSection(item.sid, droppedSid)"
              @stack-toggle="toggleStackGroupWithNext(item.sid as MainSectionId)"
              @stack-drop="droppedSid => stackWithSection(item.sid, droppedSid)"
              @hide="hideSection(item.sid as MainSectionId)"
              @show="showSection(item.sid as MainSectionId)"
            />
            <!-- Tab group config bar -->
            <DashboardTabGroupBar
              v-else-if="item.type === 'tabgroup'"
              :sections="item.sections"
              :section-labels="SECTION_LABELS"
              :widths="MAIN_WIDTHS"
              :current-width="sectionCfg(item.sections[0] as MainSectionId).width"
              @split="toggleTabGroupWithNext(item.sections[0] as MainSectionId)"
              @width-change="w => updateCfg(item.sections[0] as MainSectionId, { width: w as MainWidth })"
            />
            <!-- Stack group config bar -->
            <DashboardStackGroupBar
              v-else-if="item.type === 'stackgroup'"
              :sections="item.sections"
              :section-labels="SECTION_LABELS"
              :widths="MAIN_WIDTHS"
              :current-width="sectionCfg(item.sections[0] as MainSectionId).width"
              :is-dragging="!!dragSectionId"
              @split="toggleStackGroupWithNext(item.sections[0] as MainSectionId)"
              @width-change="w => updateCfg(item.sections[0] as MainSectionId, { width: w as MainWidth })"
              @stack-drop="droppedSid => stackWithSection(item.sections[0] as MainSectionId, droppedSid)"
            />
          </template>

          <!-- Content area -->
          <div :class="[
            isDraftMode && item.type === 'section' && sectionCfg(item.sid as MainSectionId).hidden ? 'opacity-30 saturate-0 pointer-events-none' : '',
            item.type === 'stackgroup' ? 'flex flex-col gap-2' : '',
          ]">

            <!-- Tab nav (for tabgroup) -->
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
                {{ SECTION_LABELS[sec as MainSectionId] }}
              </button>
            </div>

            <!-- Section(s) content -->
            <template v-for="sid in (item.type === 'tabgroup' || item.type === 'stackgroup' ? item.sections : [item.sid])" :key="sid">
              <div v-show="item.type !== 'tabgroup' || getActiveTab(item.key, item.sections) === sid"
                :class="item.type === 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-b-xl p-5' : ''">

                <!-- ── statProjects ── -->
                <template v-if="sid === 'statProjects'">
                  <NuxtLink to="/projects"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">Projects</p>
                    <p class="text-3xl font-bold text-blue-400 mt-1">{{ stats.projects }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statOpenIssues ── -->
                <template v-else-if="sid === 'statOpenIssues'">
                  <NuxtLink to="/issues?status=open"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">Open Issues</p>
                    <p class="text-3xl font-bold text-amber-400 mt-1">{{ stats.openIssues }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statInProgress ── -->
                <template v-else-if="sid === 'statInProgress'">
                  <NuxtLink to="/issues?status=in_progress"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">In Progress</p>
                    <p class="text-3xl font-bold text-indigo-400 mt-1">{{ stats.inProgress }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statAgentRuns ── -->
                <template v-else-if="sid === 'statAgentRuns'">
                  <NuxtLink to="/runs"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">Agent Runs</p>
                    <p class="text-3xl font-bold text-green-400 mt-1">{{ stats.agentRuns }}</p>
                  </NuxtLink>
                </template>

                <!-- ── recentIssues ── -->
                <template v-else-if="sid === 'recentIssues'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Recent Issues</h2>
                      <NuxtLink to="/issues" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('recentIssues').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ stats.openIssues }}
                        <p class="text-sm text-gray-400 font-normal mt-1">open issues</p>
                      </div>
                    </template>
                    <template v-else>
                      <div class="space-y-1">
                        <NuxtLink v-for="issue in recentIssues" :key="issue.id"
                          :to="`/projects/${issue.projectId}/issues/${issue.number}`"
                          class="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-800 transition-colors block">
                          <span :class="statusDot(issue.status)" class="w-2 h-2 rounded-full shrink-0"></span>
                          <div class="flex-1 min-w-0">
                            <p class="text-sm text-gray-200 truncate">{{ issue.title }}</p>
                            <p class="text-xs text-gray-500">{{ issue.projectName }}</p>
                          </div>
                          <span :class="priorityBadge(issue.priority)" class="text-xs px-1.5 py-0.5 rounded font-medium shrink-0">
                            {{ issue.priority }}
                          </span>
                        </NuxtLink>
                        <p v-if="recentIssues.length === 0" class="text-sm text-gray-500 py-4 text-center">No recent issues</p>
                      </div>
                    </template>
                  </div>
                </template>

                <!-- ── recentProjects ── -->
                <template v-else-if="sid === 'recentProjects'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Projects</h2>
                      <NuxtLink to="/projects" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('recentProjects').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ stats.projects }}
                        <p class="text-sm text-gray-400 font-normal mt-1">projects</p>
                      </div>
                    </template>
                    <template v-else>
                      <div class="space-y-1">
                        <NuxtLink v-for="project in projectsStore.projects.slice(0, sectionCfg('recentProjects').maxItems)" :key="project.id"
                          :to="`/projects/${project.id}`"
                          class="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-800 transition-colors block">
                          <div :style="{ background: project.color || '#4c6ef5' }"
                            class="w-7 h-7 rounded-md flex items-center justify-center text-white text-xs font-bold shrink-0">
                            {{ project.name.charAt(0).toUpperCase() }}
                          </div>
                          <div class="flex-1 min-w-0">
                            <p class="text-sm text-gray-200 truncate">{{ project.name }}</p>
                            <p class="text-xs text-gray-500">{{ project.issueCount }} issues</p>
                          </div>
                          <svg class="w-4 h-4 text-gray-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                          </svg>
                        </NuxtLink>
                        <p v-if="projectsStore.projects.length === 0" class="text-sm text-gray-500 py-4 text-center">No projects yet</p>
                      </div>
                    </template>
                  </div>
                </template>

                <!-- ── chart ── -->
                <template v-else-if="sid === 'chart'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <h2 class="font-semibold text-white mb-4">Issue Activity (last {{ sectionCfg('chart').chartDays ?? CHART_DAY_DEFAULT }} days)</h2>
                    <div v-if="filteredIssueHistory.length" class="overflow-x-auto">
                      <svg :viewBox="`0 0 ${chartWidth} ${chartHeightPx}`" class="w-full" style="min-width:500px">
                        <line v-for="y in gridYValues" :key="y"
                          :x1="chartPad" :y1="yScale(y)" :x2="chartWidth - chartPad" :y2="yScale(y)"
                          stroke="#374151" stroke-width="1" />
                        <text v-for="y in gridYValues" :key="`yl-${y}`"
                          :x="chartPad - 6" :y="yScale(y) + 4"
                          text-anchor="end" fill="#6b7280" font-size="10">{{ y }}</text>
                        <polyline :points="linePoints('open')" fill="none" stroke="#f59e0b" stroke-width="2"
                          stroke-linejoin="round" />
                        <polyline :points="linePoints('inProgress')" fill="none" stroke="#6366f1" stroke-width="2"
                          stroke-linejoin="round" />
                        <polyline :points="linePoints('done')" fill="none" stroke="#22c55e" stroke-width="2"
                          stroke-linejoin="round" />
                        <text v-for="(entry, i) in filteredIssueHistory" :key="`xl-${i}`"
                          :x="xPos(i)" :y="chartHeightPx - 4"
                          text-anchor="middle" fill="#6b7280" font-size="9">{{ shortDate(entry.date) }}</text>
                      </svg>
                    </div>
                    <div v-else class="py-8 text-center text-sm text-gray-500">No activity data yet</div>
                    <div class="flex items-center gap-5 mt-3">
                      <span class="flex items-center gap-1.5 text-xs text-gray-400">
                        <span class="w-3 h-0.5 bg-amber-400 rounded-full inline-block"></span> Open
                      </span>
                      <span class="flex items-center gap-1.5 text-xs text-gray-400">
                        <span class="w-3 h-0.5 bg-indigo-400 rounded-full inline-block"></span> In Progress
                      </span>
                      <span class="flex items-center gap-1.5 text-xs text-gray-400">
                        <span class="w-3 h-0.5 bg-green-400 rounded-full inline-block"></span> Done
                      </span>
                    </div>
                  </div>
                </template>

                <!-- ── cicdRuns ── -->
                <template v-else-if="sid === 'cicdRuns'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">CI/CD Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('cicdRuns').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.runs.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">CI/CD runs</p>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="cicdRunsItems.length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="run in cicdRunsItems" :key="run.id"
                              class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                              @click="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
                              <td class="px-3 py-2"><CiCdStatusChip :runs="[run]" /></td>
                              <td class="px-3 py-2 text-gray-300 text-xs truncate max-w-[8rem]">{{ run.workflow || '—' }}</td>
                              <td class="px-3 py-2 text-gray-300 font-mono text-xs hidden md:table-cell">{{ run.branch || '—' }}</td>
                              <td class="px-3 py-2 text-gray-400 text-xs"><DateDisplay :date="run.startedAt" mode="auto" /></td>
                            </tr>
                          </tbody>
                        </table>
                      </div>
                      <p v-else class="text-sm text-gray-500 py-6 text-center">No CI/CD runs yet</p>
                    </template>
                  </div>
                </template>

                <!-- ── agentRunsList ── -->
                <template v-else-if="sid === 'agentRunsList'">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Agent Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg('agentRunsList').displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.dashboardSessions.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">agent runs</p>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="agentRunsItems.length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="session in agentRunsItems" :key="session.id"
                              class="hover:bg-gray-800/40 transition-colors cursor-pointer"
                              @click="navigateTo(`/projects/${session.projectId}/runs/agent-sessions/${session.id}`)">
                              <td class="px-3 py-2"><AgentSessionStatusChip :session="session" /></td>
                              <td class="px-3 py-2 text-xs">
                                <NuxtLink :to="`/projects/${session.projectId}/issues/${session.issueNumber}`"
                                  class="text-brand-400 hover:text-brand-300 transition-colors" @click.stop>
                                  #{{ formatIssueId(session.issueNumber, projectsStore.projects.find(p => p.id === session.projectId)) }} {{ session.issueTitle }}
                                </NuxtLink>
                              </td>
                              <td class="px-3 py-2 text-gray-400 text-xs hidden md:table-cell">{{ session.projectName }}</td>
                              <td class="px-3 py-2 text-gray-400 text-xs"><DateDisplay :date="session.startedAt" mode="auto" /></td>
                            </tr>
                          </tbody>
                        </table>
                      </div>
                      <p v-else class="text-sm text-gray-500 py-6 text-center">No agent runs yet</p>
                    </template>
                  </div>
                </template>

              </div>
            </template>
          </div>
        </div>
      </template>
    </div>

    <!-- Customize button -->
    <div v-if="!isDraftMode" class="flex justify-center mt-2 mb-4">
      <button @click="enterDraftMode"
        class="flex items-center gap-1.5 text-xs text-gray-600 hover:text-gray-400 transition-colors px-3 py-1.5 rounded-lg hover:bg-gray-800/50">
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M4 6h16M4 10h16M4 14h16M4 18h16" />
        </svg>
        Customize dashboard
      </button>
    </div>

    <!-- Save as modal -->
    <DashboardSaveModal
      v-if="showSaveModal"
      :layout-json="exportLayoutJson()"
      dashboard-type="main"
      @close="showSaveModal = false"
      @saved="showSaveModal = false"
    />

    <!-- Load / import modal -->
    <DashboardLoadModal
      v-if="showLoadModal"
      dashboard-type="main"
      @close="showLoadModal = false"
      @apply="applyImportedLayout"
    />
  </div>
</template>

<script setup lang="ts">
import { IssueStatus, IssuePriority, type IssueHistoryEntry } from '~/types'
import { useProjectsStore } from '~/stores/projects'
import { useIssuesStore } from '~/stores/issues'
import { useCiCdRunsStore } from '~/stores/cicdRuns'
import { formatIssueId } from '~/composables/useIssueFormat'
import { useDashboardLayout } from '~/composables/useDashboardLayout'

const projectsStore = useProjectsStore()
const issuesStore = useIssuesStore()
const runsStore = useCiCdRunsStore()

const api = useApi()
const issueHistory = ref<IssueHistoryEntry[]>([])

// ── Layout / Draft mode ───────────────────────────────────────────────────
type MainSectionId =
  | 'statProjects' | 'statOpenIssues' | 'statInProgress' | 'statAgentRuns'
  | 'recentIssues' | 'recentProjects' | 'chart' | 'cicdRuns' | 'agentRunsList'

type MainWidth = 'xxs' | 'xs' | 'quarter' | 'sm' | 'md' | 'lg'
type MainDisplayMode = 'list' | 'count'

const MAIN_LAYOUT_KEY = 'main-dashboard-layout-v6'

const DEFAULT_ORDER: MainSectionId[] = [
  'statProjects', 'statOpenIssues', 'statInProgress', 'statAgentRuns',
  'recentIssues', 'recentProjects', 'chart', 'cicdRuns', 'agentRunsList',
]

const DEFAULT_CONFIGS = {
  statProjects:   { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  statOpenIssues: { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  statInProgress: { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  statAgentRuns:  { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  recentIssues:   { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  recentProjects: { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  chart:          { hidden: false, width: 'lg', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  cicdRuns:       { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  agentRunsList:  { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
}

const SECTION_LABELS: Record<MainSectionId, string> = {
  statProjects:   'Projects',
  statOpenIssues: 'Open Issues',
  statInProgress: 'In Progress',
  statAgentRuns:  'Agent Runs',
  recentIssues:   'Recent Issues',
  recentProjects: 'Recent Projects',
  chart:          'Issue Activity Chart',
  cicdRuns:       'CI/CD Runs',
  agentRunsList:  'Agent Runs List',
}

const WIDTH_LABELS: Record<MainWidth, string> = { xxs: '1/12', xs: '1/6', quarter: '1/4', sm: '1/3', md: '1/2', lg: 'Full' }
const MAIN_WIDTHS = (['xxs', 'xs', 'quarter', 'sm', 'md', 'lg'] as MainWidth[]).map(v => ({ value: v, label: WIDTH_LABELS[v] }))

const SECTION_DISPLAY_MODES: Partial<Record<MainSectionId, MainDisplayMode[]>> = {
  recentIssues:   ['list', 'count'],
  recentProjects: ['list', 'count'],
  cicdRuns:       ['list', 'count'],
  agentRunsList:  ['list', 'count'],
}

const SECTION_HAS_MAX_ITEMS = new Set<MainSectionId>(['recentIssues', 'recentProjects', 'cicdRuns', 'agentRunsList'])
const SECTION_CAN_TAB = new Set<MainSectionId>(['recentIssues', 'recentProjects', 'chart', 'cicdRuns', 'agentRunsList'])
const SECTION_CAN_STACK = new Set<MainSectionId>([
  'statProjects', 'statOpenIssues', 'statInProgress', 'statAgentRuns',
  'recentIssues', 'recentProjects', 'chart', 'cicdRuns', 'agentRunsList',
])

const {
  layout,
  isDraftMode,
  dragSectionId,
  dragHoverSid,
  renderedItems,
  hiddenSections,
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
  captureSnapshot,
  removeRowBreak,
  exportLayoutJson,
  importLayoutJson,
  onDragStart: onDragStartRaw,
  onDragOver: onDragOverRaw,
  onDragEnter: onDragEnterRaw,
  onDragEnd,
  onGapDragEnter,
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
  storageKey: MAIN_LAYOUT_KEY,
})

// ── Template save / load / export / import ────────────────────────────────
const showSaveModal = ref(false)
const showLoadModal = ref(false)
const importError = ref<string | null>(null)

function handleExportJson() {
  if (!import.meta.client) return
  const json = exportLayoutJson()
  const blob = new Blob([json], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'main-dashboard-layout.json'
  a.click()
  URL.revokeObjectURL(url)
}

function applyImportedLayout(json: string) {
  const ok = importLayoutJson(json)
  if (!ok) {
    importError.value = 'The selected file is not a valid dashboard layout JSON.'
  } else {
    importError.value = null
  }
}

function sectionCfg(s: MainSectionId) { return sectionCfgRaw(s) }
function updateCfg(s: MainSectionId, patch: object) { updateCfgRaw(s, patch) }
function hideSection(id: MainSectionId) { hideSectionRaw(id) }
function showSection(id: MainSectionId) { showSectionRaw(id) }
function onDragStart(e: DragEvent, id: string) { onDragStartRaw(e, id) }
function onDragOver(e: DragEvent, id: string) { onDragOverRaw(e, id) }
function onDragEnter(e: DragEvent, id: string) { onDragEnterRaw(e, id) }
function toggleTabGroupWithNext(sid: MainSectionId) { toggleTabGroupWithNextRaw(sid) }
function toggleStackGroupWithNext(sid: MainSectionId) { toggleStackGroupWithNextRaw(sid) }

function mainColSpanClass(width: MainWidth): string {
  if (width === 'xxs')     return 'col-span-12 sm:col-span-6 lg:col-span-1'
  if (width === 'xs')      return 'col-span-12 sm:col-span-6 lg:col-span-2'
  if (width === 'quarter') return 'col-span-12 sm:col-span-6 lg:col-span-3'
  if (width === 'sm')      return 'col-span-12 sm:col-span-6 lg:col-span-4'
  if (width === 'md')      return 'col-span-12 lg:col-span-6'
  return 'col-span-12'
}

function itemColSpanClass(item: { type: string; sid?: string; sections?: string[] }): string {
  if (item.type === 'rowbreak') return 'col-span-12'
  const firstSid = item.type === 'section' ? item.sid! : item.sections![0]
  return mainColSpanClass(sectionCfg(firstSid as MainSectionId).width as MainWidth)
}

// ── Data ─────────────────────────────────────────────────────────────────
const stats = computed(() => ({
  projects: projectsStore.projects.length,
  openIssues: issuesStore.issues.filter(i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled).length,
  inProgress: issuesStore.issues.filter(i => i.status === IssueStatus.InProgress).length,
  agentRuns: runsStore.dashboardSessions.length
}))

const recentIssues = computed(() =>
  issuesStore.issues.slice(0, sectionCfg('recentIssues').maxItems).map(i => ({
    ...i,
    projectName: projectsStore.projects.find(p => p.id === i.projectId)?.name ?? ''
  }))
)

const cicdRunsItems = computed(() => runsStore.runs.slice(0, sectionCfg('cicdRuns').maxItems))
const agentRunsItems = computed(() => runsStore.dashboardSessions.slice(0, sectionCfg('agentRunsList').maxItems))

// ── Chart helpers ─────────────────────────────────────────────────────────
const chartWidth = 600
const chartPad = 36

const CHART_DAY_DEFAULT = 14
const CHART_HEIGHT_OPTIONS = [
  { value: 'xs', label: 'XS' }, { value: 'sm', label: 'S' }, { value: 'md', label: 'M' },
  { value: 'lg', label: 'L' }, { value: 'xl', label: 'XL' },
]
const CHART_HEIGHT_PX: Record<string, number> = { xs: 80, sm: 120, md: 180, lg: 260, xl: 360 }

const chartHeightPx = computed(() =>
  CHART_HEIGHT_PX[sectionCfg('chart').chartHeightKey ?? 'md'] ?? 160,
)

const filteredIssueHistory = computed(() => {
  const days = sectionCfg('chart').chartDays ?? CHART_DAY_DEFAULT
  if (!issueHistory.value.length) return issueHistory.value
  const cutoff = new Date()
  cutoff.setDate(cutoff.getDate() - days)
  return issueHistory.value.filter(e => new Date(e.date) >= cutoff)
})

const chartMaxY = computed(() => {
  const max = Math.max(...filteredIssueHistory.value.flatMap(e => [e.open, e.inProgress, e.done]), 1)
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
  const n = filteredIssueHistory.value.length
  const plotW = chartWidth - chartPad * 2
  return chartPad + (n > 1 ? (i / (n - 1)) * plotW : plotW / 2)
}

function linePoints(key: 'open' | 'inProgress' | 'done') {
  return filteredIssueHistory.value.map((e, i) => `${xPos(i)},${yScale(e[key])}`).join(' ')
}

function shortDate(d: string) {
  const dt = new Date(d)
  return `${dt.getMonth() + 1}/${dt.getDate()}`
}

function statusDot(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

function priorityBadge(priority: IssuePriority) {
  const map: Record<IssuePriority, string> = {
    [IssuePriority.Urgent]: 'bg-red-900/60 text-red-300',
    [IssuePriority.High]: 'bg-orange-900/60 text-orange-300',
    [IssuePriority.Medium]: 'bg-yellow-900/60 text-yellow-300',
    [IssuePriority.Low]: 'bg-blue-900/60 text-blue-300',
    [IssuePriority.NoPriority]: 'bg-gray-800 text-gray-400'
  }
  return map[priority] ?? 'bg-gray-800 text-gray-400'
}


onMounted(async () => {
  if (import.meta.client) loadLayout()
  await Promise.allSettled([
    projectsStore.fetchProjects(),
    issuesStore.fetchIssues(),
    runsStore.fetchRuns(),
    runsStore.fetchDashboardSessions(),
    api.get<IssueHistoryEntry[]>('/api/dashboard/issue-history')
      .then(data => { issueHistory.value = data })
      .catch((e) => { console.error('Failed to load issue history', e) }),
  ])
})
</script>
