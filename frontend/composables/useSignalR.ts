/**
 * Composable for connecting to a SignalR hub.
 * Returns connection helpers and reactive message state.
 *
 * Usage:
 *   const { messages, connect, disconnect } = useSignalR('/hubs/agent-output')
 *   await connect()
 *   connection.value?.invoke('JoinIssue', issueId)
 */
export const useSignalR = (hubUrl: string) => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string

  // Dynamic import so SSR is not affected
  const connection = ref<import('@microsoft/signalr').HubConnection | null>(null)
  const isConnected = ref(false)
  const error = ref<string | null>(null)

  async function connect() {
    const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr')
    const conn = new HubConnectionBuilder()
      .withUrl(`${baseURL}${hubUrl}`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    conn.onclose(() => { isConnected.value = false })
    conn.onreconnected(() => { isConnected.value = true })

    try {
      await conn.start()
      connection.value = conn
      isConnected.value = true
      error.value = null
    } catch (e) {
      error.value = String(e)
    }
  }

  async function disconnect() {
    await connection.value?.stop()
    connection.value = null
    isConnected.value = false
  }

  onUnmounted(disconnect)

  return { connection, isConnected, error, connect, disconnect }
}
