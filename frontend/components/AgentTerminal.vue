<template>
  <div class="agent-terminal-wrapper bg-gray-950 rounded-xl overflow-hidden border border-gray-800 flex flex-col">
    <!-- Toolbar -->
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-800 bg-gray-900 select-none">
      <div class="flex items-center gap-2">
        <span class="w-2.5 h-2.5 rounded-full" :class="connected ? 'bg-green-400 animate-pulse' : 'bg-gray-600'" />
        <span class="text-xs text-gray-400 font-mono">{{ connected ? 'Connected' : (error ? 'Error' : 'Connecting…') }}</span>
      </div>
      <div class="flex items-center gap-2">
        <button
          class="text-xs text-gray-500 hover:text-gray-300 transition-colors px-2 py-0.5 rounded border border-gray-700 hover:border-gray-600"
          title="Clear terminal"
          @click="clearTerminal">
          Clear
        </button>
      </div>
    </div>

    <!-- Error banner -->
    <div v-if="error" class="px-4 py-2 bg-red-900/30 border-b border-red-800 text-xs text-red-400">
      {{ error }}
    </div>

    <!-- xterm container -->
    <div ref="xtermContainer" class="flex-1" style="min-height: 400px;" />
  </div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
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

const connected = ref(false)
const error = ref<string | null>(null)

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
      if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: 'resize', cols: terminal.cols, rows: terminal.rows }))
      }
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

function connectWebSocket() {
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
    // Send initial terminal size.
    if (terminal && fitAddon) {
      fitAddon.fit()
      ws!.send(JSON.stringify({ type: 'resize', cols: terminal.cols, rows: terminal.rows }))
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
    connected.value = false
    error.value = 'WebSocket connection failed. The terminal server may not be available.'
  }

  ws.onclose = (ev) => {
    connected.value = false
    if (ev.code !== 1000) {
      error.value = `Connection closed (${ev.code}${ev.reason ? ': ' + ev.reason : ''})`
    }
  }
}

function clearTerminal() {
  terminal?.clear()
}

// Reconnect when containerId becomes available.
watch(() => [props.containerId, props.active], ([newId, newActive]) => {
  if (newId && newActive && terminal) {
    connectWebSocket()
  }
})

onMounted(() => {
  initTerminal()
})

onBeforeUnmount(() => {
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
</style>
