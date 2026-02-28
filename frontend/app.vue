<template>
  <div class="flex h-screen bg-gray-950 text-gray-100 overflow-hidden">
    <!-- Sidebar -->
    <aside class="w-60 bg-gray-900 border-r border-gray-800 flex flex-col shrink-0">
      <!-- Logo -->
      <div class="h-14 flex items-center px-4 border-b border-gray-800">
        <div class="flex items-center gap-2">
          <div class="w-7 h-7 rounded-md bg-brand-600 flex items-center justify-center">
            <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M9 3H5a2 2 0 00-2 2v4m6-6h10a2 2 0 012 2v4M9 3v18m0 0h10a2 2 0 002-2V9M9 21H5a2 2 0 01-2-2V9m0 0h18" />
            </svg>
          </div>
          <span class="font-bold text-white text-sm tracking-wide">IssuePit</span>
        </div>
      </div>

      <!-- Nav -->
      <nav class="flex-1 p-3 space-y-0.5 overflow-y-auto">
        <SidebarLink to="/" icon="dashboard" label="Dashboard" />
        <SidebarLink to="/projects" icon="projects" label="Projects" />
        <div class="pt-3 pb-1">
          <p class="text-xs font-medium text-gray-500 uppercase tracking-wider px-2 mb-1">Work</p>
        </div>
        <SidebarLink to="/issues" icon="issues" label="My Issues" />
        <div class="pt-3 pb-1">
          <p class="text-xs font-medium text-gray-500 uppercase tracking-wider px-2 mb-1">System</p>
        </div>
        <SidebarLink to="/agents" icon="agents" label="Agents" />
        <SidebarLink to="/config/keys" icon="config" label="Configuration" />
        <SidebarLink to="/settings" icon="settings" label="Settings" />
        <div class="pt-3 pb-1">
          <p class="text-xs font-medium text-gray-500 uppercase tracking-wider px-2 mb-1">Admin</p>
        </div>
        <SidebarLink to="/admin/tenants" icon="tenants" label="Tenants" />
      </nav>

      <!-- Footer -->
      <div class="p-3 border-t border-gray-800">
        <div class="flex items-center gap-2 px-2 py-1.5 rounded-md hover:bg-gray-800 cursor-pointer">
          <div class="w-6 h-6 rounded-full bg-brand-600 flex items-center justify-center text-xs font-bold">U</div>
          <span class="text-sm text-gray-300">User</span>
        </div>
      </div>
    </aside>

    <!-- Main -->
    <main class="flex-1 overflow-y-auto">
      <NuxtPage />
    </main>
  </div>
</template>

<script setup lang="ts">
// Sidebar link component defined inline
const SidebarLink = defineComponent({
  props: { to: String, icon: String, label: String },
  setup(props) {
    const route = useRoute()
    const isActive = computed(() => {
      if (props.to === '/') return route.path === '/'
      if (props.to === '/config/keys') return route.path.startsWith('/config')
      return route.path.startsWith(props.to!)
    })
    const icons: Record<string, string> = {
      dashboard: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
      projects: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10',
      issues: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2',
      agents: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17H3a2 2 0 01-2-2V5a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2h-2',
      config: 'M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z',
      settings: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
      tenants: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4'
    }
    return () => h(resolveComponent('NuxtLink'), {
      to: props.to,
      class: [
        'flex items-center gap-2.5 px-2 py-1.5 rounded-md text-sm transition-colors',
        isActive.value ? 'bg-gray-800 text-white' : 'text-gray-400 hover:text-gray-200 hover:bg-gray-800/60'
      ]
    }, () => [
      h('svg', { class: 'w-4 h-4 shrink-0', fill: 'none', stroke: 'currentColor', viewBox: '0 0 24 24' },
        h('path', { 'stroke-linecap': 'round', 'stroke-linejoin': 'round', 'stroke-width': '2', d: icons[props.icon!] })
      ),
      h('span', props.label)
    ])
  }
})
</script>
