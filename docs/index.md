---
layout: home

hero:
  name: "topsolid-automation-mcp"
  text: "MCP server communautaire pour TopSolid® 7"
  tagline: Projet open-source (MIT) indépendant de TOPSOLID SAS. Branchez n'importe quel agent IA compatible MCP à votre flux CAO/PDM.
  actions:
    - theme: brand
      text: Demarrage rapide
      link: /guide/quickstart
    - theme: alt
      text: Outils MCP
      link: /guide/outils-mcp
    - theme: alt
      text: GitHub
      link: https://github.com/Julien38300/topsolid-automation-mcp

features:
  - title: Graphe API enrichi
    details: 4119 edges, 1728 methodes, 1194 avec exemples reels, 85% de hints semantiques FR/EN.
    icon: "&#x1F9E0;"
  - title: 13 outils MCP
    details: run_recipe, api_help, find_path, explore_paths, get_state, execute_script, modify_script, get_recipe, compile, search_examples, whats_new, search_help, search_commands.
    icon: "&#x1F6E0;"
  - title: 130 recettes
    details: Pilotage complet sans code — PDM, parametres, masse, export, assemblages, familles, mise en plan, nomenclature (BOM read+write), mise a plat, comparaison, audit batch.
    icon: "&#x1F4D6;"
  - title: Aide TopSolid en FTS5
    details: 5809 pages (2974 EN + 2835 FR) indexees. Tool search_help avec bm25 + snippets. 100% embedded, pas de dep externe.
    icon: "&#x1F4DA;"
  - title: Catalogue UI commands
    details: 2428 commandes TopSolid indexees (tout module confondu). FullName pret a invoke_command. Couvre les actions non exposees par l'API Automation.
    icon: "&#x1F5C3;"
  - title: Bridge HTTP/SSE
    details: Exposez le serveur stdio local a claude.ai web / ChatGPT / apps mobiles via un simple wrapper Node (mcp-proxy + tunnel Cloudflare).
    icon: "&#x1F310;"
---

<div style="max-width: 900px; margin: 3rem auto 0; padding: 1.25rem 1.5rem; border: 1px solid #e5e7eb; border-radius: 8px; font-size: 0.9em; color: var(--vp-c-text-2);">

**Note de marque** — TopSolid<sup>®</sup> est une marque déposée de <a href="https://www.topsolid.com/" target="_blank" rel="noopener">TOPSOLID SAS</a>. Le projet `topsolid-automation-mcp` est une initiative **communautaire indépendante** sous licence MIT. Il n'est ni endossé, ni sponsorisé, ni affilié à TOPSOLID SAS. Il encapsule l'API Automation publique livrée avec TopSolid 7 et indexe l'aide en ligne publiquement distribuée. Une licence TopSolid valide reste nécessaire pour utiliser ce serveur contre une instance TopSolid.

</div>
