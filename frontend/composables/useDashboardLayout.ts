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
  | { type: 'rowbreak'; sid: string }

/** Returns true for virtual IDs (e.g. row-break placeholders) that are not real sections. */
function isVirtualId(id: string) { return id.startsWith('rowbreak-') }

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
    if (isVirtualId(s)) return { hidden: false, width: '', displayMode: '', maxItems: 0, tabGroup: null, stackGroup: null }
    return layout.value.configs[s] ?? { ...defaultConfigs[s] }
  }

  function updateCfg(s: string, patch: Partial<LayoutSectionConfig>) {
    if (isVirtualId(s)) return
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
        // Keep real section IDs plus virtual row-break IDs
        const valid = parsed.order.filter(s => s in defaultConfigs || isVirtualId(s))
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

  function exportLayoutJson(): string {
    return JSON.stringify(layout.value, null, 2)
  }

  function importLayoutJson(json: string): boolean {
    try {
      const parsed = JSON.parse(json) as Partial<LayoutData>
      if (Array.isArray(parsed.order) && parsed.order.length) {
        // Only accept string items; skip any non-string values to prevent injection
        const valid = parsed.order.filter((s): s is string => typeof s === 'string' && (s in defaultConfigs || isVirtualId(s)))
        const missing = defaultOrder.filter(s => !valid.includes(s))
        layout.value.order = [...valid, ...missing]
      }
      if (parsed.configs && typeof parsed.configs === 'object') {
        for (const sid of defaultOrder) {
          if (parsed.configs[sid] && typeof parsed.configs[sid] === 'object') {
            layout.value.configs[sid] = { ...defaultConfigs[sid], ...parsed.configs[sid] }
          }
        }
      }
      return true
    } catch {
      return false
    }
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

  // ── Row break ───────────────────────────────────────────────────────────────
  let _rowBreakCounter = 0

  function addRowBreak(): string {
    const rowBreakId = `rowbreak-${Date.now()}-${++_rowBreakCounter}`
    layout.value.order = [...layout.value.order, rowBreakId]
    return rowBreakId
  }

  function removeRowBreak(id: string) {
    layout.value.order = layout.value.order.filter(s => s !== id)
  }

  // ── Drag & drop ────────────────────────────────────────────────────────────
  const dragSectionId = ref<string | null>(null)
  const dragHoverSid = ref<string | null>(null)
  let _dragSnapshot: string | null = null
  // Cached drag group (all sections being moved together); populated on dragstart
  let _dragGroup: string[] = []
  let _dragEscaped = false

  function captureSnapshot() {
    _dragSnapshot = JSON.stringify(layout.value)
  }

  function onDragStart(e: DragEvent, id: string) {
    _dragEscaped = false
    dragSectionId.value = id
    // Only capture a new snapshot if one hasn't already been taken (e.g. before a pre-insert)
    if (!_dragSnapshot) _dragSnapshot = JSON.stringify(layout.value)
    // Cache the full drag group for use during dragover (avoids repeated filter calls)
    const stk = isVirtualId(id) ? null : (sectionCfg(id).stackGroup ?? null)
    _dragGroup = stk
      ? layout.value.order.filter(s => (sectionCfg(s).stackGroup ?? null) === stk)
      : [id]
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move'
      e.dataTransfer.setData('text/plain', id)
    }
  }

  function onDragOver(_e: DragEvent, id: string) {
    if (!dragSectionId.value) return
    // Only highlight cards outside the drag group; reorder is handled by onDragEnter
    if (!_dragGroup.includes(id)) dragHoverSid.value = id
  }

  function onDragEnter(e: DragEvent, id: string) {
    if (!dragSectionId.value) return
    // Only fire when truly entering the card from outside (not from a child element)
    if (e.currentTarget instanceof HTMLElement && e.currentTarget.contains(e.relatedTarget as Node)) return
    // Only reorder if entering from the true gap (not from another card).
    // When the cursor moves directly from Card A to Card B there is no gap crossing,
    // so the relatedTarget will be inside a [data-drag-card] element — skip reorder in that case.
    if ((e.relatedTarget as Element | null)?.closest('[data-drag-card]')) return

    const isSameGroup = _dragGroup.length > 1 && _dragGroup.includes(id)
    // Skip reorder if dragging over ourselves or our group
    if (id === dragSectionId.value || isSameGroup || _dragGroup.includes(id)) return

    // Determine which side the cursor entered from (left = insert before, right = insert after)
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    const enteredFromLeft = e.clientX - rect.left < rect.width / 2

    const order = layout.value.order
    if (order.indexOf(dragSectionId.value) === -1 || order.indexOf(id) === -1) return

    // If the target card belongs to a stack group, move relative to the whole group
    const targetStk = isVirtualId(id) ? null : (sectionCfg(id).stackGroup ?? null)
    let insertAnchor: string
    let insertAfter: boolean
    if (targetStk) {
      const tGroup = order.filter(s => (sectionCfg(s).stackGroup ?? null) === targetStk)
      if (enteredFromLeft) {
        insertAnchor = tGroup[0]
        insertAfter = false
      } else {
        insertAnchor = tGroup[tGroup.length - 1]
        insertAfter = true
      }
    } else {
      insertAnchor = id
      insertAfter = enteredFromLeft  // entered from left = insert after target (drag source moves right)
    }

    // Remove the drag group from the order, then reinsert at the anchor position
    const withoutGroup = order.filter(s => !_dragGroup.includes(s))
    let anchorIdx = withoutGroup.indexOf(insertAnchor)
    if (anchorIdx === -1) return
    if (insertAfter) anchorIdx++
    const newOrder = [...withoutGroup]
    newOrder.splice(anchorIdx, 0, ..._dragGroup)
    if (newOrder.join(',') === order.join(',')) return
    layout.value.order = newOrder
  }

  function onDragEnd(_e?: DragEvent) {
    // Only restore pre-drag layout on explicit ESC cancel (tracked via keydown listener).
    // Dropping outside a valid target keeps the current placeholder position.
    if (_dragEscaped && _dragSnapshot) {
      layout.value = JSON.parse(_dragSnapshot)
    }
    _dragEscaped = false
    dragSectionId.value = null
    dragHoverSid.value = null
    _dragSnapshot = null
    _dragGroup = []
  }

  function _onKeyDownDuringDrag(e: KeyboardEvent) {
    if (e.key === 'Escape' && dragSectionId.value) _dragEscaped = true
  }

  // Scroll the page with mouse wheel while dragging (browser suppresses native scroll during DnD)
  function _onWheelDuringDrag(e: WheelEvent) {
    if (dragSectionId.value) window.scrollBy(0, e.deltaY)
  }

  onMounted(() => {
    window.addEventListener('wheel', _onWheelDuringDrag, { passive: true })
    window.addEventListener('keydown', _onKeyDownDuringDrag)
  })
  onUnmounted(() => {
    window.removeEventListener('wheel', _onWheelDuringDrag)
    window.removeEventListener('keydown', _onKeyDownDuringDrag)
  })

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
      // Row-break virtual IDs are always included (they are invisible in normal mode)
      if (isVirtualId(s)) return true
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

      // Virtual row-break — pass through without grouping
      if (isVirtualId(sid)) {
        items.push({ type: 'rowbreak', sid })
        i++
        continue
      }

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
    exportLayoutJson,
    importLayoutJson,
    enterDraftMode,
    saveDraftMode,
    cancelDraftMode,
    resetLayout,
    addRowBreak,
    captureSnapshot,
    removeRowBreak,
    onDragStart,
    onDragOver,
    onDragEnter,
    onDragEnd,
    toggleTabGroupWithNext,
    tabWithSection,
    toggleStackGroupWithNext,
    stackWithSection,
    getActiveTab,
    setActiveTab,
  }
}
