<template>
  <NuxtPage />
</template>
<!-- milestones.vue acts as a transparent parent so that both
     milestones/index.vue (list) and milestones/[milestoneId].vue (detail)
     are rendered as Nuxt child routes. Without this wrapper the detail page
     URL would change but the list view would never unmount. -->
