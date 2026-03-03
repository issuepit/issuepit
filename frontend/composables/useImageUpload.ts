/**
 * Composable for uploading images pasted into a textarea and inserting Markdown image links.
 *
 * Usage:
 *   const { uploading, handlePaste } = useImageUpload()
 *
 *   // General textarea (model ref):
 *   <textarea @paste="e => handlePaste(e, md => myText.value += md)" />
 *
 *   // Inline per-object comment:
 *   <textarea @paste="e => handlePaste(e, md => comment.text += md)" />
 */
export const useImageUpload = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string
  const uploading = ref(false)
  const uploadError = ref<string | null>(null)

  async function handlePaste(event: ClipboardEvent, insertText: (md: string) => void): Promise<void> {
    const items = event.clipboardData?.items
    if (!items) return

    for (const item of items) {
      if (item.kind === 'file' && item.type.startsWith('image/')) {
        const file = item.getAsFile()
        if (!file) continue

        event.preventDefault()
        uploading.value = true
        uploadError.value = null
        try {
          const body = new FormData()
          body.append('file', file)
          const result = await $fetch<{ url: string }>('/api/uploads/image', {
            baseURL,
            method: 'POST',
            body,
            credentials: 'include',
          })
          insertText(`![image](${result.url})`)
        } catch (e: unknown) {
          const msg = e instanceof Error ? e.message : 'Upload failed'
          uploadError.value = msg
          console.error('[useImageUpload] Upload error:', e)
        } finally {
          uploading.value = false
        }
        return
      }
    }
  }

  return { uploading, uploadError, handlePaste }
}

