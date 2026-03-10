/**
 * Composable for recording voice as a 16-bit PCM WAV file and uploading it
 * to the backend for Vosk transcription.
 *
 * Usage:
 *   const { recording, uploading, transcription, voiceUrl, error, startRecording, stopRecording, uploadRecording, reset } = useVoiceRecorder()
 */

const SAMPLE_RATE = 16000

function encodeWav(samples: Float32Array, sampleRate: number): Blob {
  // Convert float32 PCM → int16 PCM
  const pcm = new Int16Array(samples.length)
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    pcm[i] = s < 0 ? s * 0x8000 : s * 0x7fff
  }

  const dataLen = pcm.byteLength
  const buffer = new ArrayBuffer(44 + dataLen)
  const view = new DataView(buffer)

  const write = (offset: number, str: string) => {
    for (let i = 0; i < str.length; i++) view.setUint8(offset + i, str.charCodeAt(i))
  }

  // RIFF header
  write(0, 'RIFF')
  view.setUint32(4, 36 + dataLen, true)
  write(8, 'WAVE')
  // fmt chunk
  write(12, 'fmt ')
  view.setUint32(16, 16, true)        // chunk size
  view.setUint16(20, 1, true)         // PCM
  view.setUint16(22, 1, true)         // mono
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * 2, true) // byte rate (sampleRate * channels * bitsPerSample/8)
  view.setUint16(32, 2, true)         // block align
  view.setUint16(34, 16, true)        // bits per sample
  // data chunk
  write(36, 'data')
  view.setUint32(40, dataLen, true)
  new Int16Array(buffer, 44).set(pcm)

  return new Blob([buffer], { type: 'audio/wav' })
}

export const useVoiceRecorder = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string

  const recording = ref(false)
  const uploading = ref(false)
  const transcription = ref('')
  const voiceUrl = ref('')
  const error = ref<string | null>(null)
  const lastWavBlob = ref<Blob | null>(null)

  // Internal recorder state
  let audioContext: AudioContext | null = null
  let processor: ScriptProcessorNode | null = null
  let stream: MediaStream | null = null
  let chunks: Float32Array[] = []
  let actualSampleRate = SAMPLE_RATE

  async function startRecording(): Promise<void> {
    error.value = null
    transcription.value = ''
    voiceUrl.value = ''
    chunks = []

    try {
      stream = await navigator.mediaDevices.getUserMedia({
        audio: { sampleRate: SAMPLE_RATE, channelCount: 1, echoCancellation: true },
      })

      // Use the actual sample rate the browser provides
      audioContext = new AudioContext({ sampleRate: SAMPLE_RATE })
      actualSampleRate = audioContext.sampleRate

      const source = audioContext.createMediaStreamSource(stream)
      // ScriptProcessorNode is deprecated in favour of AudioWorklet but has broader
      // browser support and requires no additional worker-script setup, making it
      // the pragmatic choice here for a first-party recording composable.
      processor = audioContext.createScriptProcessor(4096, 1, 1)
      processor.onaudioprocess = (e: AudioProcessingEvent) => {
        const data = e.inputBuffer.getChannelData(0)
        chunks.push(new Float32Array(data))
      }
      source.connect(processor)
      processor.connect(audioContext.destination)
      recording.value = true
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Microphone access denied'
    }
  }

  function stopRecording(): Blob | null {
    if (!recording.value) return null

    processor?.disconnect()
    audioContext?.close()
    stream?.getTracks().forEach((t) => t.stop())

    recording.value = false

    if (chunks.length === 0) return null

    // Flatten all captured chunks into one Float32Array
    const totalLength = chunks.reduce((acc, c) => acc + c.length, 0)
    const samples = new Float32Array(totalLength)
    let offset = 0
    for (const chunk of chunks) {
      samples.set(chunk, offset)
      offset += chunk.length
    }

    return encodeWav(samples, actualSampleRate)
  }

  async function uploadRecording(wavBlob: Blob): Promise<void> {
    uploading.value = true
    error.value = null
    lastWavBlob.value = wavBlob
    try {
      const body = new FormData()
      body.append('file', wavBlob, 'recording.wav')
      const result = await $fetch<{ voiceUrl: string; transcription: string }>('/api/uploads/voice', {
        baseURL,
        method: 'POST',
        body,
        credentials: 'include',
      })
      voiceUrl.value = result.voiceUrl
      transcription.value = result.transcription
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Upload failed'
    } finally {
      uploading.value = false
    }
  }

  function reset(): void {
    recording.value = false
    uploading.value = false
    transcription.value = ''
    voiceUrl.value = ''
    error.value = null
    lastWavBlob.value = null
    chunks = []
    processor = null
    audioContext = null
    stream = null
  }

  return { recording, uploading, transcription, voiceUrl, lastWavBlob, error, startRecording, stopRecording, uploadRecording, reset }
}
