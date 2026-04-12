import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'TopSolid MCP',
  description: 'Serveur MCP et Graphe API enrichi pour TopSolid Automation',
  lang: 'fr-FR',
  base: '/Cortana/',

  head: [
    ['link', { rel: 'icon', href: '/Cortana/favicon.ico' }]
  ],

  themeConfig: {
    logo: '/logo.svg',

    nav: [
      { text: 'Accueil', link: '/' },
      { text: 'Guide', link: '/guide/presentation' },
      { text: 'Reference', link: '/reference/glossaire' },
      { text: 'Roadmap', link: '/guide/roadmap' }
    ],

    sidebar: {
      '/guide/': [
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
            { text: 'Recettes', link: '/guide/recettes' }
          ]
        },
        {
          text: 'Metier TopSolid',
          items: [
            { text: 'Glossaire FR/EN', link: '/reference/glossaire' },
            { text: 'Exporteurs', link: '/reference/exporteurs' },
            { text: 'Proprietes PDM', link: '/reference/proprietes-pdm' }
          ]
        }
      ],
      '/reference/': [
        {
          text: 'Reference',
          items: [
            { text: 'Glossaire FR/EN', link: '/reference/glossaire' },
            { text: 'Proprietes PDM', link: '/reference/proprietes-pdm' },
            { text: 'Exporteurs', link: '/reference/exporteurs' },
            { text: 'Interfaces API', link: '/reference/interfaces' },
            { text: 'Graphe Interactif', link: '/reference/graphe-interactif' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/jup/Cortana' }
    ],

    search: {
      provider: 'local'
    },

    footer: {
      message: 'TopSolid MCP — Serveur Model Context Protocol pour TopSolid Automation',
      copyright: '2026 Julien'
    },

    outline: {
      level: [2, 3],
      label: 'Sur cette page'
    }
  }
})
