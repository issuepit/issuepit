<template>
  <div class="p-8">
    <!-- Loading skeleton -->
    <div v-if="projectsStore.loading && !projectsStore.currentProject" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="projectsStore.currentProject">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 mb-6">
        <PageBreadcrumb :items="[
          { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
          { label: projectsStore.currentProject.name, to: `/projects/${id}`, color: projectsStore.currentProject.color || '#4c6ef5' },
          { label: 'Test History', to: `/projects/${id}/test-history`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4' },
        ]" />
      </div>

      <!-- Toolbar: tabs + filters + import button -->
      <div class="flex flex-wrap items-center gap-3 mb-6">
        <!-- Tab bar -->
        <div class="flex gap-1">
          <button
            v-for="tab in tabs"
            :key="tab.value"
            :class="['px-4 py-1.5 text-sm font-medium rounded-lg transition-colors', activeTab === tab.value ? 'bg-brand-600 text-white' : 'bg-gray-800 text-gray-400 hover:text-gray-200']"
            @click="activeTab = tab.value"
          >{{ tab.label }}</button>
        </div>

        <!-- Branch filter -->
        <select
          v-model="selectedBranch"
          class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
          @change="loadCurrentTab"
        >
          <option value="">All branches</option>
          <option v-for="b in store.branches" :key="b" :value="b">{{ b }}</option>
        </select>

        <!-- Import button -->
        <button
          class="ml-auto flex items-center gap-1.5 px-3 py-1.5 text-sm bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg transition-colors"
          @click="showImport = true"
        >
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          Import TRX
        </button>
      </div>

      <!-- ── History tab ──────────────────────────────────────────────────── -->
      <div v-if="activeTab === 'history'">
        <!-- Summary mini-chart: pass/fail bars -->
        <div v-if="store.history.length" class="bg-gray-900 border border-gray-800 rounded-xl p-4 mb-5">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-sm font-semibold text-white">Test run trend (latest {{ store.history.length }})</h3>
            <div class="flex items-center gap-4 text-xs text-gray-500">
              <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-green-500 inline-block" />Passed</span>
              <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-red-500 inline-block" />Failed</span>
              <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-yellow-500 inline-block" />Skipped</span>
            </div>
          </div>
          <div class="flex items-end gap-1 h-20 overflow-x-auto pb-1">
            <div
              v-for="suite in [...store.history].reverse()"
              :key="suite.id"
              class="flex flex-col-reverse gap-px min-w-[10px] flex-1 max-w-[28px] cursor-pointer group relative"
              :title="`${suite.run.branch ?? '?'} @ ${suite.run.commitSha.slice(0, 7)}\n${suite.passedTests}p / ${suite.failedTests}f / ${suite.skippedTests}s`"
              @click="$router.push(`/projects/${id}/runs/cicd/${suite.run.id}`)"
            >
              <div
                class="bg-green-500 group-hover:bg-green-400 transition-colors rounded-sm"
                :style="`height: ${barPct(suite.passedTests, suite.totalTests)}%`"
              />
              <div
                v-if="suite.failedTests"
                class="bg-red-500 group-hover:bg-red-400 transition-colors rounded-sm"
                :style="`height: ${barPct(suite.failedTests, suite.totalTests)}%`"
              />
              <div
                v-if="suite.skippedTests"
                class="bg-yellow-500 group-hover:bg-yellow-400 transition-colors rounded-sm"
                :style="`height: ${barPct(suite.skippedTests, suite.totalTests)}%`"
              />
            </div>
          </div>
        </div>

        <!-- Run list -->
        <div v-if="store.loading" class="flex items-center justify-center py-12">
          <div class="w-7 h-7 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="store.history.length === 0" class="text-center py-12 text-gray-500 text-sm">
          No test results found for this project.
          <br>
          <span class="text-xs text-gray-600">
            Results are saved automatically when CI/CD runs produce <code>.trx</code> artifact files.
            You can also <button class="text-brand-400 hover:text-brand-300 underline" @click="showImport = true">import a TRX file</button>.
          </span>
        </div>

        <div v-else class="space-y-2">
          <NuxtLink
            v-for="suite in store.history"
            :key="suite.id"
            :to="`/projects/${id}/runs/cicd/${suite.run.id}`"
            class="block bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 transition-colors"
          >
            <div class="flex items-center gap-3 flex-wrap">
              <!-- Status indicator -->
              <span
                :class="suite.failedTests > 0 ? 'text-red-400 bg-red-900/30' : 'text-green-400 bg-green-900/30'"
                class="flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium shrink-0"
              >
                <span :class="suite.failedTests > 0 ? 'bg-red-400' : 'bg-green-400'" class="w-1.5 h-1.5 rounded-full" />
                {{ suite.failedTests > 0 ? 'Failed' : 'Passed' }}
              </span>

              <!-- Counts -->
              <span class="text-xs text-gray-400">
                <span class="text-green-400">{{ suite.passedTests }}p</span>
                <span v-if="suite.failedTests" class="text-red-400 ml-1">{{ suite.failedTests }}f</span>
                <span v-if="suite.skippedTests" class="text-yellow-500 ml-1">{{ suite.skippedTests }}s</span>
                <span class="text-gray-500 ml-1">/ {{ suite.totalTests }} total</span>
              </span>

              <!-- Duration -->
              <span class="text-xs text-gray-500">{{ formatMs(suite.durationMs) }}</span>

              <!-- Artifact name -->
              <span class="text-xs text-gray-600 font-mono">{{ suite.artifactName }}</span>

              <!-- Branch / commit -->
              <span class="ml-auto flex items-center gap-1.5 text-xs text-gray-500">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18m0 0h10a2 2 0 002-2V9M9 21H5a2 2 0 01-2-2V9m0 0h18" />
                </svg>
                <span class="font-mono">{{ suite.run.branch ?? '—' }}</span>
                <span class="font-mono text-gray-600">{{ suite.run.commitSha.slice(0, 7) }}</span>
              </span>
              <span class="text-xs text-gray-600">{{ formatDate(suite.createdAt) }}</span>
            </div>
          </NuxtLink>
        </div>
      </div>

      <!-- ── Flakiness tab ──────────────────────────────────────────────── -->
      <div v-else-if="activeTab === 'flaky'">
        <div v-if="store.flakyLoading" class="flex items-center justify-center py-12">
          <div class="w-7 h-7 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="store.flakyTests.length === 0" class="text-center py-12 text-gray-500 text-sm">
          No flaky tests detected.
          <br>
          <span class="text-xs text-gray-600">
            A test is considered flaky if it has ≥3 runs and its failure rate is between 5% and 95%.
          </span>
        </div>

        <div v-else class="space-y-2">
          <!-- Min-runs filter -->
          <div class="flex items-center gap-3 mb-4">
            <label class="text-sm text-gray-400">Min. runs:</label>
            <input
              v-model.number="minRuns"
              type="number"
              min="2"
              max="100"
              class="w-20 bg-gray-800 border border-gray-700 rounded-lg px-2 py-1 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
              @change="store.fetchFlakyTests(id, { branch: selectedBranch || undefined, minRuns })"
            >
          </div>

          <div
            v-for="test in store.flakyTests"
            :key="test.fullName"
            class="bg-gray-900 border border-gray-800 rounded-xl p-4 cursor-pointer hover:border-gray-700 transition-colors"
            @click="openTestDetail(test.fullName)"
          >
            <div class="flex items-start gap-3">
              <!-- Failure rate gauge -->
              <div class="shrink-0 w-12 h-12 relative">
                <svg viewBox="0 0 36 36" class="w-12 h-12 -rotate-90">
                  <circle cx="18" cy="18" r="15.9155" fill="none" stroke="#374151" stroke-width="3" />
                  <circle
                    cx="18" cy="18" r="15.9155" fill="none"
                    :stroke="test.failureRate > 0.5 ? '#ef4444' : '#f59e0b'"
                    stroke-width="3"
                    stroke-dasharray="100"
                    :stroke-dashoffset="100 - test.failureRate * 100"
                    stroke-linecap="round"
                  />
                </svg>
                <span class="absolute inset-0 flex items-center justify-center text-xs font-bold text-gray-300">
                  {{ Math.round(test.failureRate * 100) }}%
                </span>
              </div>

              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-white truncate">{{ test.methodName ?? test.fullName }}</p>
                <p class="text-xs text-gray-500 truncate">{{ test.className }}</p>
                <div class="flex items-center gap-3 mt-1 text-xs text-gray-500">
                  <span><span class="text-green-400">{{ test.passedRuns }}</span> passed</span>
                  <span><span class="text-red-400">{{ test.failedRuns }}</span> failed</span>
                  <span>{{ test.totalRuns }} total runs</span>
                  <span>avg {{ formatMs(test.avgDurationMs) }}</span>
                </div>
              </div>

              <svg class="w-4 h-4 text-gray-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Compare tab ─────────────────────────────────────────────────── -->
      <div v-else-if="activeTab === 'compare'">
        <div class="flex gap-3 mb-5 max-w-2xl">
          <div class="flex-1">
            <label class="block text-xs text-gray-500 mb-1">Base commit (SHA or prefix)</label>
            <input
              v-model="compareBase"
              type="text"
              placeholder="abc1234"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
          </div>
          <div class="flex-1">
            <label class="block text-xs text-gray-500 mb-1">Head commit (SHA or prefix)</label>
            <input
              v-model="compareHead"
              type="text"
              placeholder="def5678"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
          </div>
          <div class="flex items-end">
            <button
              :disabled="!compareBase || !compareHead || store.compareLoading"
              class="px-4 py-2 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-lg transition-colors"
              @click="runCompare"
            >
              <span v-if="store.compareLoading" class="flex items-center gap-1.5">
                <span class="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                Comparing…
              </span>
              <span v-else>Compare</span>
            </button>
          </div>
        </div>

        <div v-if="store.comparison">
          <div class="flex gap-4 mb-5 flex-wrap">
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-gray-500 mb-1">Base tests</p>
              <p class="text-2xl font-bold text-white">{{ store.comparison.baseTestCount }}</p>
            </div>
            <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-gray-500 mb-1">Head tests</p>
              <p class="text-2xl font-bold text-white">{{ store.comparison.headTestCount }}</p>
            </div>
            <div v-if="store.comparison.newTests.length" class="bg-green-900/30 border border-green-800/50 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-green-400 mb-1">New tests</p>
              <p class="text-2xl font-bold text-green-400">+{{ store.comparison.newTests.length }}</p>
            </div>
            <div v-if="store.comparison.removedTests.length" class="bg-red-900/30 border border-red-800/50 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-red-400 mb-1">Removed</p>
              <p class="text-2xl font-bold text-red-400">-{{ store.comparison.removedTests.length }}</p>
            </div>
            <div v-if="store.comparison.nowFailing.length" class="bg-orange-900/30 border border-orange-800/50 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-orange-400 mb-1">Now failing</p>
              <p class="text-2xl font-bold text-orange-400">{{ store.comparison.nowFailing.length }}</p>
            </div>
            <div v-if="store.comparison.nowPassing.length" class="bg-teal-900/30 border border-teal-800/50 rounded-xl p-4 flex-1 min-w-[140px]">
              <p class="text-xs text-teal-400 mb-1">Now passing</p>
              <p class="text-2xl font-bold text-teal-400">{{ store.comparison.nowPassing.length }}</p>
            </div>
          </div>

          <!-- Now Failing -->
          <div v-if="store.comparison.nowFailing.length" class="mb-4">
            <h3 class="text-sm font-semibold text-orange-400 mb-2">🔴 Now failing ({{ store.comparison.nowFailing.length }})</h3>
            <div class="space-y-1">
              <div v-for="t in store.comparison.nowFailing" :key="t.fullName"
                class="bg-gray-900 border border-orange-800/30 rounded-lg px-4 py-2 text-sm text-gray-300 cursor-pointer hover:border-orange-600/50"
                @click="openTestDetail(t.fullName)">
                {{ t.fullName }}
              </div>
            </div>
          </div>

          <!-- New tests -->
          <div v-if="store.comparison.newTests.length" class="mb-4">
            <h3 class="text-sm font-semibold text-green-400 mb-2">✅ New tests ({{ store.comparison.newTests.length }})</h3>
            <div class="space-y-1">
              <div v-for="t in store.comparison.newTests" :key="t.fullName"
                class="bg-gray-900 border border-green-800/30 rounded-lg px-4 py-2 text-sm text-gray-300 cursor-pointer hover:border-green-600/50"
                @click="openTestDetail(t.fullName)">
                {{ t.fullName }}
                <span :class="t.outcomeName === 'Passed' ? 'text-green-400' : 'text-red-400'" class="ml-2 text-xs">{{ t.outcomeName }}</span>
              </div>
            </div>
          </div>

          <!-- Removed tests -->
          <div v-if="store.comparison.removedTests.length" class="mb-4">
            <h3 class="text-sm font-semibold text-red-400 mb-2">🗑️ Removed tests ({{ store.comparison.removedTests.length }})</h3>
            <div class="space-y-1">
              <div v-for="t in store.comparison.removedTests" :key="t.fullName"
                class="bg-gray-900 border border-red-800/30 rounded-lg px-4 py-2 text-sm text-gray-300">
                {{ t.fullName }}
              </div>
            </div>
          </div>

          <!-- Now passing -->
          <div v-if="store.comparison.nowPassing.length" class="mb-4">
            <h3 class="text-sm font-semibold text-teal-400 mb-2">💚 Now passing ({{ store.comparison.nowPassing.length }})</h3>
            <div class="space-y-1">
              <div v-for="t in store.comparison.nowPassing" :key="t.fullName"
                class="bg-gray-900 border border-teal-800/30 rounded-lg px-4 py-2 text-sm text-gray-300 cursor-pointer hover:border-teal-600/50"
                @click="openTestDetail(t.fullName)">
                {{ t.fullName }}
              </div>
            </div>
          </div>

          <!-- Slower tests -->
          <div v-if="store.comparison.slowerTests.length" class="mb-4">
            <h3 class="text-sm font-semibold text-yellow-400 mb-2">🐢 Significantly slower ({{ store.comparison.slowerTests.length }})</h3>
            <div class="space-y-1">
              <div v-for="t in store.comparison.slowerTests" :key="t.fullName"
                class="bg-gray-900 border border-yellow-800/30 rounded-lg px-4 py-2 text-sm flex items-center gap-2">
                <span class="text-gray-300 flex-1 truncate">{{ t.fullName }}</span>
                <span class="text-gray-500 text-xs shrink-0">{{ formatMs(t.baseDurationMs) }} → <span class="text-yellow-400">{{ formatMs(t.headDurationMs) }}</span> (+{{ formatMs(t.deltaMs) }})</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Search tab ──────────────────────────────────────────────────── -->
      <div v-else-if="activeTab === 'search'">
        <div class="flex gap-2 mb-5 max-w-2xl">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Search by name, error message, stack trace…"
            class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
            @keydown.enter="runSearch"
          >
          <select
            v-model="searchOutcome"
            class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
          >
            <option value="">All outcomes</option>
            <option value="0">Not executed</option>
            <option value="1">Passed</option>
            <option value="2">Failed</option>
            <option value="3">Skipped</option>
          </select>
          <button
            :disabled="!searchQuery || store.searchLoading"
            class="px-4 py-2 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-lg transition-colors"
            @click="runSearch"
          >
            <span v-if="store.searchLoading" class="flex items-center gap-1.5">
              <span class="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
            </span>
            <span v-else>Search</span>
          </button>
        </div>

        <div v-if="store.searchResults.length === 0 && !store.searchLoading" class="text-center py-8 text-gray-500 text-sm">
          Enter a search query and press Search.
        </div>

        <div v-else class="space-y-2">
          <div
            v-for="tc in store.searchResults"
            :key="tc.id"
            class="bg-gray-900 border border-gray-800 hover:border-gray-700 rounded-xl p-4 cursor-pointer transition-colors"
            @click="openTestDetail(tc.fullName)"
          >
            <div class="flex items-center gap-2 mb-1">
              <span
                :class="outcomeClass(tc.outcome)"
                class="text-xs px-2 py-0.5 rounded-full font-medium"
              >{{ tc.outcomeName }}</span>
              <span class="text-sm text-white truncate flex-1">{{ tc.methodName ?? tc.fullName }}</span>
              <span class="text-xs text-gray-500 shrink-0">{{ formatMs(tc.durationMs) }}</span>
            </div>
            <p class="text-xs text-gray-500 truncate">{{ tc.className }}</p>
            <p v-if="tc.errorMessage" class="text-xs text-red-400 mt-1 line-clamp-2">{{ tc.errorMessage }}</p>
            <div class="flex items-center gap-2 mt-2 text-xs text-gray-600">
              <span class="font-mono">{{ tc.suite.run.branch ?? '—' }}</span>
              <span class="font-mono">{{ tc.suite.run.commitSha.slice(0, 7) }}</span>
              <span>{{ formatDate(tc.suite.createdAt) }}</span>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- ── Test Detail Modal ──────────────────────────────────────────── -->
    <div
      v-if="selectedTestName"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      @click.self="selectedTestName = null"
    >
      <div class="bg-gray-900 border border-gray-800 rounded-2xl w-full max-w-3xl max-h-[90vh] flex flex-col shadow-xl">
        <!-- Modal header -->
        <div class="flex items-center justify-between p-5 border-b border-gray-800 shrink-0">
          <div class="flex-1 min-w-0">
            <h2 class="text-base font-semibold text-white truncate">{{ selectedTestName }}</h2>
            <p class="text-xs text-gray-500 mt-0.5">Test history</p>
          </div>
          <button class="text-gray-500 hover:text-gray-300 ml-4 shrink-0" @click="selectedTestName = null">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- Modal body -->
        <div class="flex-1 overflow-y-auto p-5">
          <div v-if="store.testCaseLoading" class="flex items-center justify-center py-8">
            <div class="w-7 h-7 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
          </div>

          <div v-else-if="store.testCaseHistory.length === 0" class="text-center py-8 text-gray-500 text-sm">
            No history found for this test.
          </div>

          <div v-else>
            <!-- Mini duration sparkline (bars) -->
            <div class="flex items-end gap-1 h-12 mb-4 overflow-x-auto">
              <div
                v-for="entry in [...store.testCaseHistory].reverse()"
                :key="entry.id"
                class="flex-1 min-w-[6px] max-w-[18px] rounded-sm cursor-pointer"
                :class="entry.outcome === 2 ? 'bg-red-500' : entry.outcome === 1 ? 'bg-green-500' : 'bg-yellow-500'"
                :style="`height: ${durationBarPct(entry.durationMs, store.testCaseHistory)}%`"
                :title="`${entry.outcomeName} – ${formatMs(entry.durationMs)}\n${entry.suite.run.branch ?? '?'} @ ${entry.suite.run.commitSha.slice(0,7)}`"
                @click="$router.push(`/projects/${id}/runs/cicd/${entry.suite.run.id}`)"
              />
            </div>

            <!-- Entry list -->
            <div class="space-y-2">
              <div
                v-for="entry in store.testCaseHistory"
                :key="entry.id"
                class="border rounded-xl p-3"
                :class="entry.outcome === 2 ? 'border-red-900/50 bg-red-900/10' : 'border-gray-800 bg-gray-800/30'"
              >
                <div class="flex items-center gap-2">
                  <span :class="outcomeClass(entry.outcome)" class="text-xs px-2 py-0.5 rounded-full font-medium shrink-0">
                    {{ entry.outcomeName }}
                  </span>
                  <span class="text-xs text-gray-400">{{ formatMs(entry.durationMs) }}</span>
                  <NuxtLink
                    :to="`/projects/${id}/runs/cicd/${entry.suite.run.id}`"
                    class="ml-auto text-xs text-gray-600 hover:text-gray-400 font-mono"
                    @click.stop
                  >{{ entry.suite.run.commitSha.slice(0, 7) }}</NuxtLink>
                  <span class="text-xs text-gray-600">{{ formatDate(entry.suite.createdAt) }}</span>
                </div>
                <div v-if="entry.errorMessage" class="mt-2">
                  <p class="text-xs text-red-400">{{ entry.errorMessage }}</p>
                  <pre v-if="entry.stackTrace" class="text-xs text-gray-600 mt-1 overflow-x-auto whitespace-pre-wrap line-clamp-4">{{ entry.stackTrace }}</pre>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ── Import TRX Modal ──────────────────────────────────────────────── -->
    <div
      v-if="showImport"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      @click.self="showImport = false"
    >
      <div class="bg-gray-900 border border-gray-800 rounded-2xl w-full max-w-lg shadow-xl">
        <div class="flex items-center justify-between p-5 border-b border-gray-800">
          <h2 class="text-base font-semibold text-white">Import TRX File</h2>
          <button class="text-gray-500 hover:text-gray-300" @click="showImport = false">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form class="p-5 space-y-4" @submit.prevent="submitImport">
          <div>
            <label class="block text-sm text-gray-300 mb-1.5">TRX file <span class="text-red-400">*</span></label>
            <input
              ref="trxFileInput"
              type="file"
              accept=".trx"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 file:mr-3 file:py-1 file:px-3 file:bg-gray-700 file:border-0 file:text-sm file:text-gray-300 file:rounded"
              @change="onFileChange"
            >
          </div>

          <div>
            <label class="block text-sm text-gray-300 mb-1.5">Commit SHA <span class="text-gray-500 text-xs">(optional)</span></label>
            <input
              v-model="importForm.commitSha"
              type="text"
              placeholder="abc1234..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
          </div>

          <div class="flex gap-3">
            <div class="flex-1">
              <label class="block text-sm text-gray-300 mb-1.5">Branch <span class="text-gray-500 text-xs">(optional)</span></label>
              <input
                v-model="importForm.branch"
                type="text"
                placeholder="main"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
              >
            </div>
            <div class="flex-1">
              <label class="block text-sm text-gray-300 mb-1.5">Workflow <span class="text-gray-500 text-xs">(optional)</span></label>
              <input
                v-model="importForm.workflow"
                type="text"
                placeholder="unit-tests"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
              >
            </div>
          </div>

          <div>
            <label class="block text-sm text-gray-300 mb-1.5">Artifact name <span class="text-gray-500 text-xs">(optional, defaults to file name)</span></label>
            <input
              v-model="importForm.artifactName"
              type="text"
              placeholder="unit-test-results"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
          </div>

          <div v-if="importError" class="text-sm text-red-400 bg-red-900/20 border border-red-800/30 rounded-lg px-3 py-2">
            {{ importError }}
          </div>
          <div v-if="importSuccess" class="text-sm text-green-400 bg-green-900/20 border border-green-800/30 rounded-lg px-3 py-2">
            {{ importSuccess }}
          </div>

          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="!importFile || store.importLoading"
              class="flex-1 py-2 text-sm bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white rounded-lg font-medium transition-colors"
            >
              <span v-if="store.importLoading" class="flex items-center justify-center gap-1.5">
                <span class="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                Importing…
              </span>
              <span v-else>Import</span>
            </button>
            <button type="button" class="flex-1 py-2 text-sm bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg font-medium transition-colors" @click="showImport = false">Cancel</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useProjectsStore } from '~/stores/projects'
import { useTestHistoryStore } from '~/stores/testHistory'

const route = useRoute()
const id = route.params.id as string

const projectsStore = useProjectsStore()
const store = useTestHistoryStore()

// ── State ──────────────────────────────────────────────────────────────────

const tabs = [
  { label: 'History', value: 'history' },
  { label: 'Flakiness', value: 'flaky' },
  { label: 'Compare', value: 'compare' },
  { label: 'Search', value: 'search' },
] as const

const activeTab = ref<'history' | 'flaky' | 'compare' | 'search'>('history')
const selectedBranch = ref('')
const minRuns = ref(3)
const compareBase = ref('')
const compareHead = ref('')
const searchQuery = ref('')
const searchOutcome = ref('')
const selectedTestName = ref<string | null>(null)

// Import dialog
const showImport = ref(false)
const importFile = ref<File | null>(null)
const trxFileInput = ref<HTMLInputElement | null>(null)
const importForm = reactive({ commitSha: '', branch: '', workflow: '', artifactName: '' })
const importError = ref<string | null>(null)
const importSuccess = ref<string | null>(null)

// ── Lifecycle ──────────────────────────────────────────────────────────────

onMounted(async () => {
  if (!projectsStore.currentProject) {
    await projectsStore.fetchProject(id)
  }
  await Promise.all([
    store.fetchHistory(id),
    store.fetchBranches(id),
  ])
})

// ── Methods ────────────────────────────────────────────────────────────────

function loadCurrentTab() {
  const branch = selectedBranch.value || undefined
  if (activeTab.value === 'history') {
    store.fetchHistory(id, { branch })
  } else if (activeTab.value === 'flaky') {
    store.fetchFlakyTests(id, { branch, minRuns: minRuns.value })
  }
}

watch(activeTab, (tab) => {
  const branch = selectedBranch.value || undefined
  if (tab === 'flaky' && store.flakyTests.length === 0) {
    store.fetchFlakyTests(id, { branch })
  }
})

function openTestDetail(fullName: string) {
  selectedTestName.value = fullName
  store.fetchTestCaseHistory(id, fullName, { branch: selectedBranch.value || undefined })
}

async function runCompare() {
  if (!compareBase.value || !compareHead.value) return
  await store.compareCommits(id, compareBase.value, compareHead.value)
}

async function runSearch() {
  if (!searchQuery.value) return
  await store.searchTests(id, searchQuery.value, {
    branch: selectedBranch.value || undefined,
    outcome: searchOutcome.value !== '' ? parseInt(searchOutcome.value) : undefined,
  })
}

function onFileChange(e: Event) {
  const files = (e.target as HTMLInputElement).files
  importFile.value = files?.[0] ?? null
}

async function submitImport() {
  if (!importFile.value) return
  importError.value = null
  importSuccess.value = null
  const result = await store.importTrx(id, importFile.value, {
    commitSha: importForm.commitSha || undefined,
    branch: importForm.branch || undefined,
    workflow: importForm.workflow || undefined,
    artifactName: importForm.artifactName || undefined,
  })
  if (result.success) {
    importSuccess.value = result.message
    // Refresh history
    await store.fetchHistory(id, { branch: selectedBranch.value || undefined })
    // Close after short delay
    setTimeout(() => {
      showImport.value = false
      importSuccess.value = null
      importFile.value = null
      if (trxFileInput.value) trxFileInput.value.value = ''
      importForm.commitSha = ''
      importForm.branch = ''
      importForm.workflow = ''
      importForm.artifactName = ''
    }, 1500)
  } else {
    importError.value = result.message
  }
}

// ── Helpers ────────────────────────────────────────────────────────────────

function barPct(count: number, total: number) {
  if (total === 0) return 0
  return Math.max(4, Math.round((count / total) * 100))
}

function durationBarPct(ms: number, entries: { durationMs: number }[]) {
  const max = Math.max(...entries.map(e => e.durationMs), 1)
  return Math.max(8, Math.round((ms / max) * 100))
}

function formatMs(ms: number) {
  if (ms < 1000) return `${Math.round(ms)}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`
  const mins = Math.floor(ms / 60000)
  const secs = Math.round((ms % 60000) / 1000)
  return `${mins}m ${secs}s`
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}

function outcomeClass(outcome: number) {
  if (outcome === 1) return 'bg-green-900/50 text-green-400'
  if (outcome === 2) return 'bg-red-900/50 text-red-400'
  if (outcome === 3) return 'bg-yellow-900/50 text-yellow-400'
  return 'bg-gray-800 text-gray-400'
}
</script>
