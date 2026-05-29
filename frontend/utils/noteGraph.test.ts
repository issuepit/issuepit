import { describe, expect, it } from 'vitest'
import { NoteLinkType } from '~/types'
import { buildNoteGraphVisualData } from './noteGraph'

describe('buildNoteGraphVisualData', () => {
  it('includes linked note edges when target note exists', () => {
    const graph = buildNoteGraphVisualData({
      nodes: [
        { id: 'n1', title: 'Note 1', slug: 'note-1', notebookId: 'nb' },
        { id: 'n2', title: 'Note 2', slug: 'note-2', notebookId: 'nb' },
      ],
      edges: [
        { sourceNoteId: 'n1', targetType: NoteLinkType.Note, targetNoteId: 'n2', linkText: 'Note 2' },
      ],
    })

    expect(graph.nodes).toHaveLength(2)
    expect(graph.edges).toEqual([{ sourceId: 'n1', targetId: 'n2' }])
  })

  it('creates external issue and todo nodes for entity links', () => {
    const graph = buildNoteGraphVisualData({
      nodes: [
        { id: 'n1', title: 'Note 1', slug: 'note-1', notebookId: 'nb' },
      ],
      edges: [
        { sourceNoteId: 'n1', targetType: NoteLinkType.Issue, targetEntityId: 'issue-1', linkText: 'issue:1' },
        { sourceNoteId: 'n1', targetType: NoteLinkType.Todo, targetEntityId: 'todo-1', linkText: 'todo:1' },
      ],
    })

    expect(graph.nodes.map(n => n.kind)).toEqual(expect.arrayContaining(['note', 'issue', 'todo']))
    expect(graph.edges).toEqual(
      expect.arrayContaining([
        { sourceId: 'n1', targetId: 'issue:issue-1' },
        { sourceId: 'n1', targetId: 'todo:todo-1' },
      ])
    )
  })
})
