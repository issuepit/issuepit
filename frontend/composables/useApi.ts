export const useApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string

  const getHeaders = () => {
    const token = process.client ? localStorage.getItem('issuepit_token') : null
    return token ? { Authorization: `Bearer ${token}` } : {}
  }

  const get = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'GET', headers: getHeaders(), ...opts })

  const post = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'POST', body, headers: getHeaders(), ...opts })

  const put = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PUT', body, headers: getHeaders(), ...opts })

  const patch = <T>(path: string, body: unknown, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'PATCH', body, headers: getHeaders(), ...opts })

  const del = <T>(path: string, opts?: object) =>
    $fetch<T>(path, { baseURL, method: 'DELETE', headers: getHeaders(), ...opts })

  return { get, post, put, patch, del }
}
