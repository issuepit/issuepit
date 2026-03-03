export interface LogColorRule {
  id: string
  /** JavaScript regular expression pattern (string form, no slashes) */
  pattern: string
  /** CSS color value (e.g. "#ff0000" or "red") applied as the line text color */
  color: string
}

interface UserPreferences {
  /** Render ANSI escape codes as colored text; when false, codes are stripped */
  ansiColors: boolean
  /** Custom regex-based color rules applied to log line plain text */
  logColorRules: LogColorRule[]
}

const STORAGE_KEY = 'issuepit-user-preferences'

const defaultPreferences: UserPreferences = {
  ansiColors: true,
  logColorRules: [],
}

function load(): UserPreferences {
  if (!import.meta.client) return { ...defaultPreferences, logColorRules: [] }
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      const parsed = JSON.parse(stored) as Partial<UserPreferences>
      return {
        ansiColors: parsed.ansiColors ?? defaultPreferences.ansiColors,
        logColorRules: Array.isArray(parsed.logColorRules) ? parsed.logColorRules : [],
      }
    }
  } catch { /* ignore */ }
  return { ...defaultPreferences, logColorRules: [] }
}

function save(prefs: UserPreferences) {
  if (!import.meta.client) return
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs))
  } catch { /* ignore */ }
}

export const useUserPreferences = () => {
  const prefs = useState<UserPreferences>('userPreferences', load)

  function setAnsiColors(value: boolean) {
    prefs.value.ansiColors = value
    save(prefs.value)
  }

  function addLogColorRule(rule: Omit<LogColorRule, 'id'>) {
    prefs.value.logColorRules = [
      ...prefs.value.logColorRules,
      { ...rule, id: crypto.randomUUID() },
    ]
    save(prefs.value)
  }

  function removeLogColorRule(id: string) {
    prefs.value.logColorRules = prefs.value.logColorRules.filter(r => r.id !== id)
    save(prefs.value)
  }

  function updateLogColorRule(id: string, updates: Partial<Omit<LogColorRule, 'id'>>) {
    prefs.value.logColorRules = prefs.value.logColorRules.map(r =>
      r.id === id ? { ...r, ...updates } : r,
    )
    save(prefs.value)
  }

  return { prefs, setAnsiColors, addLogColorRule, removeLogColorRule, updateLogColorRule }
}
