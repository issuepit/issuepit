<template>
  <CiCdStatusChip v-if="commitRuns.length" :runs="commitRuns" />
</template>

<script setup lang="ts">
import type { CiCdRun } from '~/types'

const props = defineProps<{
  commitSha: string
  runs: CiCdRun[]
}>()

const commitRuns = computed(() =>
  props.runs.filter(r => r.commitSha === props.commitSha),
)
</script>
