export const useNotesApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.notesBase as string

  const ssrHeaders = import.meta.server ? useRequestHeaders(['cookie']) : undefined

  const get = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'GET', credentials: 'include', headers: ssrHeaders, ...opts })

  const post = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'POST', body, credentials: 'include', headers: ssrHeaders, ...opts })

  const put = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PUT', body, credentials: 'include', headers: ssrHeaders, ...opts })

  const del = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'DELETE', credentials: 'include', headers: ssrHeaders, ...opts })

  return { get, post, put, del }
}
