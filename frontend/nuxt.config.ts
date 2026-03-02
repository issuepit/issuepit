export default defineNuxtConfig({
  devtools: { enabled: true },
  modules: ['@nuxtjs/tailwindcss', '@pinia/nuxt', '@nuxt/eslint'],
  css: ['~/assets/main.css'],
  runtimeConfig: {
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5000',
      mcpBase: process.env.NUXT_PUBLIC_MCP_BASE || 'http://localhost:5010',
    }
  },
  typescript: {
    strict: true
  }
})
