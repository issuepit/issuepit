import DefaultTheme from 'vitepress/theme'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute, useData } from 'vitepress'
import mediumZoom from 'medium-zoom'
import type { EnhanceAppContext } from 'vitepress'
import './custom.css'

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

    const updateThemedScreenshots = () => {
      document.querySelectorAll<HTMLImageElement>('img[data-src-light][data-src-dark]').forEach((img) => {
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
