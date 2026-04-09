import DefaultTheme from 'vitepress/theme'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute, useData } from 'vitepress'
import mediumZoom from 'medium-zoom'
import type { EnhanceAppContext } from 'vitepress'
import './custom.css'

// Screenshot directory prefix (relative URLs in markdown)
const SCREENSHOT_PREFIX = '/assets/screenshots/'

export default {
  extends: DefaultTheme,
  setup() {
    const route = useRoute()
    const { isDark } = useData()

    const initZoom = () => {
      mediumZoom('.main img:not(.VPImage)', {
        background: 'var(--vp-c-bg)',
        margin: 24,
      })
    }

    /**
     * Swap documentation screenshot images between dark and light variants.
     *
     * Convention:
     *   - Dark (default):  <name>.png
     *   - Light variant:   <name>-light.png
     *
     * Markdown references the dark variant; the theme swaps to the light
     * variant automatically when the user switches to light mode.
     */
    const updateThemedScreenshots = () => {
      document.querySelectorAll<HTMLImageElement>('.main img').forEach((img) => {
        const src = img.getAttribute('src') || ''
        if (!src.includes(SCREENSHOT_PREFIX)) return

        // Store the original (dark) src on first encounter
        if (!img.dataset.srcDark) {
          img.dataset.srcDark = src
          img.dataset.srcLight = src.replace(/\.png$/, '-light.png')
        }

        img.src = isDark.value ? img.dataset.srcDark! : img.dataset.srcLight!
      })
    }

    onMounted(() => {
      initZoom()
      updateThemedScreenshots()
    })

    watch(
      () => route.path,
      () => nextTick(() => {
        initZoom()
        updateThemedScreenshots()
      }),
    )

    watch(isDark, () => {
      updateThemedScreenshots()
    })
  },
} satisfies EnhanceAppContext
