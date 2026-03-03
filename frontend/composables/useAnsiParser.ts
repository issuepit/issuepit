// Maps standard ANSI SGR foreground color codes to CSS colors (dark-mode optimized)
const FG_COLORS: Record<number, string> = {
  30: '#4d4d4d', 31: '#cc4444', 32: '#44cc44', 33: '#cccc44',
  34: '#4444cc', 35: '#cc44cc', 36: '#44cccc', 37: '#cccccc',
  90: '#777777', 91: '#ff6666', 92: '#66ff66', 93: '#ffff66',
  94: '#6666ff', 95: '#ff66ff', 96: '#66ffff', 97: '#ffffff',
}

// Maps standard ANSI SGR background color codes to CSS colors
const BG_COLORS: Record<number, string> = {
  40: '#000000', 41: '#aa0000', 42: '#00aa00', 43: '#aa5500',
  44: '#0000aa', 45: '#aa00aa', 46: '#00aaaa', 47: '#aaaaaa',
  100: '#555555', 101: '#ff5555', 102: '#55ff55', 103: '#ffff55',
  104: '#5555ff', 105: '#ff55ff', 106: '#55ffff', 107: '#ffffff',
}

// Pattern source for ANSI escape sequences (ESC [ <params> m).
// The constant is used only for its .source to create fresh per-call RegExp instances
// in parseAnsiToHtml — this constant itself is never executed with .exec().
// eslint-disable-next-line no-control-regex
const ANSI_RE = /\u001b\[([0-9;]*)m/g

interface AnsiState {
  fg?: string
  bg?: string
  bold?: boolean
  dim?: boolean
  italic?: boolean
  underline?: boolean
}

function stateToStyle(s: AnsiState): string {
  const parts: string[] = []
  if (s.fg) parts.push(`color:${s.fg}`)
  if (s.bg) parts.push(`background-color:${s.bg}`)
  if (s.bold) parts.push('font-weight:bold')
  if (s.dim) parts.push('opacity:0.6')
  if (s.italic) parts.push('font-style:italic')
  if (s.underline) parts.push('text-decoration:underline')
  return parts.join(';')
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

// Returns an 8-bit (256-color) CSS color string from an index
function get256Color(n: number): string {
  if (n < 16) {
    const table = [
      '#000000', '#aa0000', '#00aa00', '#aa5500',
      '#0000aa', '#aa00aa', '#00aaaa', '#aaaaaa',
      '#555555', '#ff5555', '#55ff55', '#ffff55',
      '#5555ff', '#ff55ff', '#55ffff', '#ffffff',
    ]
    return table[n] ?? '#aaaaaa'
  }
  if (n < 232) {
    const i = n - 16
    const r = Math.floor(i / 36) * 51
    const g = Math.floor((i % 36) / 6) * 51
    const b = (i % 6) * 51
    return `rgb(${r},${g},${b})`
  }
  const gray = 8 + (n - 232) * 10
  return `rgb(${gray},${gray},${gray})`
}

/**
 * Converts ANSI SGR escape codes in `text` to styled HTML spans.
 * The text content is HTML-escaped before processing so the output is safe for v-html.
 */
export function parseAnsiToHtml(text: string): string {
  const re = new RegExp(ANSI_RE.source, 'g')
  let result = ''
  let lastIndex = 0
  let state: AnsiState = {}
  let hasOpenSpan = false

  let match: RegExpExecArray | null
  while ((match = re.exec(text)) !== null) {
    const rawChunk = text.slice(lastIndex, match.index)
    if (rawChunk) result += escapeHtml(rawChunk)
    lastIndex = match.index + match[0].length

    const rawCodes = match[1] === '' ? [0] : match[1].split(';').map(Number)
    const newState: AnsiState = { ...state }

    let i = 0
    while (i < rawCodes.length) {
      const code = rawCodes[i]
      if (code === 0) {
        // Reset all
        newState.fg = undefined
        newState.bg = undefined
        newState.bold = false
        newState.dim = false
        newState.italic = false
        newState.underline = false
        i++
      } else if (code === 1) { newState.bold = true; i++ }
      else if (code === 2) { newState.dim = true; i++ }
      else if (code === 3) { newState.italic = true; i++ }
      else if (code === 4) { newState.underline = true; i++ }
      else if (code === 22) { newState.bold = false; newState.dim = false; i++ }
      else if (code === 23) { newState.italic = false; i++ }
      else if (code === 24) { newState.underline = false; i++ }
      else if (code === 39) { newState.fg = undefined; i++ }
      else if (code === 49) { newState.bg = undefined; i++ }
      else if (code === 38 || code === 48) {
        const isFg = code === 38
        if (rawCodes[i + 1] === 5 && rawCodes[i + 2] !== undefined) {
          // 256-color
          const color = get256Color(rawCodes[i + 2])
          if (isFg) newState.fg = color; else newState.bg = color
          i += 3
        } else if (rawCodes[i + 1] === 2 && rawCodes[i + 4] !== undefined) {
          // True-color (r;g;b)
          const color = `rgb(${rawCodes[i + 2]},${rawCodes[i + 3]},${rawCodes[i + 4]})`
          if (isFg) newState.fg = color; else newState.bg = color
          i += 5
        } else { i++ }
      } else if (FG_COLORS[code]) { newState.fg = FG_COLORS[code]; i++ }
      else if (BG_COLORS[code]) { newState.bg = BG_COLORS[code]; i++ }
      else { i++ }
    }

    // Close previous span
    if (hasOpenSpan) {
      result += '</span>'
      hasOpenSpan = false
    }

    // Open new span if there is any active styling
    const style = stateToStyle(newState)
    if (style) {
      result += `<span style="${style}">`
      hasOpenSpan = true
    }

    state = newState
  }

  // Remaining text after last escape sequence
  const remaining = text.slice(lastIndex)
  if (remaining) result += escapeHtml(remaining)
  if (hasOpenSpan) result += '</span>'

  return result
}

/**
 * Strips all ANSI SGR escape codes from `text`, returning plain text.
 */
export function stripAnsiCodes(text: string): string {
  // eslint-disable-next-line no-control-regex
  return text.replace(/\u001b\[[0-9;]*m/g, '')
}
