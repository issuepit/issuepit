<template>
  <div class="flex h-screen bg-gray-950 text-gray-100 overflow-hidden">
    <!-- Sidebar -->
    <AppSidebar />

    <!-- Main -->
    <main class="flex-1 overflow-y-auto">
      <NuxtPage />
    </main>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'

const auth = useAuthStore()
const { resolveAndApply } = useTheme()

// Fetch the current user on initial load so the sidebar reflects the logged-in state.
onMounted(async () => {
  await auth.fetchMe()
  // Apply the user's saved theme (falls back to localStorage → system default).
  resolveAndApply(auth.user?.theme)
})
</script>
