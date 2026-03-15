import { ref, computed, onMounted, onUnmounted } from 'vue'

export interface LayoutSectionConfig {
  hidden: boolean
  width: string
  displayMode: string
  maxItems: number
  tabGroup: string | null
  stackGroup: string | null
  chartDays?: number
  chartHeightKey?: string
  selectedBoardId?: string
}

interface LayoutData {
  order: string[]
  configs: Record<string, LayoutSectionConfig>
}

type RenderItem =
  | { type: 'section'; sid: string }
  | { type: 'tabgroup'; key: string; sections: string[] }
  | { type: 'stackgroup'; key: string; sections: string[] }

export function useDashboardLayout(options: {
  defaultOrder: string[]
  defaultConfigs: Record<string, LayoutSectionConfig>
  storageKey: string
  filterVisible?: (sid: string, cfg: LayoutSectionConfig) => boolean
}) {
  const { defaultOrder, defaultConfigs, storageKey, filterVisible } = options

  const layout = ref<LayoutData>({
    order: [...defaultOrder],
    configs: JSON.parse(JSON.stringify(defaultConfigs)) as Record<string, LayoutSectionConfig>,
  })

  const isDraftMode = ref(false)
  let _snapshot: string | null = null

  function sectionCfg(s: string): LayoutSectionConfig {
    return layout.value.configs[s] ?? { ...defaultConfigs[s] }
  }

  function updateCfg(s: string, patch: Partial<LayoutSectionConfig>) {
    layout.value.configs[s] = { ...sectionCfg(s), ...patch }
  }

  function hideSection(s: string) { updateCfg(s, { hidden: true }) }
  function showSection(s: string) { updateCfg(s, { hidden: false }) }

  function loadLayout() {
    if (!import.meta.client) return
    try {
      const saved = localStorage.getItem(storageKey)
      if (!saved) return
      const parsed = JSON.parse(saved) as Partial<LayoutData>
      if (Array.isArray(parsed.order) && parsed.order.length) {
        const valid = parsed.order.filter(s => s in defaultConfigs)
        const missing = defaultOrder.filter(s => !valid.includes(s))
        layout.value.order = [...valid, ...missing]
      }
      if (parsed.configs) {
        for (const sid of defaultOrder) {
          if (parsed.configs[sid]) {
            layout.value.configs[sid] = { ...defaultConfigs[sid], ...parsed.configs[sid] }
          }
        }
      }
    } catch { /* ignore */ }
  }

  function saveLayout() {
    if (!import.meta.client) return
    localStorage.setItem(storageKey, JSON.stringify(layout.value))
  }

  function enterDraftMode() {
    _snapshot = JSON.stringify(layout.value)
    isDraftMode.value = true
  }

  function saveDraftMode() {
    saveLayout()
    isDraftMode.value = false
  }

  function cancelDraftMode() {
    if (_snapshot) layout.value = JSON.parse(_snapshot)
    isDraftMode.value = false
  }

  function resetLayout() {
    layout.value = {
      order: [...defaultOrder],
      configs: JSON.parse(JSON.stringify(defaultConfigs)) as Record<string, LayoutSectionConfig>,
    }
  }

  // ── Drag & drop ────────────────────────────────────────────────────────────
  const dragSectionId = ref<string | null>(null)
  const dragHoverSid = ref<string | null>(null)
  let _dragSnapshot: string | null = null

  function onDragStart(e: DragEvent, id: string) {
    dragSectionId.value = id
    _dragSnapshot = JSON.stringify(layout.value)
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move'
      e.dataTransfer.setData('text/plain', id)
    }
  }

  function onDragOver(e: DragEvent, id: string) {
    // Only highlight other cards, not the one being dragged
    if (id !== dragSectionId.value) dragHoverSid.value = id
    // Don't reorder when hovering over a config bar — it's a drop target for tab/stack grouping.
    // Skipping the reorder keeps the config bar stable so the user can drop onto the buttons.
    if ((e.target as HTMLElement)?.closest('[data-no-reorder]')) return
    if (!dragSectionId.value || id === dragSectionId.value) return
    // Only reorder when cursor is near the left or right edge of the card — these are the
    // "gap" zones between cards in the horizontal grid flow. Hovering over the card's main body
    // (center area) only highlights the card for Tab/Stack grouping without moving anything.
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    const fromLeft = e.clientX - rect.left
    const fromRight = rect.right - e.clientX
    const edgePx = Math.min(40, rect.width * 0.25)
    if (fromLeft > edgePx && fromRight > edgePx) return

    const order = layout.value.order
    const from = order.indexOf(dragSectionId.value)
    const to = order.indexOf(id)
    if (from === -1 || to === -1 || from === to) return

    // If the target section is part of a stack group, treat the group as a single unit:
    // when dragging downward (from < to), skip to after the last member so we never
    // insert the dragged card between stack-group members.
    let insertAt = to
    const stk = sectionCfg(order[to]).stackGroup
    if (stk !== null && from < to) {
      for (let gi = to; gi < order.length; gi++) {
        if (sectionCfg(order[gi]).stackGroup === stk) insertAt = gi
        else break
      }
    }

    if (from === insertAt) return
    const newOrder = [...order]
    newOrder.splice(from, 1)
    // insertAt was computed in original-index space; after removing `from` (< insertAt),
    // elements shift left by 1, so splice(insertAt, …) correctly places the item after
    // the last group member in the new array.
    newOrder.splice(insertAt, 0, dragSectionId.value)
    layout.value.order = newOrder
  }

  function onDragEnd(e?: DragEvent) {
    // ESC or aborted drag: dropEffect is 'none' → restore the pre-drag layout
    if (e && e.dataTransfer?.dropEffect === 'none' && _dragSnapshot) {
      layout.value = JSON.parse(_dragSnapshot)
    }
    dragSectionId.value = null
    dragHoverSid.value = null
    _dragSnapshot = null
  }

  // Scroll the page with mouse wheel while dragging (browser suppresses native scroll during DnD)
  function _onWheelDuringDrag(e: WheelEvent) {
    if (dragSectionId.value) window.scrollBy(0, e.deltaY)
  }

  onMounted(() => window.addEventListener('wheel', _onWheelDuringDrag, { passive: true }))
  onUnmounted(() => window.removeEventListener('wheel', _onWheelDuringDrag))

  // ── Tab group logic ─────────────────────────────────────────────────────────
  let _tabGroupCounter = 0

  function toggleTabGroupWithNext(sid: string) {
    const cfg = sectionCfg(sid)
    if (cfg.tabGroup !== null) {
      const grp = cfg.tabGroup
      updateCfg(sid, { tabGroup: null, stackGroup: null })
      for (const s of layout.value.order) {
        if (s !== sid && sectionCfg(s).tabGroup === grp) updateCfg(s, { tabGroup: null, stackGroup: null })
      }
    } else {
      const visible = layout.value.order.filter(s => !sectionCfg(s).hidden)
      const idx = visible.indexOf(sid)
      const nextSid = idx >= 0 && idx + 1 < visible.length ? visible[idx + 1] : null
      if (!nextSid) return
      const nextCfg = sectionCfg(nextSid)
      const grp = nextCfg.tabGroup ?? `grp-${++_tabGroupCounter}`
      updateCfg(sid, { tabGroup: grp, stackGroup: null })
      if (nextCfg.tabGroup === null) updateCfg(nextSid, { tabGroup: grp, stackGroup: null })
    }
  }

  function tabWithSection(targetSid: string, droppedSid: string) {
    if (targetSid === droppedSid) return
    const existingGrp = sectionCfg(targetSid).tabGroup
    // If already in the same group, nothing to do
    if (existingGrp && existingGrp === sectionCfg(droppedSid).tabGroup) return

    // Find the last member of target's existing group (so we insert after all current group members)
    let insertAfterSid = targetSid
    if (existingGrp) {
      const order = layout.value.order
      const targetIdx = order.indexOf(targetSid)
      let lastIdx = targetIdx
      for (let j = targetIdx + 1; j < order.length; j++) {
        if (sectionCfg(order[j]).tabGroup === existingGrp) lastIdx = j
        else break
      }
      insertAfterSid = order[lastIdx]
    }

    // Move droppedSid to be right after the last group member
    const newOrder = layout.value.order.filter(s => s !== droppedSid)
    const insertIdx = newOrder.indexOf(insertAfterSid)
    if (insertIdx === -1) return
    newOrder.splice(insertIdx + 1, 0, droppedSid)
    layout.value.order = newOrder

    // Remove dropped's existing group membership (if different from target's group)
    const dGrp = sectionCfg(droppedSid).tabGroup
    if (dGrp && dGrp !== existingGrp) {
      for (const s of layout.value.order) {
        if (sectionCfg(s).tabGroup === dGrp) updateCfg(s, { tabGroup: null, stackGroup: null })
      }
    }

    const grp = existingGrp ?? `grp-${++_tabGroupCounter}`
    if (!existingGrp) updateCfg(targetSid, { tabGroup: grp, stackGroup: null })
    updateCfg(droppedSid, { tabGroup: grp, stackGroup: null })
  }

  // ── Stack group logic ───────────────────────────────────────────────────────
  let _stackGroupCounter = 0

  function toggleStackGroupWithNext(sid: string) {
    const cfg = sectionCfg(sid)
    if (cfg.stackGroup !== null) {
      const grp = cfg.stackGroup
      updateCfg(sid, { stackGroup: null, tabGroup: null })
      for (const s of layout.value.order) {
        if (s !== sid && sectionCfg(s).stackGroup === grp) updateCfg(s, { stackGroup: null, tabGroup: null })
      }
    } else {
      const visible = layout.value.order.filter(s => !sectionCfg(s).hidden)
      const idx = visible.indexOf(sid)
      const nextSid = idx >= 0 && idx + 1 < visible.length ? visible[idx + 1] : null
      if (!nextSid) return
      const nextCfg = sectionCfg(nextSid)
      const grp = nextCfg.stackGroup ?? `stk-${++_stackGroupCounter}`
      updateCfg(sid, { stackGroup: grp, tabGroup: null })
      if (nextCfg.stackGroup === null) updateCfg(nextSid, { stackGroup: grp, tabGroup: null })
    }
  }

  function stackWithSection(targetSid: string, droppedSid: string) {
    if (targetSid === droppedSid) return
    const existingGrp = sectionCfg(targetSid).stackGroup
    if (existingGrp && existingGrp === sectionCfg(droppedSid).stackGroup) return

    let insertAfterSid = targetSid
    if (existingGrp) {
      const order = layout.value.order
      const targetIdx = order.indexOf(targetSid)
      let lastIdx = targetIdx
      for (let j = targetIdx + 1; j < order.length; j++) {
        if (sectionCfg(order[j]).stackGroup === existingGrp) lastIdx = j
        else break
      }
      insertAfterSid = order[lastIdx]
    }

    const newOrder = layout.value.order.filter(s => s !== droppedSid)
    const insertIdx = newOrder.indexOf(insertAfterSid)
    if (insertIdx === -1) return
    newOrder.splice(insertIdx + 1, 0, droppedSid)
    layout.value.order = newOrder

    const dGrp = sectionCfg(droppedSid).stackGroup
    if (dGrp && dGrp !== existingGrp) {
      for (const s of layout.value.order) {
        if (sectionCfg(s).stackGroup === dGrp) updateCfg(s, { stackGroup: null, tabGroup: null })
      }
    }

    const grp = existingGrp ?? `stk-${++_stackGroupCounter}`
    if (!existingGrp) updateCfg(targetSid, { stackGroup: grp, tabGroup: null })
    updateCfg(droppedSid, { stackGroup: grp, tabGroup: null })
  }

  // ── Active tab tracking ─────────────────────────────────────────────────────
  const activeTabInGroup = ref<Record<string, string>>({})

  function getActiveTab(key: string, sections: string[]): string {
    return activeTabInGroup.value[key] || sections[0]
  }

  function setActiveTab(key: string, sid: string) {
    activeTabInGroup.value[key] = sid
  }

  // ── Rendered items ──────────────────────────────────────────────────────────
  const renderedItems = computed((): RenderItem[] => {
    const visible = layout.value.order.filter(s => {
      const c = sectionCfg(s)
      if (isDraftMode.value) return true
      if (c.hidden) return false
      if (filterVisible) return filterVisible(s, c)
      return true
    })
    const items: RenderItem[] = []
    let i = 0
    while (i < visible.length) {
      const sid = visible[i]
      const cfg = sectionCfg(sid)

      // Tab group
      if (cfg.tabGroup !== null) {
        const grp = cfg.tabGroup
        const grpSids: string[] = [sid]
        let j = i + 1
        while (j < visible.length && sectionCfg(visible[j]).tabGroup === grp) {
          grpSids.push(visible[j])
          j++
        }
        if (grpSids.length > 1) {
          items.push({ type: 'tabgroup', key: grp, sections: grpSids })
          i = j
          continue
        }
      }

      // Stack group
      if (cfg.stackGroup !== null) {
        const grp = cfg.stackGroup
        const grpSids: string[] = [sid]
        let j = i + 1
        while (j < visible.length && sectionCfg(visible[j]).stackGroup === grp) {
          grpSids.push(visible[j])
          j++
        }
        if (grpSids.length > 1) {
          items.push({ type: 'stackgroup', key: grp, sections: grpSids })
          i = j
          continue
        }
      }

      items.push({ type: 'section', sid })
      i++
    }
    return items
  })

  const hiddenSections = computed(() =>
    new Set(layout.value.order.filter(s => layout.value.configs[s]?.hidden))
  )

  return {
    layout,
    isDraftMode,
    dragSectionId,
    dragHoverSid,
    renderedItems,
    hiddenSections,
    activeTabInGroup,
    sectionCfg,
    updateCfg,
    hideSection,
    showSection,
    loadLayout,
    saveLayout,
    enterDraftMode,
    saveDraftMode,
    cancelDraftMode,
    resetLayout,
    onDragStart,
    onDragOver,
    onDragEnd,
    toggleTabGroupWithNext,
    tabWithSection,
    toggleStackGroupWithNext,
    stackWithSection,
    getActiveTab,
    setActiveTab,
  }
}
