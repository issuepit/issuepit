/// Composable for making requests to the Notes API service (separate from the main API).
export const useNotesApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.notesApiBase as string

  const get = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'GET', credentials: 'include', ...opts })

  const post = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'POST', body, credentials: 'include', ...opts })

  const put = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PUT', body, credentials: 'include', ...opts })

  const del = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'DELETE', credentials: 'include', ...opts })

  /**
   * Submit an OT delta for a note (CRDT collaborative editing).
   * The server transforms the delta against concurrent operations and returns the
   * confirmed operation with its assigned SequenceNumber.
   */
  const submitOperation = (
    noteId: string,
    delta: string,
    baseSequence: number,
    clientId: string,
  ) =>
    post<NoteOperationResponse>(`/api/notes/${noteId}/operations`, {
      delta,
      baseSequence,
      clientId,
    })

  /**
   * Fetch all operations for a note since the given sequence number.
   * Used by the polling loop to apply remote changes.
   */
  const getOperations = (noteId: string, since: number) =>
    get<NoteOperationResponse[]>(`/api/notes/${noteId}/operations`, {
      params: { since },
    })

  return { get, post, put, del, submitOperation, getOperations }
}

export interface NoteOperationResponse {
  id: string
  sequenceNumber: number
  delta: string
  clientId: string
  noteVersion: number
  appliedAt: string
}
