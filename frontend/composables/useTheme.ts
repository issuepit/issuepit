/**
 * useTheme — manages the active UI theme.
 *
 * Priority (highest → lowest):
 *  1. ?theme= query-string parameter (e.g. for screenshot automation)
 *  2. localStorage key "issuepit-theme" (browser-level override)
 *  3. User profile theme saved in the database (persisted preference)
 *  4. System prefers-color-scheme (OS/browser default)
 *
 * The active theme is applied as a `data-theme` attribute on <html>.
 */

export type ThemeId = 'dark' | 'dim' | 'dark-accent' | 'dark-square' | 'light'

export interface ThemeOption {
  id: ThemeId
  label: string
  description: string
}

export const THEMES: ThemeOption[] = [
  { id: 'dark',        label: 'Dark',         description: 'Classic dark theme (default)' },
  { id: 'dim',         label: 'Dim',          description: 'Softer, cooler dark theme' },
  { id: 'dark-accent', label: 'Dark Accent',  description: 'Dark with a vibrant purple accent' },
  { id: 'dark-square', label: 'Dark Square',  description: 'Dark theme with sharp, square edges' },
  { id: 'light',       label: 'Light',        description: 'Light theme for bright environments' },
]

const STORAGE_KEY = 'issuepit-theme'

function systemDefault(): ThemeId {
  if (!import.meta.client) return 'dark'
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

function readLocalStorage(): ThemeId | null {
  if (!import.meta.client) return null
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v && THEMES.some(t => t.id === v)) return v as ThemeId
  } catch { /* ignore */ }
  return null
}

function readQueryString(): ThemeId | null {
  if (!import.meta.client) return null
  try {
    const params = new URLSearchParams(window.location.search)
    const v = params.get('theme')
    if (v && THEMES.some(t => t.id === v)) return v as ThemeId
  } catch { /* ignore */ }
  return null
}

function applyTheme(id: ThemeId) {
  if (!import.meta.client) return
  document.documentElement.setAttribute('data-theme', id)
}

export const useTheme = () => {
  const auth = useAuthStore()
  const api = useApi()

  /** The theme currently applied (reactive). */
  const activeTheme = useState<ThemeId>('activeTheme', () => 'dark')

  /**
   * Reactive ref tracking the browser-local override. Updated synchronously
   * whenever setBrowserTheme() is called so computed consumers re-evaluate.
   */
  const _browserOverrideTick = useState<ThemeId | null>('browserOverrideTick', () => readLocalStorage())

  /**
   * Determine and apply the correct theme based on the priority chain.
   * Call this once on app mount (app.vue) and after the user changes their preference.
   */
  function resolveAndApply(userDbTheme?: string | null) {
    const qs = readQueryString()
    const ls = _browserOverrideTick.value
    const db = (userDbTheme && THEMES.some(t => t.id === userDbTheme)) ? (userDbTheme as ThemeId) : null
    const sys = systemDefault()

    const resolved: ThemeId = qs ?? ls ?? db ?? sys
    activeTheme.value = resolved
    applyTheme(resolved)
  }

  /**
   * Set a browser-local theme override (stored in localStorage only).
   * Pass `null` to clear the override and fall back to DB / system default.
   */
  function setBrowserTheme(id: ThemeId | null) {
    if (!import.meta.client) return
    try {
      if (id) {
        localStorage.setItem(STORAGE_KEY, id)
      } else {
        localStorage.removeItem(STORAGE_KEY)
      }
    } catch { /* ignore */ }
    _browserOverrideTick.value = id
    resolveAndApply(auth.user?.theme)
  }

  /**
   * Save the user's preferred theme to their profile (persisted in DB).
   * Also updates the local state immediately.
   */
  async function saveProfileTheme(id: ThemeId | null) {
    await api.patch('/api/auth/me/theme', { theme: id })
    if (auth.user) {
      auth.user = { ...auth.user, theme: id ?? undefined }
    }
    resolveAndApply(id)
  }

  /** Currently active browser override (from localStorage), reactive. */
  const browserOverride = computed<ThemeId | null>(() => _browserOverrideTick.value)

  return {
    activeTheme,
    browserOverride,
    resolveAndApply,
    setBrowserTheme,
    saveProfileTheme,
    THEMES,
  }
}
