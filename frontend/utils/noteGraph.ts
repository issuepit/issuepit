import type { NoteGraphResponse } from '~/types'
import { NoteLinkType } from '~/types'

export type GraphNodeKind = 'note' | 'issue' | 'todo' | 'project'

export interface NoteGraphVisualNode {
  id: string
  title: string
  kind: GraphNodeKind
  noteId?: string
  notebookId?: string
  targetEntityId?: string
}

export interface NoteGraphVisualEdge {
  sourceId: string
  targetId: string
}

export function buildNoteGraphVisualData(data: NoteGraphResponse) {
  const nodeMap = new Map<string, NoteGraphVisualNode>()
  const edges: NoteGraphVisualEdge[] = []

  for (const node of data.nodes) {
    nodeMap.set(node.id, {
      id: node.id,
      title: node.title,
      kind: 'note',
      noteId: node.id,
      notebookId: node.notebookId,
    })
  }

  for (const edge of data.edges) {
    if (!nodeMap.has(edge.sourceNoteId)) continue

    if (edge.targetType === NoteLinkType.Note) {
      if (!edge.targetNoteId || !nodeMap.has(edge.targetNoteId)) continue
      edges.push({ sourceId: edge.sourceNoteId, targetId: edge.targetNoteId })
      continue
    }

    const target = getOrCreateExternalNode(nodeMap, edge.targetType, edge.linkText, edge.targetEntityId)
    edges.push({ sourceId: edge.sourceNoteId, targetId: target.id })
  }

  return {
    nodes: [...nodeMap.values()],
    edges,
  }
}

function getOrCreateExternalNode(
  nodeMap: Map<string, NoteGraphVisualNode>,
  targetType: NoteLinkType,
  linkText: string,
  targetEntityId?: string,
) {
  const kind = toNodeKind(targetType)
  const keyPart = targetEntityId || linkText.trim().toLowerCase() || 'unknown'
  const id = `${kind}:${keyPart}`
  const existing = nodeMap.get(id)
  if (existing) return existing

  const title = linkText.trim() || kind
  const node: NoteGraphVisualNode = {
    id,
    title,
    kind,
    targetEntityId,
  }
  nodeMap.set(id, node)
  return node
}

function toNodeKind(type: NoteLinkType): GraphNodeKind {
  switch (type) {
    case NoteLinkType.Issue: return 'issue'
    case NoteLinkType.Todo: return 'todo'
    case NoteLinkType.Project: return 'project'
    default: return 'note'
  }
}
