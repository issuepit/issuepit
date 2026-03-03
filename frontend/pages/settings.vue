<template>
  <div class="p-8 max-w-2xl">
    <h1 class="text-2xl font-bold text-white mb-2">Visuals</h1>
    <p class="text-gray-400 mb-8">Configure display preferences and log rendering.</p>

    <div class="space-y-6">
      <!-- Appearance -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Appearance</h2>
        <p class="text-sm text-gray-500">Dark mode is the only theme currently supported.</p>
      </div>

      <!-- Log Display -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Log Display</h2>

        <!-- ANSI color toggle -->
        <label class="flex items-center justify-between cursor-pointer mb-5">
          <div>
            <span class="text-sm font-medium text-gray-300">ANSI color rendering</span>
            <p class="text-xs text-gray-500 mt-0.5">Render ANSI escape codes as colored text in CI/CD logs. When off, escape codes are stripped so they don't appear as artifacts.</p>
          </div>
          <button
            :class="prefs.ansiColors ? 'bg-brand-600' : 'bg-gray-700'"
            class="relative inline-flex w-10 h-6 shrink-0 rounded-full transition-colors focus:outline-none ml-4"
            role="switch"
            :aria-checked="prefs.ansiColors"
            @click="setAnsiColors(!prefs.ansiColors)">
            <span
              :class="prefs.ansiColors ? 'translate-x-4' : 'translate-x-0'"
              class="inline-block w-5 h-5 mt-0.5 ml-0.5 rounded-full bg-white shadow transform transition-transform" />
          </button>
        </label>

        <!-- Regex color rules -->
        <div>
          <h3 class="text-sm font-medium text-gray-300 mb-1">Custom line color rules</h3>
          <p class="text-xs text-gray-500 mb-3">
            Define regex patterns to colorize matching log lines (e.g. <code class="text-gray-400">\[success\]</code> → green).
            The first matching rule wins. Colors are applied as the line text color.
          </p>

          <!-- Existing rules -->
          <div v-if="prefs.logColorRules.length" class="space-y-2 mb-3">
            <div
              v-for="rule in prefs.logColorRules"
              :key="rule.id"
              class="flex items-center gap-2 bg-gray-800 rounded-lg px-3 py-2">
              <!-- Color swatch -->
              <span class="w-4 h-4 rounded shrink-0 border border-gray-600" :style="{ background: rule.color }" />
              <span class="font-mono text-xs text-gray-300 flex-1 truncate" :title="rule.pattern">{{ rule.pattern }}</span>
              <span class="text-xs text-gray-500 font-mono">{{ rule.color }}</span>
              <button class="ml-1 text-gray-500 hover:text-red-400 transition-colors" title="Remove rule" @click="removeLogColorRule(rule.id)">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>
          <p v-else class="text-xs text-gray-600 mb-3">No rules yet.</p>

          <!-- Add rule form -->
          <div v-if="showAddRule" class="bg-gray-800 rounded-lg p-3 mb-2 space-y-2">
            <div class="flex gap-2">
              <div class="flex-1">
                <label class="block text-xs text-gray-500 mb-1">Regex pattern</label>
                <input
                  v-model="newRulePattern"
                  type="text"
                  placeholder='e.g. \[error\]'
                  class="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1 text-xs text-gray-300 font-mono placeholder-gray-600 focus:outline-none focus:border-brand-500" />
              </div>
              <div class="w-28">
                <label class="block text-xs text-gray-500 mb-1">Color</label>
                <div class="flex items-center gap-1.5">
                  <input
                    v-model="newRuleColor"
                    type="color"
                    class="w-7 h-7 rounded cursor-pointer bg-transparent border-0 p-0" />
                  <input
                    v-model="newRuleColor"
                    type="text"
                    placeholder="#ffffff"
                    class="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1 text-xs text-gray-300 font-mono placeholder-gray-600 focus:outline-none focus:border-brand-500" />
                </div>
              </div>
            </div>
            <p v-if="newRuleError" class="text-xs text-red-400">{{ newRuleError }}</p>
            <div class="flex justify-end gap-2">
              <button class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-1" @click="cancelAddRule">Cancel</button>
              <button class="text-xs bg-brand-600 hover:bg-brand-500 text-white rounded px-3 py-1 transition-colors" @click="saveNewRule">Add rule</button>
            </div>
          </div>

          <button
            v-if="!showAddRule"
            class="flex items-center gap-1.5 text-xs text-brand-400 hover:text-brand-300 transition-colors"
            @click="showAddRule = true">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add rule
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const { prefs, setAnsiColors, addLogColorRule, removeLogColorRule } = useUserPreferences()

// --- Add rule form ---
const showAddRule = ref(false)
const newRulePattern = ref('')
const newRuleColor = ref('#66ff66')
const newRuleError = ref('')

function cancelAddRule() {
  showAddRule.value = false
  newRulePattern.value = ''
  newRuleColor.value = '#66ff66'
  newRuleError.value = ''
}

function saveNewRule() {
  newRuleError.value = ''
  const pattern = newRulePattern.value.trim()
  if (!pattern) {
    newRuleError.value = 'Pattern is required.'
    return
  }
  try {
    // Validate the regex
    new RegExp(pattern)
  } catch {
    newRuleError.value = 'Invalid regular expression.'
    return
  }
  addLogColorRule({ pattern, color: newRuleColor.value })
  cancelAddRule()
}
</script>
