import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'TopSolid MCP',
  description: 'Serveur MCP et Graphe API enrichi pour TopSolid Automation',
  lang: 'fr-FR',
  base: '/topsolid-automation-mcp/',

  head: [
    ['link', { rel: 'icon', href: '/topsolid-automation-mcp/favicon.ico' }]
  ],

  themeConfig: {
    logo: '/logo.svg',

    nav: [
      { text: 'Accueil', link: '/' },
      { text: 'Guide', link: '/guide/presentation' },
      { text: 'Reference', link: '/reference/glossaire' },
      { text: 'Roadmap', link: '/guide/roadmap' }
    ],

    sidebar: (() => {
      const common = [
        {
          text: 'Demarrage',
          items: [
            { text: 'Demarrage rapide', link: '/guide/quickstart' },
            { text: 'Integration MCP', link: '/guide/integration' },
            { text: 'Bridge HTTP/SSE (claude.ai web)', link: '/guide/bridge-http' },
            { text: 'Depannage', link: '/guide/troubleshooting' }
          ]
        },
        {
          text: 'Introduction',
          items: [
            { text: 'Presentation', link: '/guide/presentation' },
            { text: 'Architecture', link: '/guide/architecture' },
            { text: 'Roadmap', link: '/guide/roadmap' }
          ]
        },
        {
          text: 'Serveur MCP',
          items: [
            { text: 'Outils MCP', link: '/guide/outils-mcp' },
            { text: 'Graphe API', link: '/guide/graphe' },
            { text: 'Graphe Interactif', link: '/reference/graphe-interactif' },
            { text: 'Recettes', link: '/guide/recettes' },
            { text: 'Base de connaissance (dev standalone)', link: '/guide/knowledge-base' },
            { text: 'Tests', link: '/guide/tests' }
          ]
        },
        {
          text: 'Reference',
          items: [
            { text: 'Glossaire FR/EN', link: '/reference/glossaire' },
            { text: 'Exporteurs', link: '/reference/exporteurs' },
            { text: 'Proprietes PDM', link: '/reference/proprietes-pdm' },
            { text: 'Interfaces API', link: '/reference/interfaces' }
          ]
        }
      ]
      // Same sidebar for /guide/ and /reference/ so navigation stays consistent
      return { '/guide/': common, '/reference/': common }
    })(),

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Julien38300/topsolid-automation-mcp' }
    ],

    search: {
      provider: 'local'
    },

    footer: {
      message: 'TopSolid MCP — Serveur Model Context Protocol pour TopSolid Automation',
      copyright: "2026 Julien — TopSolid\u00AE est une marque deposee de TOPSOLID SAS"
    },

    outline: {
      level: [2, 3],
      label: 'Sur cette page'
    }
  }
})
