<template>
  <div>
    <div v-if="loading" class="text-xs text-gray-500 py-2 text-center">Loading…</div>
    <div v-else-if="!layout" class="text-xs text-gray-500 py-2 text-center italic">No graph data</div>
    <div v-else class="overflow-x-auto">
      <svg
        :width="layout.svgWidth"
        :height="layout.svgHeight"
        class="block overflow-visible"
        style="max-width: 360px">
        <!-- Edges (rendered behind job boxes) -->
        <path
          v-for="(edge, i) in layout.edges"
          :key="`e${i}`"
          :d="edge.path"
          fill="none"
          :stroke="edgeStroke(edge.targetStatus)"
          stroke-width="1.2"
          opacity="0.75" />
        <!-- Job boxes -->
        <g v-for="job in layout.jobs" :key="job.id">
          <rect
            :x="job.x"
            :y="job.y"
            :width="JOB_BOX_W"
            :height="JOB_BOX_H"
            rx="3"
            :fill="jobFill(job.status)"
            :stroke="jobStroke(job.status)"
            stroke-width="1" />
          <!-- Status dot -->
          <circle
            :cx="job.x + 7"
            :cy="job.y + JOB_BOX_H / 2"
            r="2.5"
            :fill="jobDotFill(job.status)" />
          <!-- Job name text -->
          <text
            :x="job.x + 15"
            :y="job.y + JOB_BOX_H / 2 + 3.5"
            font-size="9"
            :fill="jobTextFill(job.status)"
            class="select-none pointer-events-none">
            {{ job.name.length > MAX_JOB_NAME_CHARS ? job.name.slice(0, MAX_JOB_NAME_CHARS - 1) + '…' : job.name }}
          </text>
        </g>
      </svg>
    </div>
  </div>
</template>

<script setup lang="ts">
import { buildGraphJobIndexes, resolveLogJobId } from '~/utils/cicdLogMapper'
import type { WorkflowGraph, WorkflowJobNode } from '~/types'

const props = defineProps<{
  runId: string
}>()

const api = useApi()

// ── Constants ────────────────────────────────────────────────────────────────

const JOB_BOX_W = 96
const JOB_BOX_H = 22
const ROW_GAP = 6
const COL_GAP = 28
const PADDING = 6
const MAX_JOB_NAME_CHARS = 13

// ── Types ─────────────────────────────────────────────────────────────────────

type JobStatus = 'succeeded' | 'failed' | 'running' | 'pending'

interface MiniGraphJob {
  id: string
  name: string
  x: number
  y: number
  status: JobStatus
}

interface MiniGraphEdge {
  path: string
  targetStatus: JobStatus
}

interface MiniGraphLayout {
  svgWidth: number
  svgHeight: number
  jobs: MiniGraphJob[]
  edges: MiniGraphEdge[]
}

// ── Data fetching ─────────────────────────────────────────────────────────────

/** Cache keyed by runId to avoid refetching on repeated hovers. */
const graphCache = new Map<string, WorkflowGraph | null>()
const statusCache = new Map<string, Map<string, JobStatus>>()

const graph = ref<WorkflowGraph | null>(null)
const jobStatuses = ref<Map<string, JobStatus>>(new Map())
const loading = ref(false)

async function loadData() {
  if (graphCache.has(props.runId)) {
    graph.value = graphCache.get(props.runId) ?? null
    jobStatuses.value = statusCache.get(props.runId) ?? new Map()
    return
  }

  loading.value = true
  graph.value = null
  jobStatuses.value = new Map()

  try {
    const [fetchedGraph, rawStatuses] = await Promise.all([
      api.get<WorkflowGraph>(`/api/cicd-runs/${props.runId}/graph`),
      api.get<{ logJobId: string; status: string }[]>(`/api/cicd-runs/${props.runId}/job-statuses`),
    ])

    graphCache.set(props.runId, fetchedGraph)
    graph.value = fetchedGraph

    // Map log job IDs → graph node IDs using the cicdLogMapper utility.
    const indexes = buildGraphJobIndexes(fetchedGraph.jobs)
    const statusMap = new Map<string, JobStatus>()
    for (const { logJobId, status } of rawStatuses) {
      const graphNodeId = resolveLogJobId(logJobId, indexes)
      const s = status as JobStatus
      // Keep the most severe status if the same node appears under multiple log IDs.
      const prev = statusMap.get(graphNodeId)
      if (!prev || SEVERITY[s] > SEVERITY[prev]) {
        statusMap.set(graphNodeId, s)
      }
    }
    statusCache.set(props.runId, statusMap)
    jobStatuses.value = statusMap
  }
  catch {
    graphCache.set(props.runId, null)
    graph.value = null
  }
  finally {
    loading.value = false
  }
}

const SEVERITY: Record<JobStatus, number> = { failed: 3, running: 2, succeeded: 1, pending: 0 }

watch(() => props.runId, loadData, { immediate: true })

// ── Layout computation ────────────────────────────────────────────────────────

const layout = computed((): MiniGraphLayout | null => {
  if (!graph.value || !graph.value.jobs.length) return null

  const jobs = graph.value.jobs
  const edges = graph.value.edges

  // BFS column assignment (topological ordering)
  const colMap = new Map<string, number>()
  const rowInCol = new Map<string, number>()
  const inDegree = new Map<string, number>()
  for (const job of jobs) inDegree.set(job.id, 0)
  for (const edge of edges) inDegree.set(edge.to, (inDegree.get(edge.to) ?? 0) + 1)

  const queue: string[] = []
  for (const job of jobs) if ((inDegree.get(job.id) ?? 0) === 0) queue.push(job.id)
  while (queue.length) {
    const id = queue.shift()!
    const col = colMap.get(id) ?? 0
    for (const edge of edges) {
      if (edge.from === id) {
        const nextCol = Math.max(colMap.get(edge.to) ?? 0, col + 1)
        colMap.set(edge.to, nextCol)
        const newDeg = (inDegree.get(edge.to) ?? 1) - 1
        inDegree.set(edge.to, newDeg)
        if (newDeg === 0) queue.push(edge.to)
      }
    }
  }

  // Assign rows within each column (preserving order from the jobs array)
  const colRowCounts = new Map<number, number>()
  for (const job of jobs) {
    const col = colMap.get(job.id) ?? 0
    const row = colRowCounts.get(col) ?? 0
    rowInCol.set(job.id, row)
    colRowCounts.set(col, row + 1)
  }

  const numCols = Math.max(...Array.from(colMap.values()), 0) + 1
  const maxRows = Math.max(...Array.from(colRowCounts.values()), 1)

  const svgWidth = PADDING * 2 + numCols * JOB_BOX_W + (numCols - 1) * COL_GAP
  const svgHeight = PADDING * 2 + maxRows * JOB_BOX_H + (maxRows - 1) * ROW_GAP

  // Compute absolute positions for each job
  const positions = new Map<string, { x: number; y: number }>()
  const layoutJobs: MiniGraphJob[] = jobs.map((job: WorkflowJobNode) => {
    const col = colMap.get(job.id) ?? 0
    const row = rowInCol.get(job.id) ?? 0
    const x = PADDING + col * (JOB_BOX_W + COL_GAP)
    const y = PADDING + row * (JOB_BOX_H + ROW_GAP)
    positions.set(job.id, { x, y })
    return {
      id: job.id,
      name: job.name,
      x,
      y,
      status: jobStatuses.value.get(job.id) ?? 'pending',
    }
  })

  // Draw bezier edge paths
  const layoutEdges: MiniGraphEdge[] = []
  for (const edge of edges) {
    const from = positions.get(edge.from)
    const to = positions.get(edge.to)
    if (!from || !to) continue

    const x1 = from.x + JOB_BOX_W
    const y1 = from.y + JOB_BOX_H / 2
    const x2 = to.x
    const y2 = to.y + JOB_BOX_H / 2
    const cx = (x2 - x1) * 0.45
    const path = `M ${x1} ${y1} C ${x1 + cx} ${y1} ${x2 - cx} ${y2} ${x2} ${y2}`
    layoutEdges.push({ path, targetStatus: jobStatuses.value.get(edge.to) ?? 'pending' })
  }

  return { svgWidth, svgHeight, jobs: layoutJobs, edges: layoutEdges }
})

// ── Color helpers ─────────────────────────────────────────────────────────────

function jobFill(s: JobStatus) {
  if (s === 'succeeded') return '#14532d'
  if (s === 'failed') return '#450a0a'
  if (s === 'running') return '#1e3a5f'
  return '#1f2937'
}

function jobStroke(s: JobStatus) {
  if (s === 'succeeded') return '#22c55e'
  if (s === 'failed') return '#ef4444'
  if (s === 'running') return '#3b82f6'
  return '#374151'
}

function jobTextFill(s: JobStatus) {
  if (s === 'succeeded') return '#bbf7d0'
  if (s === 'failed') return '#fecaca'
  if (s === 'running') return '#bfdbfe'
  return '#9ca3af'
}

function jobDotFill(s: JobStatus) {
  if (s === 'succeeded') return '#22c55e'
  if (s === 'failed') return '#ef4444'
  if (s === 'running') return '#60a5fa'
  return '#4b5563'
}

function edgeStroke(s: JobStatus) {
  if (s === 'succeeded') return '#22c55e'
  if (s === 'failed') return '#ef4444'
  if (s === 'running') return '#3b82f6'
  return '#4b5563'
}
</script>
