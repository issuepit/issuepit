/**
 * Composable that adds @mention (agents/users) and #reference (runs/similar issues) support
 * to a textarea element.
 *
 * Usage:
 *   const mention = useMentionDropdown({ agents, users })
 *   // Bind to <textarea> via v-bind="mention.textareaBindings"
 *   // Render mention.dropdown when mention.isOpen is true
 */

export interface MentionItem {
  /** Unique value that will be inserted after the @ or # symbol. */
  value: string
  /** Human-readable label shown in the dropdown. */
  label: string
  /** Optional CSS dot class for colored indicators (e.g. online status). */
  dotClass?: string
}

export interface UseMentionDropdownOptions {
  /** Active agents to show in the @-mention dropdown. May be a reactive ref. */
  agents?: MentionItem[] | Ref<MentionItem[]> | ComputedRef<MentionItem[]>
  /** Active users to show in the @-mention dropdown. May be a reactive ref. */
  users?: MentionItem[] | Ref<MentionItem[]> | ComputedRef<MentionItem[]>
  /** Special # reference tokens (e.g. "similar", "runs"). */
  hashTokens?: MentionItem[]
}

export interface MentionDropdownState {
  /** Whether the dropdown is currently visible. */
  isOpen: Ref<boolean>
  /** Which trigger character opened the dropdown ('at' or 'hash'). */
  triggerChar: Ref<'at' | 'hash' | null>
  /** The partial search query typed after the trigger. */
  query: Ref<string>
  /** Filtered items to show. */
  items: ComputedRef<MentionItem[]>
  /** Currently highlighted item index. */
  activeIndex: Ref<number>
  /** Drop-in textarea event bindings (attach with v-bind="textareaBindings"). */
  textareaBindings: {
    onInput: (e: Event) => void
    onKeydown: (e: KeyboardEvent) => void
    onBlur: () => void
    ref: Ref<HTMLTextAreaElement | null>
  }
  /** Call this from the textarea's ref to register the element. */
  textareaRef: Ref<HTMLTextAreaElement | null>
  /** Confirm the currently highlighted item (called on Enter / click). */
  confirmSelection: (item?: MentionItem) => void
  /** Close the dropdown without selecting. */
  close: () => void
}

export function useMentionDropdown(options: UseMentionDropdownOptions): MentionDropdownState {
  const textareaRef = ref<HTMLTextAreaElement | null>(null)
  const isOpen = ref(false)
  const triggerChar = ref<'at' | 'hash' | null>(null)
  const query = ref('')
  const activeIndex = ref(0)

  // Start position (index in the text) of the current mention trigger character.
  let triggerPos = -1

  const items = computed<MentionItem[]>(() => {
    const q = query.value.toLowerCase()
    const agentsArr = isRef(options.agents) ? options.agents.value : (options.agents ?? [])
    const usersArr = isRef(options.users) ? options.users.value : (options.users ?? [])
    if (triggerChar.value === 'at') {
      const agents = agentsArr.filter(a => a.value.toLowerCase().includes(q) || a.label.toLowerCase().includes(q))
      const users = usersArr.filter(u => u.value.toLowerCase().includes(q) || u.label.toLowerCase().includes(q))
      return [...agents, ...users]
    }
    if (triggerChar.value === 'hash') {
      const tokens = options.hashTokens ?? []
      return tokens.filter(t => t.value.toLowerCase().includes(q) || t.label.toLowerCase().includes(q))
    }
    return []
  })

  function open(char: 'at' | 'hash', pos: number) {
    triggerChar.value = char
    triggerPos = pos
    query.value = ''
    activeIndex.value = 0
    isOpen.value = true
  }

  function close() {
    isOpen.value = false
    triggerChar.value = null
    query.value = ''
    triggerPos = -1
    activeIndex.value = 0
  }

  function confirmSelection(item?: MentionItem) {
    const el = textareaRef.value
    if (!el) return

    const selected = item ?? items.value[activeIndex.value]
    if (!selected) {
      close()
      return
    }

    const text = el.value
    const prefix = triggerChar.value === 'at' ? '@' : '#'
    const before = text.slice(0, triggerPos)
    const after = text.slice(triggerPos + 1 + query.value.length)
    const insertion = `${prefix}${selected.value} `

    el.value = before + insertion + after
    // Dispatch an input event so v-model updates.
    el.dispatchEvent(new Event('input', { bubbles: true }))
    // Move caret after the inserted text.
    const cursorPos = (before + insertion).length
    el.setSelectionRange(cursorPos, cursorPos)
    el.focus()

    close()
  }

  function onInput(e: Event) {
    const el = e.target as HTMLTextAreaElement
    const cursor = el.selectionStart ?? 0
    const text = el.value

    // Check if there's an active trigger between triggerPos and cursor.
    if (isOpen.value && triggerPos >= 0 && cursor > triggerPos) {
      const segment = text.slice(triggerPos + 1, cursor)
      // If there's a space or newline in the segment, close the dropdown.
      if (/[\s]/.test(segment)) {
        close()
        return
      }
      query.value = segment
      activeIndex.value = 0
      return
    }

    // Check if the character just typed is a trigger.
    if (cursor > 0) {
      const charBefore = text[cursor - 1]
      // Only open if preceded by start-of-string, space, or newline.
      const charBeforeTrigger = cursor >= 2 ? text[cursor - 2] : ''
      const isWordBoundary = cursor === 1 || /[\s]/.test(charBeforeTrigger)

      if (charBefore === '@' && isWordBoundary) {
        open('at', cursor - 1)
        return
      }
      if (charBefore === '#' && isWordBoundary) {
        open('hash', cursor - 1)
        return
      }
    }

    if (isOpen.value) {
      close()
    }
  }

  function onKeydown(e: KeyboardEvent) {
    if (!isOpen.value) return

    if (e.key === 'ArrowDown') {
      e.preventDefault()
      activeIndex.value = (activeIndex.value + 1) % Math.max(1, items.value.length)
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      activeIndex.value = (activeIndex.value - 1 + Math.max(1, items.value.length)) % Math.max(1, items.value.length)
      return
    }
    if (e.key === 'Enter' && isOpen.value) {
      if (items.value.length > 0) {
        e.preventDefault()
        confirmSelection()
      }
      return
    }
    if (e.key === 'Escape') {
      e.preventDefault()
      close()
      return
    }
  }

  function onBlur() {
    // Small delay to allow click on dropdown item to register before closing.
    setTimeout(() => {
      if (isOpen.value) close()
    }, 150)
  }

  return {
    isOpen,
    triggerChar,
    query,
    items,
    activeIndex,
    textareaRef,
    textareaBindings: {
      onInput,
      onKeydown,
      onBlur,
      ref: textareaRef,
    },
    confirmSelection,
    close,
  }
}
