<template>
  <div class="p-8 max-w-2xl">
    <PageBreadcrumb :items="[
      { label: 'System', to: '/settings', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
      { label: 'Settings', to: '/settings', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
    ]" class="mb-8" />

    <div class="space-y-6">
      <!-- Appearance -->
      <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
        <h2 class="font-semibold text-white mb-4">Appearance</h2>

        <!-- Theme selector -->
        <div class="mb-5">
          <h3 class="text-sm font-medium text-gray-300 mb-1">Theme</h3>
          <p class="text-xs text-gray-500 mb-3">
            Choose your preferred colour scheme. Changes marked <span class="italic">browser only</span>
            apply just to this device and are not saved to your profile.
          </p>

          <!-- Theme cards -->
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2 mb-3">
            <button
              v-for="t in THEMES"
              :key="t.id"
              :class="[
                'flex flex-col items-start gap-1 rounded-lg border px-3 py-2.5 text-left transition-colors',
                activeTheme === t.id
                  ? 'border-brand-500 bg-brand-600/10'
                  : 'border-gray-700 hover:border-gray-600 bg-gray-800',
              ]"
              @click="selectTheme(t.id)"
            >
              <!-- Colour preview swatches -->
              <span class="flex gap-1 mb-1">
                <span :style="swatchStyle(t.id, 'bg')"   class="w-3 h-3 rounded-full border border-black/20" />
                <span :style="swatchStyle(t.id, 'card')" class="w-3 h-3 rounded-full border border-black/20" />
                <span :style="swatchStyle(t.id, 'text')" class="w-3 h-3 rounded-full border border-black/20" />
                <span :style="swatchStyle(t.id, 'accent')" class="w-3 h-3 rounded-full border border-black/20" />
              </span>
              <span class="text-xs font-medium text-gray-200">{{ t.label }}</span>
              <span class="text-xs text-gray-500">{{ t.description }}</span>
            </button>
          </div>

          <!-- Browser-local override note -->
          <div v-if="browserOverride" class="flex items-center justify-between bg-gray-800 rounded-lg px-3 py-2 text-xs">
            <span class="text-gray-400">
              Browser override active: <strong class="text-gray-200">{{ browserOverride }}</strong>
            </span>
            <button class="text-brand-400 hover:text-brand-300 transition-colors" @click="clearBrowserOverride">
              Clear override
            </button>
          </div>
          <div v-else-if="auth.user?.theme" class="text-xs text-gray-500 mt-1">
            Profile theme: <strong class="text-gray-400">{{ auth.user.theme }}</strong> — saved to your account.
          </div>
          <div v-else class="text-xs text-gray-500 mt-1">
            Using system default. Select a theme above to save it to your profile.
          </div>
        </div>

        <!-- Save-to-profile vs browser-only toggle -->
        <div class="flex items-center justify-between border-t border-gray-800 pt-4">
          <div>
            <span class="text-sm font-medium text-gray-300">Save to profile</span>
            <p class="text-xs text-gray-500 mt-0.5">When on, the selected theme is saved to your account and applied on all devices.</p>
          </div>
          <button
            :class="saveToProfile ? 'bg-brand-600' : 'bg-gray-700'"
            class="relative inline-flex w-10 h-6 shrink-0 rounded-full transition-colors focus:outline-none ml-4"
            role="switch"
            :aria-checked="saveToProfile"
            @click="saveToProfile = !saveToProfile">
            <span
              :class="saveToProfile ? 'translate-x-4' : 'translate-x-0'"
              class="inline-block w-5 h-5 mt-0.5 ml-0.5 rounded-full bg-white shadow transform transition-transform" />
          </button>
        </div>

        <p v-if="themeSavedMsg" class="text-xs text-green-400 mt-2">{{ themeSavedMsg }}</p>
        <p v-if="themeError" class="text-xs text-red-400 mt-2">{{ themeError }}</p>
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
import type { ThemeId } from '~/composables/useTheme'
import { useAuthStore } from '~/stores/auth'

const auth = useAuthStore()
const { activeTheme, browserOverride, setBrowserTheme, saveProfileTheme, THEMES } = useTheme()

// --- Theme selection ---
const saveToProfile = ref(true)
const themeSavedMsg = ref('')
const themeError = ref('')

async function selectTheme(id: ThemeId) {
  themeError.value = ''
  themeSavedMsg.value = ''
  if (saveToProfile.value) {
    try {
      await saveProfileTheme(id)
      themeSavedMsg.value = 'Theme saved to your profile.'
    } catch {
      themeError.value = 'Failed to save theme. The change was applied locally only.'
      setBrowserTheme(id)
    }
  } else {
    setBrowserTheme(id)
  }
}

function clearBrowserOverride() {
  setBrowserTheme(null)
}

/** Hardcoded preview swatches so they don't depend on the active theme's CSS variables.
 * Keep these in sync with the RGB values in assets/themes.css. */
const SWATCH_COLORS: Record<string, { bg: string; card: string; text: string; accent: string }> = {
  dark:         { bg: '#030712', card: '#111827', text: '#d1d5db', accent: '#4c6ef5' },
  dim:          { bg: '#0b0b0f', card: '#161b22', text: '#adb5bd', accent: '#4c6ef5' },
  'dark-accent':{ bg: '#030712', card: '#111827', text: '#d1d5db', accent: '#9333ea' },
  'dark-square':{ bg: '#030712', card: '#111827', text: '#d1d5db', accent: '#4c6ef5' },
  light:        { bg: '#ffffff', card: '#f8fafc', text: '#334155', accent: '#2563eb' },
}

function swatchStyle(themeId: string, part: 'bg' | 'card' | 'text' | 'accent') {
  const c = SWATCH_COLORS[themeId]
  if (!c) return {}
  return { background: c[part] }
}

// --- Log Display ---
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
