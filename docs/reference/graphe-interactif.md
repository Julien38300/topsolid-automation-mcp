# Graphe API Interactif

<div id="graph-stats" style="text-align:center; margin:1em 0; font-size:0.9em; color:#888;"></div>

<div id="graph-toolbar" style="text-align:center; margin:0.5em 0;">
  <button id="btn-all" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Tout</button>
  <button id="btn-kernel" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #4a9eff; background:#e8f0fe; color:#1a56db;">Kernel</button>
  <button id="btn-design" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #10b981; background:#ecfdf5; color:#059669;">Design</button>
  <button id="btn-drafting" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #f59e0b; background:#fffbeb; color:#d97706;">Drafting</button>
  <button id="btn-fit" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Recentrer</button>
</div>

<div id="cy" style="width:100%; height:650px; border:1px solid #e5e7eb; border-radius:8px; background:#fafafa;"></div>

<div id="node-info" style="margin-top:1em; padding:1em; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; display:none;">
  <h3 id="info-title" style="margin:0 0 0.5em 0;"></h3>
  <div id="info-content"></div>
</div>

<script setup>
import { onMounted } from 'vue'

onMounted(() => {
  const script = document.createElement('script')
  script.src = 'https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js'
  script.onload = () => { initGraph() }
  document.head.appendChild(script)
})

function initGraph() {
  const colors = {
    Kernel: { bg: '#4a9eff', border: '#1a56db' },
    Design: { bg: '#10b981', border: '#059669' },
    Drafting: { bg: '#f59e0b', border: '#d97706' }
  }

  fetch('/noemid-topsolid-automation/graph-data.json')
    .then(r => r.json())
    .then(graphData => {
      document.getElementById('graph-stats').innerHTML =
        '<strong>' + graphData.stats.totalInterfaces + '</strong> interfaces · ' +
        '<strong>' + graphData.stats.totalMethods + '</strong> methodes · ' +
        '<strong>' + graphData.stats.totalEdges + '</strong> edges · ' +
        '<strong>' + graphData.stats.totalExamples + '</strong> avec exemples'

      const cy = window.cytoscape({
        container: document.getElementById('cy'),
        elements: [...graphData.nodes, ...graphData.edges],
        style: [
          {
            selector: 'node',
            style: {
              'label': 'data(label)',
              'width': 'data(size)',
              'height': 'data(size)',
              'font-size': '10px',
              'text-valign': 'center',
              'text-halign': 'center',
              'text-wrap': 'wrap',
              'text-max-width': '70px',
              'border-width': 2,
              'border-color': ele => colors[ele.data('module')]?.border || '#888',
              'background-color': ele => colors[ele.data('module')]?.bg || '#888',
              'color': '#fff',
              'font-weight': 'bold',
              'text-outline-width': 1,
              'text-outline-color': ele => colors[ele.data('module')]?.border || '#888'
            }
          },
          {
            selector: 'node[examples = 0]',
            style: { 'border-style': 'dashed', 'opacity': 0.7 }
          },
          {
            selector: 'edge',
            style: {
              'width': ele => Math.max(1, ele.data('weight') * 0.8),
              'line-color': '#94a3b8',
              'curve-style': 'bezier',
              'opacity': 0.6
            }
          },
          {
            selector: '.faded',
            style: { 'opacity': 0.15 }
          }
        ],
        layout: {
          name: 'cose',
          idealEdgeLength: 120,
          nodeOverlap: 20,
          fit: true,
          padding: 30,
          nodeRepulsion: 8000,
          gravity: 0.25
        },
        minZoom: 0.3,
        maxZoom: 3
      })

      cy.on('tap', 'node', function(evt) {
        const d = evt.target.data()
        const pctEx = d.edges > 0 ? Math.round(d.examples / d.edges * 100) : 0
        const pctHint = d.edges > 0 ? Math.round(d.hints / d.edges * 100) : 0

        cy.elements().removeClass('faded')
        const neighborhood = evt.target.neighborhood().add(evt.target)
        cy.elements().addClass('faded')
        neighborhood.removeClass('faded')

        const info = document.getElementById('node-info')
        info.style.display = 'block'
        document.getElementById('info-title').textContent = 'I' + d.label + ' (' + d.module + ')'
        document.getElementById('info-content').innerHTML =
          '<table style="width:100%;border-collapse:collapse;">' +
          '<tr><td style="padding:4px 8px;">Methodes</td><td><strong>' + d.methods + '</strong></td></tr>' +
          '<tr><td style="padding:4px 8px;">Edges</td><td><strong>' + d.edges + '</strong></td></tr>' +
          '<tr><td style="padding:4px 8px;">Exemples</td><td><strong>' + d.examples + '</strong> (' + pctEx + '%)</td></tr>' +
          '<tr><td style="padding:4px 8px;">Hints</td><td><strong>' + d.hints + '</strong> (' + pctHint + '%)</td></tr>' +
          '<tr><td style="padding:4px 8px;">Connexions</td><td><strong>' + evt.target.neighborhood('node').length + '</strong></td></tr>' +
          '</table>'
      })

      cy.on('tap', function(evt) {
        if (evt.target === cy) {
          cy.elements().removeClass('faded')
          document.getElementById('node-info').style.display = 'none'
        }
      })

      function filterModule(mod) {
        if (mod === 'all') { cy.elements().show() }
        else {
          cy.nodes().forEach(n => n.data('module') === mod ? n.show() : n.hide())
          cy.edges().forEach(e => e.source().visible() && e.target().visible() ? e.show() : e.hide())
        }
        cy.fit(cy.elements(':visible'), 30)
      }

      document.getElementById('btn-all').onclick = () => filterModule('all')
      document.getElementById('btn-kernel').onclick = () => filterModule('Kernel')
      document.getElementById('btn-design').onclick = () => filterModule('Design')
      document.getElementById('btn-drafting').onclick = () => filterModule('Drafting')
      document.getElementById('btn-fit').onclick = () => cy.fit()
    })
}
</script>

## Legende

- **Taille du noeud** = nombre de methodes
- **Couleur** : <span style="color:#4a9eff">Kernel</span> / <span style="color:#10b981">Design</span> / <span style="color:#f59e0b">Drafting</span>
- **Bordure pointillee** = 0 exemples de code
- **Liens** = types partages entre interfaces (DocumentId, ElementId, PdmObjectId...)
- **Clic sur un noeud** = details + highlight des connexions
