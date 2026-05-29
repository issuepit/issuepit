<template>
  <div ref="graphContainer" class="w-full h-full bg-gray-900/50 rounded-lg border border-gray-700 relative overflow-hidden">
    <div v-if="loading" class="absolute inset-0 flex items-center justify-center text-gray-500">
      Loading graph...
    </div>
    <div v-else-if="!graphData || graphData.nodes.length === 0" class="absolute inset-0 flex items-center justify-center text-gray-500">
      No notes to visualize. Create notes with [[wiki links]] to see note, issue, todo and project links.
    </div>
    <svg v-else ref="svgEl" class="w-full h-full" @mousedown="onMouseDown" @mousemove="onMouseMove" @mouseup="onMouseUp">
      <!-- Edges -->
      <line v-for="(edge, i) in positionedEdges" :key="'e' + i"
        :x1="edge.x1" :y1="edge.y1" :x2="edge.x2" :y2="edge.y2"
        stroke="#4b5563" stroke-width="1.5" stroke-opacity="0.6" />
      <!-- Nodes -->
      <g v-for="node in positionedNodes" :key="node.id"
        :transform="`translate(${node.x}, ${node.y})`"
        class="cursor-pointer"
        @click="navigateToNote(node.id)">
        <circle r="20" :fill="nodeColor(node)" stroke="#6366f1" stroke-width="2" />
        <text dy="30" text-anchor="middle" class="fill-gray-300 text-xs select-none">
          {{ truncate(node.title, 20) }}
        </text>
      </g>
    </svg>
  </div>
</template>

<script setup lang="ts">
import type { NoteGraphResponse } from '~/types'
import { buildNoteGraphVisualData, type NoteGraphVisualNode } from '~/utils/noteGraph'

const props = defineProps<{
  notebookId?: string
}>()

const store = useNotesStore()
const router = useRouter()

const graphContainer = ref<HTMLElement | null>(null)
const svgEl = ref<SVGElement | null>(null)
const loading = ref(true)
const graphData = ref<NoteGraphResponse | null>(null)

interface PositionedNode extends NoteGraphVisualNode {
  x: number
  y: number
}

interface PositionedEdge {
  x1: number
  y1: number
  x2: number
  y2: number
}

const positionedNodes = ref<PositionedNode[]>([])
const positionedEdges = ref<PositionedEdge[]>([])

function layoutGraph(data: NoteGraphResponse) {
  const visual = buildNoteGraphVisualData(data)
  if (!visual.nodes.length) return

  const width = graphContainer.value?.clientWidth ?? 800
  const height = graphContainer.value?.clientHeight ?? 600
  const centerX = width / 2
  const centerY = height / 2
  const radius = Math.min(width, height) * 0.35

  // Simple circular layout
  const nodes: PositionedNode[] = visual.nodes.map((node, i) => {
    const angle = (2 * Math.PI * i) / visual.nodes.length - Math.PI / 2
    return {
      ...node,
      x: centerX + radius * Math.cos(angle),
      y: centerY + radius * Math.sin(angle),
    }
  })

  const nodeMap = new Map(nodes.map(n => [n.id, n]))
  const edges: PositionedEdge[] = []

  for (const edge of visual.edges) {
    const source = nodeMap.get(edge.sourceId)
    const target = nodeMap.get(edge.targetId)
    if (source && target) {
      edges.push({ x1: source.x, y1: source.y, x2: target.x, y2: target.y })
    }
  }

  positionedNodes.value = nodes
  positionedEdges.value = edges
}

function nodeColor(node: NoteGraphVisualNode) {
  switch (node.kind) {
    case 'issue': return '#7f1d1d'
    case 'todo': return '#1e3a8a'
    case 'project': return '#4c1d95'
    default: {
      const hash = (node.notebookId ?? '').split('').reduce((acc, c) => acc + c.charCodeAt(0), 0)
      const hue = hash % 360
      return `hsl(${hue}, 60%, 30%)`
    }
  }
}

function truncate(text: string, maxLen: number) {
  return text.length > maxLen ? text.substring(0, maxLen) + '…' : text
}

function navigateToNote(id: string) {
  const node = positionedNodes.value.find(n => n.id === id)
  if (!node) return

  if (node.kind === 'note' && node.noteId) {
    router.push(`/notes/${node.noteId}`)
    return
  }

  if (node.kind === 'project') {
    router.push(node.targetEntityId ? `/projects/${node.targetEntityId}` : '/projects')
    return
  }

  router.push(node.kind === 'todo' ? '/todos' : '/issues')
}

// Basic pan support
const dragging = ref(false)
function onMouseDown() { dragging.value = true }
function onMouseMove() { /* pan logic can be added later */ }
function onMouseUp() { dragging.value = false }

onMounted(async () => {
  loading.value = true
  await store.fetchGraph(props.notebookId)
  graphData.value = store.graphData
  if (graphData.value) {
    layoutGraph(graphData.value)
  }
  loading.value = false
})

watch(() => props.notebookId, async () => {
  loading.value = true
  await store.fetchGraph(props.notebookId)
  graphData.value = store.graphData
  if (graphData.value) {
    layoutGraph(graphData.value)
  }
  loading.value = false
})
</script>
