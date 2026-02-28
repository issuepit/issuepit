export const useApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string

  const get = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'GET', ...opts })

  const post = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'POST', body, ...opts })

  const put = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PUT', body, ...opts })

  const patch = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PATCH', body, ...opts })

  const del = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'DELETE', ...opts })

  return { get, post, put, patch, del }
}
