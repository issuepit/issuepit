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
  testHistoryBranch?: string | null
  testHistoryColorMode?: 'failure-rate' | 'pass-fail' | 'groups'
  testHistoryYAxis?: 'count' | 'duration'
  testHistoryXMode?: 'date' | 'runs'
  sortBy?: string
  countMode?: string
  projectFilter?: string
  failedHours?: number
  maxPerProject?: number
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

/** Returns true for dynamically added section IDs (e.g. 'kanban-1234567890-1', 'testHistory-1234567890-1'). */
export function isDynamicSectionId(id: string) { return /^(kanban|testHistory|cicdRuns|agentRuns|recentIssues)-\d/.test(id) }

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
        // Keep real section IDs plus virtual row-break IDs and dynamic section IDs
        const valid = parsed.order.filter(s => s in defaultConfigs || isVirtualId(s) || isDynamicSectionId(s))
        const missing = defaultOrder.filter(s => !valid.includes(s))
        layout.value.order = [...valid, ...missing]
      }
      if (parsed.configs) {
        for (const sid of defaultOrder) {
          if (parsed.configs[sid]) {
            layout.value.configs[sid] = { ...defaultConfigs[sid], ...parsed.configs[sid] }
          }
        }
        // Also restore configs for dynamic sections found in the order
        for (const sid of layout.value.order) {
          if (isDynamicSectionId(sid) && parsed.configs[sid]) {
            layout.value.configs[sid] = { ...parsed.configs[sid] }
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
        const valid = parsed.order.filter((s): s is string => typeof s === 'string' && (s in defaultConfigs || isVirtualId(s) || isDynamicSectionId(s)))
        const missing = defaultOrder.filter(s => !valid.includes(s))
        layout.value.order = [...valid, ...missing]
      }
      if (parsed.configs && typeof parsed.configs === 'object') {
        for (const sid of defaultOrder) {
          if (parsed.configs[sid] && typeof parsed.configs[sid] === 'object') {
            layout.value.configs[sid] = { ...defaultConfigs[sid], ...parsed.configs[sid] }
          }
        }
        // Also import configs for dynamic sections
        for (const sid of layout.value.order) {
          if (isDynamicSectionId(sid) && parsed.configs[sid] && typeof parsed.configs[sid] === 'object') {
            layout.value.configs[sid] = { ...parsed.configs[sid] }
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

  // ── Dynamic sections ────────────────────────────────────────────────────────

  let _dynamicSectionCounter = 0

  /** Adds a dynamic section with the given prefix (e.g. 'kanban') and initial config.
   *  Returns the generated unique section ID. */
  function addDynamicSection(prefix: string, config: LayoutSectionConfig): string {
    const id = `${prefix}-${Date.now()}-${++_dynamicSectionCounter}`
    layout.value.configs[id] = { ...config }
    layout.value.order = [...layout.value.order, id]
    return id
  }

  /** Removes a dynamic section from order and configs. No-op for static/virtual IDs. */
  function removeDynamicSection(id: string) {
    if (!isDynamicSectionId(id)) return
    layout.value.order = layout.value.order.filter(s => s !== id)
    layout.value.configs = Object.fromEntries(
      Object.entries(layout.value.configs).filter(([k]) => k !== id)
    ) as Record<string, LayoutSectionConfig>
  }

  // ── Drag & drop ────────────────────────────────────────────────────────────
  const dragSectionId = ref<string | null>(null)
  const dragHoverSid = ref<string | null>(null)
  /** Which gap zone is currently being hovered: { id: first-section-id, after: true=right-gap / false=left-gap } */
  const dragHoverGap = ref<{ id: string; after: boolean } | null>(null)
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
    // Keep dragHoverSid updated so tab/stack buttons highlight on hover
    if (!_dragGroup.includes(id)) dragHoverSid.value = id
  }

  function onDragEnter(e: DragEvent, id: string) {
    if (!dragSectionId.value) return
    // Only fire when truly entering the card from outside (not from a child element)
    if (e.currentTarget instanceof HTMLElement && e.currentTarget.contains(e.relatedTarget as Node)) return
    if (_dragGroup.includes(id)) return
    // Update hover sid for tab/stack button highlights; reorder is handled by gap sentinels
    dragHoverSid.value = id
  }

  /**
   * Called by the gap sentinel divs that live BETWEEN cards (not on the card itself).
   * `id` is the first section id of the adjacent card; `after=true` means the sentinel
   * is on the card's RIGHT (insert dragged group AFTER the card), `after=false` means LEFT (insert BEFORE).
   */
  function onGapDragEnter(_e: DragEvent, id: string, after: boolean) {
    if (!dragSectionId.value) return
    dragHoverGap.value = { id, after }
    dragHoverSid.value = null  // leaving the card — clear card-level hover

    const isSameGroup = _dragGroup.length > 1 && _dragGroup.includes(id)
    if (id === dragSectionId.value || isSameGroup || _dragGroup.includes(id)) return

    const order = layout.value.order
    if (order.indexOf(dragSectionId.value) === -1 || order.indexOf(id) === -1) return

    // If the target card belongs to a stack group, move relative to the whole group
    const targetStk = isVirtualId(id) ? null : (sectionCfg(id).stackGroup ?? null)
    let insertAnchor: string
    let insertAfterFinal: boolean
    if (targetStk) {
      const tGroup = order.filter(s => (sectionCfg(s).stackGroup ?? null) === targetStk)
      insertAnchor = after ? tGroup[tGroup.length - 1] : tGroup[0]
      insertAfterFinal = after
    } else {
      insertAnchor = id
      insertAfterFinal = after
    }

    const withoutGroup = order.filter(s => !_dragGroup.includes(s))
    let anchorIdx = withoutGroup.indexOf(insertAnchor)
    if (anchorIdx === -1) return
    if (insertAfterFinal) anchorIdx++
    const newOrder = [...withoutGroup]
    newOrder.splice(anchorIdx, 0, ..._dragGroup)
    if (newOrder.join(',') === order.join(',')) return
    layout.value.order = newOrder
  }

  function onGapDragLeave() {
    dragHoverGap.value = null
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
    dragHoverGap.value = null
    _dragSnapshot = null
    _dragGroup = []
  }

  function _onKeyDownDuringDrag(e: KeyboardEvent) {
    if (e.key === 'Escape' && dragSectionId.value) _dragEscaped = true
  }

  // Scroll the page with mouse wheel while dragging (browser suppresses native scroll during DnD)
  function _onWheelDuringDrag(e: WheelEvent) {
    if (!dragSectionId.value) return
    // Normalize delta across deltaMode values (0=pixel, 1=line, 2=page)
    let dy = e.deltaY
    if (e.deltaMode === 1) dy *= 20
    else if (e.deltaMode === 2) dy *= window.innerHeight
    window.scrollBy({ top: dy, behavior: 'instant' as ScrollBehavior })
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
    dragHoverGap,
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
    addDynamicSection,
    removeDynamicSection,
    onDragStart,
    onDragOver,
    onDragEnter,
    onDragEnd,
    onGapDragEnter,
    onGapDragLeave,
    toggleTabGroupWithNext,
    tabWithSection,
    toggleStackGroupWithNext,
    stackWithSection,
    getActiveTab,
    setActiveTab,
  }
}
