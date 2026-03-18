<script setup lang="ts">
/**
 * DateDisplay – shared date/time rendering component.
 *
 * Props:
 *   date       – ISO string or Date object to display
 *   mode       – 'absolute'  : show a formatted date/time string
 *                'relative'  : show "2 minutes ago" style text with tooltip
 *                'auto'      : relative for recent (<7d), absolute beyond that
 *   resolution – 'date'      : "16. Jan 2025"
 *                'datetime'  : "16. Jan 2025, 14:30"   (default)
 *                'time'      : "14:30"
 */

const props = withDefaults(defineProps<{
  date: string | Date
  mode?: 'absolute' | 'relative' | 'auto'
  resolution?: 'date' | 'datetime' | 'time'
}>(), {
  mode: 'absolute',
  resolution: 'datetime',
})

const now = ref(Date.now())

// Refresh every minute so relative labels stay live.
let _timer: ReturnType<typeof setInterval> | null = null
onMounted(() => { _timer = setInterval(() => { now.value = Date.now() }, 60_000) })
onUnmounted(() => { if (_timer) clearInterval(_timer) })

function parsedDate(): Date {
  return typeof props.date === 'string' ? new Date(props.date) : props.date
}

/** European-style absolute label, e.g. "16. Jan 2025" or "16. Jan 2025, 14:30" or "14:30" */
function absoluteLabel(d: Date): string {
  if (props.resolution === 'time') {
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  }
  const day = d.getDate()
  const month = d.toLocaleDateString('de-DE', { month: 'short' })
  // Capitalize first letter
  const monthStr = month.charAt(0).toUpperCase() + month.slice(1).replace('.', '')
  const year = d.getFullYear()
  const dateStr = `${day}. ${monthStr} ${year}`
  if (props.resolution === 'date') return dateStr
  const timeStr = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  return `${dateStr}, ${timeStr}`
}

/** "2 minutes ago", "3 hours ago", "5 days ago" … */
function relativeLabel(d: Date): string {
  const ms = now.value - d.getTime()
  const s = Math.floor(ms / 1000)
  if (s < 5) return 'just now'
  if (s < 60) return `${s} seconds ago`
  const m = Math.floor(s / 60)
  if (m < 60) return m === 1 ? '1 minute ago' : `${m} minutes ago`
  const h = Math.floor(m / 60)
  if (h < 24) return h === 1 ? '1 hour ago' : `${h} hours ago`
  const dd = Math.floor(h / 24)
  if (dd < 7) return dd === 1 ? 'yesterday' : `${dd} days ago`
  return absoluteLabel(d)
}

const d = computed(() => parsedDate())

const displayText = computed(() => {
  if (props.mode === 'relative') return relativeLabel(d.value)
  if (props.mode === 'auto') {
    const ageDays = (now.value - d.value.getTime()) / (1000 * 60 * 60 * 24)
    return ageDays < 7 ? relativeLabel(d.value) : absoluteLabel(d.value)
  }
  return absoluteLabel(d.value)
})

/** Full datetime tooltip: always shown for relative mode, or when abbreviated */
const tooltipText = computed(() => {
  if (props.mode === 'absolute' && props.resolution === 'datetime') return undefined
  // Build a full datetime string for the tooltip
  const fullD = new Date(d.value)
  const day = fullD.getDate()
  const month = fullD.toLocaleDateString('de-DE', { month: 'long' })
  const monthStr = month.charAt(0).toUpperCase() + month.slice(1)
  const year = fullD.getFullYear()
  const timeStr = `${String(fullD.getHours()).padStart(2, '0')}:${String(fullD.getMinutes()).padStart(2, '0')}`
  return `${day}. ${monthStr} ${year}, ${timeStr}`
})
</script>

<template>
  <time
    :datetime="d.toISOString()"
    :title="tooltipText"
    class="whitespace-nowrap"
  >{{ displayText }}</time>
</template>
