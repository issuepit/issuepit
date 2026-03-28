<template>
  <div
    class="agent-terminal-wrapper bg-gray-950 rounded-xl overflow-hidden border border-gray-800 flex flex-col"
    :class="{
      'terminal-large': terminalSize === 'large',
      'terminal-fullscreen': terminalSize === 'fullscreen',
    }">
    <!-- Toolbar -->
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-800 bg-gray-900 select-none shrink-0">
      <div class="flex items-center gap-2">
        <span class="w-2.5 h-2.5 rounded-full" :class="connected ? 'bg-green-400 animate-pulse' : 'bg-gray-600'" />
        <span class="text-xs text-gray-400 font-mono">
          {{ connected ? 'Connected' : (retryCount > 0 ? `Retrying… (${retryCount}/${maxRetries})` : (error ? 'Error' : 'Connecting…')) }}
        </span>
      </div>
      <div class="flex items-center gap-2">
        <!-- Size toggle buttons -->
        <div class="flex items-center rounded border border-gray-700 overflow-hidden">
          <button
            class="text-xs px-2 py-0.5 transition-colors"
            :class="terminalSize === 'normal' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'"
            title="Normal size"
            @click="setSize('normal')">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <rect x="3" y="3" width="18" height="18" rx="2" stroke-width="2" />
            </svg>
          </button>
          <button
            class="text-xs px-2 py-0.5 border-l border-gray-700 transition-colors"
            :class="terminalSize === 'large' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'"
            title="Large (fill window)"
            @click="setSize('large')">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <rect x="2" y="4" width="20" height="16" rx="2" stroke-width="2" />
            </svg>
          </button>
          <button
            class="text-xs px-2 py-0.5 border-l border-gray-700 transition-colors"
            :class="terminalSize === 'fullscreen' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'"
            title="Fullscreen"
            @click="setSize('fullscreen')">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M4 8V5h3M17 5h3v3M20 16v3h-3M7 19H4v-3" />
            </svg>
          </button>
        </div>
        <button
          class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-0.5 rounded border border-gray-700 hover:border-gray-600"
          title="Clear terminal"
          @click="clearTerminal">
          Clear
        </button>
      </div>
    </div>

    <!-- Error banner -->
    <div v-if="error && !retryCount" class="px-4 py-2 bg-red-900/30 border-b border-red-800 text-xs text-red-400 shrink-0">
      {{ error }}
    </div>

    <!-- xterm container -->
    <div ref="xtermContainer" class="flex-1 min-h-0" />
  </div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, nextTick } from 'vue'
import type { Terminal } from '@xterm/xterm'
import type { FitAddon } from '@xterm/addon-fit'

const props = defineProps<{
  sessionId: string
  containerId: string | null | undefined
  active: boolean
}>()

const config = useRuntimeConfig()
const xtermContainer = ref<HTMLElement | null>(null)

let terminal: Terminal | null = null
let fitAddon: FitAddon | null = null
let ws: WebSocket | null = null
let resizeObserver: ResizeObserver | null = null
let retryTimer: ReturnType<typeof setTimeout> | null = null
let isMounted = false

const connected = ref(false)
const error = ref<string | null>(null)
const retryCount = ref(0)
const maxRetries = 10
const retryDelayMs = 3000

type TerminalSize = 'normal' | 'large' | 'fullscreen'
const terminalSize = ref<TerminalSize>('normal')

function clearRetryTimer() {
  if (retryTimer) {
    clearTimeout(retryTimer)
    retryTimer = null
  }
}

function buildWsUrl(): string {
  const base = (config.public.terminalBase as string).replace(/^http/, 'ws').replace(/\/$/, '')
  return `${base}/api/agent-sessions/${props.sessionId}/terminal`
}

async function initTerminal() {
  if (!xtermContainer.value) return

  // Dynamically import xterm to avoid SSR issues.
  const { Terminal: XTerminal } = await import('@xterm/xterm')
  const { FitAddon: XFitAddon } = await import('@xterm/addon-fit')
  const { WebLinksAddon: XWebLinksAddon } = await import('@xterm/addon-web-links')

  // Import xterm CSS only once.
  await import('@xterm/xterm/css/xterm.css')

  terminal = new XTerminal({
    theme: {
      background: '#030712',
      foreground: '#e5e7eb',
      cursor: '#60a5fa',
      selectionBackground: '#1e3a5f',
    },
    fontFamily: '"JetBrains Mono", "Fira Code", "Cascadia Code", monospace',
    fontSize: 13,
    lineHeight: 1.4,
    cursorBlink: true,
    scrollback: 5000,
  })

  fitAddon = new XFitAddon()
  const webLinksAddon = new XWebLinksAddon()
  terminal.loadAddon(fitAddon)
  terminal.loadAddon(webLinksAddon)
  terminal.open(xtermContainer.value)
  fitAddon.fit()

  // Observe container resize and relay PTY resize to server.
  resizeObserver = new ResizeObserver(() => {
    if (fitAddon && terminal) {
      fitAddon.fit()
      sendResize()
    }
  })
  resizeObserver.observe(xtermContainer.value)

  // Forward user keystrokes to Docker via WebSocket.
  terminal.onData((data) => {
    if (ws && ws.readyState === WebSocket.OPEN) {
      const bytes = new TextEncoder().encode(data)
      ws.send(bytes)
    }
  })

  if (props.containerId && props.active) {
    connectWebSocket()
  }
}

function sendResize() {
  if (ws && ws.readyState === WebSocket.OPEN && terminal) {
    ws.send(JSON.stringify({ type: 'resize', cols: terminal.cols, rows: terminal.rows }))
  }
}

function shouldRetry(): boolean {
  return isMounted && props.active && retryCount.value < maxRetries
}

function connectWebSocket() {
  clearRetryTimer()
  if (ws) {
    ws.close()
    ws = null
  }

  error.value = null
  const url = buildWsUrl()

  ws = new WebSocket(url)
  ws.binaryType = 'arraybuffer'

  ws.onopen = () => {
    connected.value = true
    retryCount.value = 0
    error.value = null
    // Send initial terminal size.
    if (terminal && fitAddon) {
      fitAddon.fit()
      sendResize()
    }
  }

  ws.onmessage = (event) => {
    if (!terminal) return
    if (event.data instanceof ArrayBuffer) {
      const bytes = new Uint8Array(event.data)
      terminal.write(bytes)
    } else if (typeof event.data === 'string') {
      terminal.write(event.data)
    }
  }

  ws.onerror = () => {
    // onerror is always followed by onclose; set connected=false here for immediate feedback.
    connected.value = false
  }

  ws.onclose = (ev) => {
    connected.value = false
    if (ev.code === 1000) {
      // Clean close — no retry needed.
      return
    }
    // Abnormal close: retry if we still have retries left and the session is active.
    if (shouldRetry()) {
      retryCount.value++
      error.value = null
      retryTimer = setTimeout(() => {
        if (shouldRetry()) connectWebSocket()
      }, retryDelayMs)
    }
    else {
      error.value = ev.reason
        ? `Connection closed: ${ev.reason}`
        : `Connection closed (code ${ev.code}). Reload the page to reconnect.`
    }
  }
}

function clearTerminal() {
  terminal?.clear()
}

function setSize(size: TerminalSize) {
  terminalSize.value = size
  // Re-fit after the DOM has updated to the new size.
  nextTick(() => {
    fitAddon?.fit()
    sendResize()
  })
}

// Reconnect when containerId becomes available.
watch(() => [props.containerId, props.active], ([newId, newActive]) => {
  if (newId && newActive && terminal) {
    retryCount.value = 0
    connectWebSocket()
  }
})

// Escape key exits fullscreen.
function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Escape' && terminalSize.value === 'fullscreen') {
    setSize('normal')
  }
}

onMounted(() => {
  isMounted = true
  initTerminal()
  document.addEventListener('keydown', onKeyDown)
})

onBeforeUnmount(() => {
  isMounted = false
  document.removeEventListener('keydown', onKeyDown)
  clearRetryTimer()
  resizeObserver?.disconnect()
  ws?.close()
  terminal?.dispose()
})
</script>

<style scoped>
.agent-terminal-wrapper :deep(.xterm) {
  padding: 8px;
  height: 100%;
}
.agent-terminal-wrapper :deep(.xterm-viewport) {
  width: 100% !important;
}
.agent-terminal-wrapper :deep(.xterm-screen) {
  width: 100% !important;
}

/* Large: fill most of the viewport height */
.terminal-large {
  height: calc(100vh - 200px);
}

/* Fullscreen: overlay entire viewport, keeping toolbar visible */
.terminal-fullscreen {
  position: fixed;
  inset: 0;
  z-index: 9999;
  border-radius: 0;
}
</style>
