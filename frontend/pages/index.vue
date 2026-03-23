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
      <button v-if="!isDraftMode" @click="enterDraftMode"
        class="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-300 transition-colors px-3 py-1.5 rounded-lg border border-gray-700 hover:border-gray-600 hover:bg-gray-800/50">
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
        </svg>
        Customize
      </button>
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
        <button @click="addCiCdRunsCard()"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          + CI/CD Runs
        </button>
        <button @click="addAgentRunsCard()"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          + Agent Runs
        </button>
        <button @click="addRecentIssuesCard()"
          class="text-xs text-gray-500 hover:text-gray-200 px-2.5 py-1.5 rounded-lg hover:bg-gray-800 transition-colors flex items-center gap-1">
          + Issues
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
        {{ allSectionLabels[sid] ?? sid }}
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
              :label="allSectionLabels[item.sid] ?? item.sid"
              :display-modes="getSectionDisplayModes(item.sid)"
              :current-display-mode="sectionCfg(item.sid).displayMode"
              :has-max-items="sectionHasMaxItems(item.sid)"
              :max-items-options="[3,5,8,10]"
              :current-max-items="sectionCfg(item.sid).maxItems"
              :widths="MAIN_WIDTHS"
              :current-width="sectionCfg(item.sid).width"
              :current-chart-days="item.sid === 'chart' ? (sectionCfg(item.sid).chartDays ?? CHART_DAY_DEFAULT) : undefined"
              :chart-height-options="item.sid === 'chart' ? CHART_HEIGHT_OPTIONS : undefined"
              :current-chart-height="item.sid === 'chart' ? (sectionCfg(item.sid).chartHeightKey ?? 'md') : undefined"
              :sort-by-options="item.sid === 'recentProjects' ? RECENT_PROJECTS_SORT_OPTIONS : isRecentIssuesSid(item.sid) ? RECENT_ISSUES_SORT_OPTIONS : undefined"
              :current-sort-by="(item.sid === 'recentProjects' || isRecentIssuesSid(item.sid)) ? (sectionCfg(item.sid).sortBy ?? 'default') : undefined"
              :project-options="isRecentIssuesSid(item.sid) ? projectsStore.projects.map(p => ({ id: p.id, name: p.name })) : undefined"
              :current-project-filter="isRecentIssuesSid(item.sid) ? (sectionCfg(item.sid).projectFilter ?? null) : undefined"
              :can-tab="sectionCanTab(item.sid) && layout.order.indexOf(item.sid) < layout.order.length - 1"
              :is-tabbed="sectionCfg(item.sid).tabGroup !== null"
              :can-stack="sectionCanStack(item.sid) && layout.order.indexOf(item.sid) < layout.order.length - 1"
              :is-stacked="sectionCfg(item.sid).stackGroup !== null"
              :hidden="sectionCfg(item.sid).hidden"
              :can-remove="isDynamicVariant(item.sid)"
              :drag-hover="dragSectionId !== null && dragHoverSid === item.sid && dragSectionId !== item.sid"
              @display-mode-change="m => updateCfg(item.sid, { displayMode: m as MainDisplayMode })"
              @max-items-change="n => updateCfg(item.sid, { maxItems: n })"
              @width-change="w => updateCfg(item.sid, { width: w as MainWidth })"
              @chart-days-change="d => updateCfg(item.sid, { chartDays: d })"
              @chart-height-change="k => updateCfg(item.sid, { chartHeightKey: k })"
              @sort-by-change="v => updateCfg(item.sid, { sortBy: v })"
              @project-filter-change="v => updateCfg(item.sid, { projectFilter: v ?? undefined })"
              @tab-toggle="toggleTabGroupWithNext(item.sid as MainSectionId)"
              @tab-drop="droppedSid => tabWithSection(item.sid, droppedSid)"
              @stack-toggle="toggleStackGroupWithNext(item.sid as MainSectionId)"
              @stack-drop="droppedSid => stackWithSection(item.sid, droppedSid)"
              @hide="hideSection(item.sid as MainSectionId)"
              @show="showSection(item.sid as MainSectionId)"
              @remove="removeDynamicSection(item.sid)"
            />
            <!-- Tab group config bar -->
            <DashboardTabGroupBar
              v-else-if="item.type === 'tabgroup'"
              :sections="item.sections"
              :section-labels="allSectionLabels"
              :widths="MAIN_WIDTHS"
              :current-width="sectionCfg(item.sections[0] as MainSectionId).width"
              @split="toggleTabGroupWithNext(item.sections[0] as MainSectionId)"
              @width-change="w => updateCfg(item.sections[0] as MainSectionId, { width: w as MainWidth })"
            />
            <!-- Stack group config bar -->
            <DashboardStackGroupBar
              v-else-if="item.type === 'stackgroup'"
              :sections="item.sections"
              :section-labels="allSectionLabels"
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
                {{ allSectionLabels[sec] ?? sec }}
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
                  <NuxtLink :to="statOpenIssuesLink(sectionCfg('statOpenIssues').displayMode ?? 'open')"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">{{ statOpenIssuesLabel(sectionCfg('statOpenIssues').displayMode ?? 'open') }}</p>
                    <p :class="statOpenIssuesColor(sectionCfg('statOpenIssues').displayMode ?? 'open')" class="text-3xl font-bold mt-1">
                      {{ statOpenIssuesCount(sectionCfg('statOpenIssues').displayMode ?? 'open') }}
                    </p>
                  </NuxtLink>
                </template>

                <!-- ── statInProgress ── -->
                <template v-else-if="sid === 'statInProgress'">
                  <NuxtLink to="/runs"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">CI/CD Running</p>
                    <p class="text-3xl font-bold text-yellow-400 mt-1">{{ stats.runningCiCd }}</p>
                  </NuxtLink>
                </template>

                <!-- ── statAgentRuns ── -->
                <template v-else-if="sid === 'statAgentRuns'">
                  <NuxtLink to="/runs"
                    class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-5 block transition-colors">
                    <p class="text-sm text-gray-400">{{ statAgentRunsLabel(sectionCfg('statAgentRuns').displayMode ?? 'running') }}</p>
                    <p :class="statAgentRunsColor(sectionCfg('statAgentRuns').displayMode ?? 'running')" class="text-3xl font-bold mt-1">
                      {{ statAgentRunsCount(sectionCfg('statAgentRuns').displayMode ?? 'running') }}
                    </p>
                  </NuxtLink>
                </template>

                <!-- ── recentIssues (and dynamic recentIssues-* variants) ── -->
                <template v-else-if="isRecentIssuesSid(sid)">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Recent Issues</h2>
                      <NuxtLink to="/issues" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg(sid).displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ stats.openIssues }}
                        <p class="text-sm text-gray-400 font-normal mt-1">open issues</p>
                      </div>
                    </template>
                    <template v-else-if="sectionCfg(sid).displayMode === 'chart'">
                      <div class="py-2">
                        <p v-if="issuesDailyData.every(d => d.count === 0)" class="text-sm text-gray-500 text-center py-4">No issues created in the last {{ CICD_CHART_DAY_DEFAULT }} days</p>
                        <template v-else>
                          <svg :viewBox="`0 0 ${chartWidth} 120`" class="w-full" style="min-width:300px">
                            <g v-for="(d, i) in issuesDailyData" :key="d.date">
                              <rect
                                :x="barX(i, issuesDailyData.length)"
                                :y="120 - 30 - (issuesDailyMaxY > 0 ? (d.count / issuesDailyMaxY) * 80 : 0)"
                                :width="barW(issuesDailyData.length)"
                                :height="issuesDailyMaxY > 0 ? (d.count / issuesDailyMaxY) * 80 : 0"
                                fill="#f59e0b" opacity="0.8" rx="2" />
                              <text :x="barX(i, issuesDailyData.length) + barW(issuesDailyData.length) / 2" y="118"
                                text-anchor="middle" fill="#6b7280" font-size="8">{{ shortDate(d.date) }}</text>
                            </g>
                            <text x="2" y="38" fill="#6b7280" font-size="8">{{ issuesDailyMaxY }}</text>
                            <text x="2" y="118" fill="#6b7280" font-size="8">0</text>
                          </svg>
                        </template>
                      </div>
                    </template>
                    <template v-else>
                      <div class="space-y-1">
                        <NuxtLink v-for="issue in sortedIssues(sid)" :key="issue.id"
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
                        <p v-if="sortedIssues(sid).length === 0" class="text-sm text-gray-500 py-4 text-center">No recent issues</p>
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
                        <NuxtLink v-for="project in sortedProjects('recentProjects')" :key="project.id"
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

                <!-- ── cicdRuns (and dynamic cicdRuns-* variants) ── -->
                <template v-else-if="isCiCdRunsSid(sid)">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">CI/CD Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg(sid).displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.runs.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">CI/CD runs</p>
                      </div>
                    </template>
                    <template v-else-if="sectionCfg(sid).displayMode === 'chart'">
                      <div class="py-2">
                        <p v-if="cicdDailyData.length === 0" class="text-sm text-gray-500 text-center py-4">No data yet</p>
                        <template v-else>
                          <svg :viewBox="`0 0 ${chartWidth} 120`" class="w-full" style="min-width:300px">
                            <g v-for="(d, i) in cicdDailyData" :key="d.date">
                              <rect
                                :x="barX(i, cicdDailyData.length)"
                                :y="120 - 30 - (cicdMaxY > 0 ? (d.count / cicdMaxY) * 80 : 0)"
                                :width="barW(cicdDailyData.length)"
                                :height="cicdMaxY > 0 ? (d.count / cicdMaxY) * 80 : 0"
                                fill="#3b82f6" opacity="0.8" rx="2" />
                              <text :x="barX(i, cicdDailyData.length) + barW(cicdDailyData.length) / 2" y="118"
                                text-anchor="middle" fill="#6b7280" font-size="8">{{ shortDate(d.date) }}</text>
                            </g>
                            <text x="2" y="38" fill="#6b7280" font-size="8">{{ cicdMaxY }}</text>
                            <text x="2" y="118" fill="#6b7280" font-size="8">0</text>
                          </svg>
                        </template>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="cicdRunsItemsForSid(sid).length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="run in cicdRunsItemsForSid(sid)" :key="run.id"
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

                <!-- ── agentRunsList (and dynamic agentRuns-* variants) ── -->
                <template v-else-if="isAgentRunsSid(sid)">
                  <div :class="item.type !== 'tabgroup' ? 'bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4' : ''">
                    <div class="flex items-center justify-between mb-4">
                      <h2 class="font-semibold text-white">Agent Runs</h2>
                      <NuxtLink to="/runs" class="text-xs text-brand-400 hover:text-brand-300">View all →</NuxtLink>
                    </div>
                    <template v-if="sectionCfg(sid).displayMode === 'count'">
                      <div class="text-4xl font-bold text-white text-center py-6">
                        {{ runsStore.dashboardSessions.length }}
                        <p class="text-sm text-gray-400 font-normal mt-1">agent runs</p>
                      </div>
                    </template>
                    <template v-else-if="sectionCfg(sid).displayMode === 'chart'">
                      <div class="py-2">
                        <p v-if="agentDailyData.length === 0" class="text-sm text-gray-500 text-center py-4">No data yet</p>
                        <template v-else>
                          <svg :viewBox="`0 0 ${chartWidth} 120`" class="w-full" style="min-width:300px">
                            <g v-for="(d, i) in agentDailyData" :key="d.date">
                              <rect
                                :x="barX(i, agentDailyData.length)"
                                :y="120 - 30 - (agentMaxY > 0 ? (d.count / agentMaxY) * 80 : 0)"
                                :width="barW(agentDailyData.length)"
                                :height="agentMaxY > 0 ? (d.count / agentMaxY) * 80 : 0"
                                fill="#22c55e" opacity="0.8" rx="2" />
                              <text :x="barX(i, agentDailyData.length) + barW(agentDailyData.length) / 2" y="118"
                                text-anchor="middle" fill="#6b7280" font-size="8">{{ shortDate(d.date) }}</text>
                            </g>
                            <text x="2" y="38" fill="#6b7280" font-size="8">{{ agentMaxY }}</text>
                            <text x="2" y="118" fill="#6b7280" font-size="8">0</text>
                          </svg>
                        </template>
                      </div>
                    </template>
                    <template v-else>
                      <div v-if="agentRunsItemsForSid(sid).length" class="rounded-lg border border-gray-800 overflow-hidden">
                        <table class="w-full text-sm">
                          <tbody class="divide-y divide-gray-800">
                            <tr v-for="session in agentRunsItemsForSid(sid)" :key="session.id"
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
import { IssueStatus, IssuePriority, CiCdRunStatus, AgentSessionStatus, type IssueHistoryEntry } from '~/types'
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
type MainDisplayMode = 'list' | 'count' | 'chart' | 'open' | 'in_progress' | 'closed' | 'total' | 'newly_created' | 'running' | 'failed' | 'failed_24h'

const MAIN_LAYOUT_KEY = 'main-dashboard-layout-v8'

const DEFAULT_ORDER: MainSectionId[] = [
  'statProjects', 'statOpenIssues', 'statInProgress', 'statAgentRuns',
  'recentIssues', 'recentProjects', 'chart', 'cicdRuns', 'agentRunsList',
]

const DEFAULT_CONFIGS = {
  statProjects:   { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  statOpenIssues: { hidden: false, width: 'quarter', displayMode: 'open', maxItems: 5, tabGroup: null, stackGroup: null },
  statInProgress: { hidden: false, width: 'quarter', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  statAgentRuns:  { hidden: false, width: 'quarter', displayMode: 'running', maxItems: 5, tabGroup: null, stackGroup: null },
  recentIssues:   { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  recentProjects: { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  chart:          { hidden: false, width: 'lg', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  cicdRuns:       { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
  agentRunsList:  { hidden: false, width: 'md', displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null },
}

const SECTION_LABELS: Record<MainSectionId, string> = {
  statProjects:   'Projects',
  statOpenIssues: 'Open Issues',
  statInProgress: 'CI/CD Running',
  statAgentRuns:  'Running Agents',
  recentIssues:   'Recent Issues',
  recentProjects: 'Recent Projects',
  chart:          'Issue Activity Chart',
  cicdRuns:       'CI/CD Runs',
  agentRunsList:  'Agent Runs List',
}

const WIDTH_LABELS: Record<MainWidth, string> = { xxs: '1/12', xs: '1/6', quarter: '1/4', sm: '1/3', md: '1/2', lg: 'Full' }
const MAIN_WIDTHS = (['xxs', 'xs', 'quarter', 'sm', 'md', 'lg'] as MainWidth[]).map(v => ({ value: v, label: WIDTH_LABELS[v] }))

const SECTION_DISPLAY_MODES: Partial<Record<string, string[]>> = {
  statOpenIssues: ['open', 'in_progress', 'closed', 'total', 'newly_created'],
  statAgentRuns:  ['running', 'total', 'failed', 'failed_24h'],
  recentIssues:   ['list', 'count', 'chart'],
  recentProjects: ['list', 'count'],
  cicdRuns:       ['list', 'count', 'chart'],
  agentRunsList:  ['list', 'count', 'chart'],
}

const DYNAMIC_SECTION_DISPLAY_MODES = ['list', 'count', 'chart']

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
  addDynamicSection,
  removeDynamicSection,
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

function sectionCfg(s: string) { return sectionCfgRaw(s) }
function updateCfg(s: string, patch: object) { updateCfgRaw(s, patch) }
function hideSection(id: MainSectionId) { hideSectionRaw(id) }
function showSection(id: MainSectionId) { showSectionRaw(id) }
function onDragStart(e: DragEvent, id: string) { onDragStartRaw(e, id) }
function onDragOver(e: DragEvent, id: string) { onDragOverRaw(e, id) }
function onDragEnter(e: DragEvent, id: string) { onDragEnterRaw(e, id) }
function toggleTabGroupWithNext(sid: MainSectionId) { toggleTabGroupWithNextRaw(sid) }
function toggleStackGroupWithNext(sid: MainSectionId) { toggleStackGroupWithNextRaw(sid) }

function isCiCdRunsSid(sid: string) { return sid === 'cicdRuns' || /^cicdRuns-\d/.test(sid) }
function isAgentRunsSid(sid: string) { return sid === 'agentRunsList' || /^agentRuns-\d/.test(sid) }
function isRecentIssuesSid(sid: string) { return sid === 'recentIssues' || /^recentIssues-\d/.test(sid) }

function isDynamicVariant(sid: string): boolean {
  return (isCiCdRunsSid(sid) && sid !== 'cicdRuns')
    || (isAgentRunsSid(sid) && sid !== 'agentRunsList')
    || (isRecentIssuesSid(sid) && sid !== 'recentIssues')
}

function getSectionDisplayModes(sid: string): string[] | undefined {
  if (SECTION_DISPLAY_MODES[sid]) return SECTION_DISPLAY_MODES[sid]
  if (isCiCdRunsSid(sid) || isAgentRunsSid(sid) || isRecentIssuesSid(sid)) return DYNAMIC_SECTION_DISPLAY_MODES
  return undefined
}

function sectionHasMaxItems(sid: string): boolean {
  return SECTION_HAS_MAX_ITEMS.has(sid as MainSectionId) || isCiCdRunsSid(sid) || isAgentRunsSid(sid) || isRecentIssuesSid(sid)
}

function sectionCanTab(sid: string): boolean {
  return SECTION_CAN_TAB.has(sid as MainSectionId) || isCiCdRunsSid(sid) || isAgentRunsSid(sid) || isRecentIssuesSid(sid)
}

function sectionCanStack(sid: string): boolean {
  return SECTION_CAN_STACK.has(sid as MainSectionId) || isCiCdRunsSid(sid) || isAgentRunsSid(sid) || isRecentIssuesSid(sid)
}

function getDynamicSectionLabel(sid: string): string {
  if (isCiCdRunsSid(sid)) return 'CI/CD Runs'
  if (isAgentRunsSid(sid)) return 'Agent Runs'
  if (isRecentIssuesSid(sid)) return 'Recent Issues'
  return sid
}

const allSectionLabels = computed<Record<string, string>>(() => {
  const extra: Record<string, string> = {}
  for (const sid of layout.value.order) {
    if (!(sid in SECTION_LABELS)) {
      extra[sid] = getDynamicSectionLabel(sid)
    }
  }
  return { ...SECTION_LABELS, ...extra }
})

const DEFAULT_DYNAMIC_CFG = { hidden: false, width: 'md' as MainWidth, displayMode: 'list', maxItems: 5, tabGroup: null, stackGroup: null }

function addCiCdRunsCard() {
  addDynamicSection('cicdRuns', { ...DEFAULT_DYNAMIC_CFG })
}
function addAgentRunsCard() {
  addDynamicSection('agentRuns', { ...DEFAULT_DYNAMIC_CFG })
}
function addRecentIssuesCard() {
  addDynamicSection('recentIssues', { ...DEFAULT_DYNAMIC_CFG })
}

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
const NEWLY_CREATED_DAYS = 7
const sevenDaysAgo = computed(() => { const d = new Date(); d.setDate(d.getDate() - NEWLY_CREATED_DAYS); return d })

const PRIORITY_ORDER: Record<string, number> = { urgent: 0, high: 1, medium: 2, low: 3, no_priority: 4 }

const twentyFourHoursAgo = computed(() => { const d = new Date(); d.setHours(d.getHours() - 24); return d })

const stats = computed(() => ({
  projects: projectsStore.projects.length,
  openIssues: issuesStore.issues.filter(i => i.status !== IssueStatus.Done && i.status !== IssueStatus.Cancelled && i.status !== IssueStatus.InProgress).length,
  inProgress: issuesStore.issues.filter(i => i.status === IssueStatus.InProgress).length,
  closed: issuesStore.issues.filter(i => i.status === IssueStatus.Done || i.status === IssueStatus.Cancelled).length,
  total: issuesStore.issues.length,
  newlyCreated: issuesStore.issues.filter(i => new Date(i.createdAt) >= sevenDaysAgo.value).length,
  runningCiCd: runsStore.runs.filter(r => r.status === CiCdRunStatus.Running || r.status === CiCdRunStatus.Pending).length,
  runningAgents: runsStore.dashboardSessions.filter(s => s.status === AgentSessionStatus.Running || s.status === AgentSessionStatus.Pending).length,
  agentRuns: runsStore.dashboardSessions.length,
  failedAgents: runsStore.dashboardSessions.filter(s => s.status === AgentSessionStatus.Failed).length,
  failedAgents24h: runsStore.dashboardSessions.filter(s => s.status === AgentSessionStatus.Failed && s.endedAt && new Date(s.endedAt) >= twentyFourHoursAgo.value).length,
}))

function statOpenIssuesLabel(mode: string): string {
  if (mode === 'in_progress') return 'In Progress'
  if (mode === 'closed') return 'Closed Issues'
  if (mode === 'total') return 'Total Issues'
  if (mode === 'newly_created') return `New (${NEWLY_CREATED_DAYS}d)`
  return 'Open Issues'
}
function statOpenIssuesCount(mode: string): number {
  if (mode === 'in_progress') return stats.value.inProgress
  if (mode === 'closed') return stats.value.closed
  if (mode === 'total') return stats.value.total
  if (mode === 'newly_created') return stats.value.newlyCreated
  return stats.value.openIssues
}
function statOpenIssuesLink(mode: string): string {
  if (mode === 'in_progress') return '/issues?status=in_progress'
  if (mode === 'closed') return '/issues?status=done'
  return '/issues?status=open'
}
function statOpenIssuesColor(mode: string): string {
  if (mode === 'in_progress') return 'text-indigo-400'
  if (mode === 'closed') return 'text-green-400'
  if (mode === 'total') return 'text-gray-300'
  if (mode === 'newly_created') return 'text-purple-400'
  return 'text-amber-400'
}

function statAgentRunsLabel(mode: string): string {
  if (mode === 'total') return 'Agent Runs'
  if (mode === 'failed') return 'Failed Agents'
  if (mode === 'failed_24h') return 'Failed (24h)'
  return 'Running Agents'
}
function statAgentRunsCount(mode: string): number {
  if (mode === 'total') return stats.value.agentRuns
  if (mode === 'failed') return stats.value.failedAgents
  if (mode === 'failed_24h') return stats.value.failedAgents24h
  return stats.value.runningAgents
}
function statAgentRunsColor(mode: string): string {
  if (mode === 'failed' || mode === 'failed_24h') return 'text-red-400'
  if (mode === 'total') return 'text-gray-300'
  return 'text-green-400'
}

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

// ── Bar chart helpers (CI/CD + Agent + Issues daily counts) ──────────────
const CICD_CHART_DAY_DEFAULT = 14

function isoDateStr(isoString?: string | null): string {
  return isoString?.slice(0, 10) ?? ''
}

function buildDailyData(days: number, matchFn: (dateStr: string) => number): { date: string; count: number }[] {
  const result: { date: string; count: number }[] = []
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date()
    d.setDate(d.getDate() - i)
    const dateStr = d.toISOString().slice(0, 10)
    result.push({ date: dateStr, count: matchFn(dateStr) })
  }
  return result
}

const cicdDailyData = computed(() =>
  buildDailyData(CICD_CHART_DAY_DEFAULT, dateStr =>
    runsStore.runs.filter(r => isoDateStr(r.startedAt) === dateStr).length
  )
)
const cicdMaxY = computed(() => Math.max(...cicdDailyData.value.map(d => d.count), 1))

const agentDailyData = computed(() =>
  buildDailyData(CICD_CHART_DAY_DEFAULT, dateStr =>
    runsStore.dashboardSessions.filter(s => isoDateStr(s.startedAt) === dateStr).length
  )
)
const agentMaxY = computed(() => Math.max(...agentDailyData.value.map(d => d.count), 1))

const issuesDailyData = computed(() =>
  buildDailyData(CICD_CHART_DAY_DEFAULT, dateStr =>
    issuesStore.issues.filter(i => isoDateStr(i.createdAt) === dateStr).length
  )
)
const issuesDailyMaxY = computed(() => Math.max(...issuesDailyData.value.map(d => d.count), 1))

function barX(i: number, total: number): number {
  const plotW = chartWidth - chartPad * 2
  const w = plotW / total
  return chartPad + i * w + w * 0.1
}
function barW(total: number): number {
  const plotW = chartWidth - chartPad * 2
  return (plotW / total) * 0.8
}

// ── Sort helpers ──────────────────────────────────────────────────────────
const RECENT_PROJECTS_SORT_OPTIONS = [
  { value: 'default', label: 'Default' },
  { value: 'name', label: 'Name' },
  { value: 'issueCount', label: 'Issues' },
  { value: 'lastActivity', label: 'Last Active' },
]

function sortedProjects(sid: string) {
  const sortBy = sectionCfg(sid).sortBy ?? 'default'
  const list = [...projectsStore.projects]
  if (sortBy === 'name') list.sort((a, b) => a.name.localeCompare(b.name))
  else if (sortBy === 'issueCount') list.sort((a, b) => (b.issueCount ?? 0) - (a.issueCount ?? 0))
  else if (sortBy === 'lastActivity') list.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
  return list.slice(0, sectionCfg(sid).maxItems)
}

const RECENT_ISSUES_SORT_OPTIONS = [
  { value: 'default', label: 'Default' },
  { value: 'priority', label: 'Priority' },
  { value: 'status', label: 'Status' },
]

function sortedIssues(sid: string) {
  const sortBy = sectionCfg(sid).sortBy ?? 'default'
  const projectFilter = sectionCfg(sid).projectFilter ?? ''
  let list = [...issuesStore.issues]
  if (projectFilter) list = list.filter(i => i.projectId === projectFilter)
  if (sortBy === 'priority') {
    list.sort((a, b) => (PRIORITY_ORDER[a.priority] ?? 99) - (PRIORITY_ORDER[b.priority] ?? 99))
  } else if (sortBy === 'status') {
    list.sort((a, b) => a.status.localeCompare(b.status))
  }
  return list.slice(0, sectionCfg(sid).maxItems).map(i => ({
    ...i,
    projectName: projectsStore.projects.find(p => p.id === i.projectId)?.name ?? ''
  }))
}

function cicdRunsItemsForSid(sid: string) { return runsStore.runs.slice(0, sectionCfg(sid).maxItems) }
function agentRunsItemsForSid(sid: string) { return runsStore.dashboardSessions.slice(0, sectionCfg(sid).maxItems) }


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
