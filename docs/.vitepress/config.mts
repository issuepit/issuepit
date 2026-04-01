import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'IssuePit Documentation',
  description: 'User documentation for IssuePit — Agent Orchestration Platform with Issue Tracking',
  base: '/issuepit/',

  // Links starting with /config/ point to the IssuePit application UI, not documentation pages.
  ignoreDeadLinks: [
    /^\/config\//,
  ],

  head: [
    ['link', { rel: 'icon', href: '/issuepit/favicon.ico' }],
  ],

  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Getting Started', link: '/getting-started' },
      { text: 'GitHub', link: 'https://github.com/issuepit/issuepit' },
    ],

    sidebar: [
      {
        text: 'Guide',
        items: [
          { text: 'Home', link: '/' },
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Projects', link: '/projects' },
          { text: 'Common Agenda', link: '/agenda' },
          { text: 'Agents', link: '/issupitAgents' },
          { text: 'Configuration', link: '/configuration' },
          { text: 'Todo Tracker', link: '/todos' },
          { text: 'CI/CD Integration', link: '/cicd' },
          { text: 'Config Repo', link: '/config-repo' },
          { text: 'Releases', link: '/releases' },
        ],
      },
      {
        text: 'Developer',
        items: [
          { text: 'Developer', link: '/developer' },
          { text: 'Architecture', link: '/architecture' },
          { text: 'GitHub SSO', link: '/github-sso' },
          { text: 'Helper Containers', link: '/helper-containers' },
          { text: 'Known Issues', link: '/known-issues' },
          { text: 'FAQ', link: '/faq' },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/issuepit/issuepit' },
    ],

    search: {
      provider: 'local',
    },

    footer: {
      message: 'IssuePit — Agent Orchestration Platform',
    },
  },
})
