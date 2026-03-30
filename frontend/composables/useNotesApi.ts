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

  return { get, post, put, del }
}
