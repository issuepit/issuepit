/**
 * Composable to safely close a modal when clicking the backdrop overlay.
 *
 * Fixes the case where mousedown starts inside the modal content and mouseup
 * ends on the backdrop — in that scenario the modal must NOT close (e.g. when
 * the user selects text inside an input and the mouse drifts outside).
 *
 * Usage:
 *   const { onMousedown, onClick } = useBackdropClose(() => { showModal.value = false })
 *
 * Template:
 *   <div @mousedown="onMousedown" @click="onClick">…</div>
 */
export function useBackdropClose(onClose: () => void) {
  let mouseDownWasOnBackdrop = false

  function onMousedown(e: MouseEvent) {
    mouseDownWasOnBackdrop = e.target === e.currentTarget
  }

  function onClick(e: MouseEvent) {
    if (e.target !== e.currentTarget) return
    if (!mouseDownWasOnBackdrop) return
    onClose()
  }

  return { onMousedown, onClick }
}
