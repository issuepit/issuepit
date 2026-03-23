<template>
  <div>
    <!-- Manual textarea -->
    <textarea
      :value="modelValue"
      rows="4"
      placeholder="deploy&#10;build:upload-artifacts&#10;Notify Slack"
      class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y"
      @input="$emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <p class="text-xs text-gray-500 mt-1">
      One entry per line. Use <code class="bg-gray-800 px-1 rounded">step-name</code> to skip a step globally,
      or <code class="bg-gray-800 px-1 rounded">job-id:step-name</code> to skip it only within a specific job.
      Each entry is passed as a separate <code class="bg-gray-800 px-1 rounded">--skip-step</code> argument to act.
    </p>

    <!-- Wizard button -->
    <div class="mt-3">
      <button
        type="button"
        class="flex items-center gap-1.5 text-xs text-brand-400 hover:text-brand-300 transition-colors"
        :disabled="loadingSuggestions"
        @click="toggleWizard"
      >
        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
            d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.362.362A3.001 3.001 0 0012 15a3 3 0 01-2.975-2.625l-.363-.363zM12 15v2" />
        </svg>
        {{ showWizard ? 'Hide wizard' : 'Use wizard (auto-complete from recent runs)' }}
        <span v-if="loadingSuggestions" class="ml-1 w-3.5 h-3.5 border border-brand-400 border-t-transparent rounded-full animate-spin" />
      </button>
    </div>

    <!-- Wizard panel -->
    <div v-if="showWizard" class="mt-3 border border-gray-700 rounded-lg overflow-hidden">
      <div v-if="loadingSuggestions" class="flex items-center justify-center py-6">
        <div class="w-5 h-5 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
      </div>

      <div v-else-if="suggestions.length === 0" class="px-4 py-4 text-sm text-gray-500">
        No job/step data from recent runs yet. Run a CI/CD pipeline first to populate suggestions.
      </div>

      <template v-else>
        <div class="px-4 py-2 bg-gray-800/60 border-b border-gray-700 flex items-center justify-between">
          <span class="text-xs font-medium text-gray-400">Select steps to skip from recent runs</span>
          <div class="flex items-center gap-3">
            <button
              type="button"
              class="text-xs text-brand-400 hover:text-brand-300 transition-colors"
              @click="selectAll"
            >Select all</button>
            <button
              type="button"
              class="text-xs text-gray-400 hover:text-gray-200 transition-colors"
              @click="clearSelection"
            >Clear</button>
            <button
              type="button"
              class="text-xs bg-brand-600 hover:bg-brand-500 text-white px-2 py-0.5 rounded transition-colors"
              :disabled="wizardSelected.size === 0"
              @click="applyWizard"
            >Apply ({{ wizardSelected.size }})</button>
          </div>
        </div>

        <div class="divide-y divide-gray-800 max-h-72 overflow-y-auto">
          <div v-for="job in suggestions" :key="job.jobId" class="px-4 py-2">
            <!-- Job row -->
            <div class="flex items-center gap-2 mb-1.5">
              <input
                :ref="(el) => setJobCheckboxRef(job.jobId, el as HTMLInputElement | null)"
                :id="`job-${job.jobId}`"
                type="checkbox"
                class="w-3.5 h-3.5 rounded bg-gray-800 border-gray-600 text-brand-500 focus:ring-brand-500"
                :checked="isJobChecked(job)"
                @change="toggleJob(job)"
              />
              <label :for="`job-${job.jobId}`" class="text-xs font-semibold text-gray-200 font-mono cursor-pointer">
                {{ job.jobId }}
              </label>
            </div>
            <!-- Step rows -->
            <div class="ml-5 space-y-1">
              <div v-for="step in job.steps" :key="`${job.jobId}:${step}`" class="flex items-center gap-2">
                <input
                  :id="`step-${job.jobId}-${step}`"
                  type="checkbox"
                  class="w-3 h-3 rounded bg-gray-800 border-gray-600 text-brand-500 focus:ring-brand-500"
                  :checked="wizardSelected.has(`${job.jobId}:${step}`)"
                  @change="toggleStep(job.jobId, step)"
                />
                <label :for="`step-${job.jobId}-${step}`" class="text-xs text-gray-400 font-mono cursor-pointer hover:text-gray-200">
                  {{ step }}
                </label>
              </div>
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { StepSuggestionJob } from '~/types'

const props = defineProps<{
  modelValue: string
  projectId?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const api = useApi()

const showWizard = ref(false)
const loadingSuggestions = ref(false)
const suggestions = ref<StepSuggestionJob[]>([])
const wizardSelected = ref<Set<string>>(new Set())

// Map of jobId → checkbox element reference, used to set the indeterminate DOM property.
const jobCheckboxRefs = new Map<string, HTMLInputElement>()

function setJobCheckboxRef(jobId: string, el: HTMLInputElement | null) {
  if (el) jobCheckboxRefs.set(jobId, el)
  else jobCheckboxRefs.delete(jobId)
}

// Keep indeterminate state in sync whenever selection changes.
watchEffect(() => {
  for (const job of suggestions.value) {
    const el = jobCheckboxRefs.get(job.jobId)
    if (el) el.indeterminate = isJobIndeterminate(job)
  }
})

async function loadSuggestions() {
  if (!props.projectId) return
  loadingSuggestions.value = true
  try {
    suggestions.value = await api.get<StepSuggestionJob[]>(
      `/api/cicd-runs/step-suggestions?projectId=${props.projectId}`,
    )
  } catch {
    suggestions.value = []
  } finally {
    loadingSuggestions.value = false
  }
}

async function toggleWizard() {
  showWizard.value = !showWizard.value
  if (showWizard.value) {
    // Load suggestions first so we can resolve bare step names against jobs.
    if (suggestions.value.length === 0 && props.projectId) {
      await loadSuggestions()
    }

    // Pre-check items already present in the textarea.
    // Supports both `job:step` format (exact match) and bare step names (match against all jobs).
    const existing = (props.modelValue || '')
      .split('\n')
      .map(l => l.trim())
      .filter(l => l.length > 0)

    const preSelected = new Set<string>()
    for (const entry of existing) {
      if (entry.includes(':')) {
        // Already `job:step` format — add as-is.
        preSelected.add(entry)
      } else if (suggestions.value.length > 0) {
        // Bare step name — check it for every job that contains this step.
        // If no suggestions loaded, skip resolution (the textarea retains the bare value).
        for (const job of suggestions.value) {
          if (job.steps.includes(entry)) {
            preSelected.add(`${job.jobId}:${entry}`)
          }
        }
      }
      // Note: if suggestions are empty and entry is bare, it won't appear pre-checked,
      // but the textarea still contains the original value and will not be lost on Apply.
    }
    wizardSelected.value = preSelected
  }
}

function isJobChecked(job: StepSuggestionJob): boolean {
  return job.steps.every(s => wizardSelected.value.has(`${job.jobId}:${s}`))
}

function isJobIndeterminate(job: StepSuggestionJob): boolean {
  return !isJobChecked(job) && job.steps.some(s => wizardSelected.value.has(`${job.jobId}:${s}`))
}

function toggleJob(job: StepSuggestionJob) {
  const allChecked = isJobChecked(job)
  const next = new Set(wizardSelected.value)
  for (const step of job.steps) {
    if (allChecked) next.delete(`${job.jobId}:${step}`)
    else next.add(`${job.jobId}:${step}`)
  }
  wizardSelected.value = next
}

function toggleStep(jobId: string, step: string) {
  const key = `${jobId}:${step}`
  const next = new Set(wizardSelected.value)
  if (next.has(key)) next.delete(key)
  else next.add(key)
  wizardSelected.value = next
}

function selectAll() {
  const next = new Set<string>()
  for (const job of suggestions.value)
    for (const step of job.steps)
      next.add(`${job.jobId}:${step}`)
  wizardSelected.value = next
}

function clearSelection() {
  wizardSelected.value = new Set()
}

function applyWizard() {
  const existing = (props.modelValue || '')
    .split('\n')
    .map(l => l.trim())
    .filter(l => l.length > 0)

  const toAdd = Array.from(wizardSelected.value).filter(e => !existing.includes(e))
  const merged = [...existing, ...toAdd].join('\n')
  emit('update:modelValue', merged)
  wizardSelected.value = new Set()
  showWizard.value = false
}
</script>
