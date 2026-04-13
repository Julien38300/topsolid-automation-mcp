# Graphe API Interactif

<div id="graph-stats" style="text-align:center; margin:1em 0; font-size:0.9em; color:#888;"></div>

<div id="graph-toolbar" style="text-align:center; margin:0.5em 0;">
  <button onclick="filterModule('all')" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Tout</button>
  <button onclick="filterModule('Kernel')" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #4a9eff; background:#e8f0fe; color:#1a56db;">Kernel</button>
  <button onclick="filterModule('Design')" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #10b981; background:#ecfdf5; color:#059669;">Design</button>
  <button onclick="filterModule('Drafting')" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #f59e0b; background:#fffbeb; color:#d97706;">Drafting</button>
  <button onclick="cy.fit()" style="padding:4px 12px; margin:2px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Recentrer</button>
</div>

<div id="cy" style="width:100%; height:650px; border:1px solid #e5e7eb; border-radius:8px; background:#fafafa;"></div>

<div id="node-info" style="margin-top:1em; padding:1em; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; display:none;">
  <h3 id="info-title" style="margin:0 0 0.5em 0;"></h3>
  <div id="info-content"></div>
</div>

<script>
let cy, graphData;

// Load cytoscape dynamically (single <script> required by VitePress)
if (typeof window !== 'undefined' && !window.cytoscape) {
  const s = document.createElement('script');
  s.src = 'https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js';
  s.onload = () => { if (typeof initGraph === 'function') initGraph(); };
  document.head.appendChild(s);
}

const colors = {
  Kernel: { bg: '#4a9eff', border: '#1a56db', light: '#e8f0fe' },
  Design: { bg: '#10b981', border: '#059669', light: '#ecfdf5' },
  Drafting: { bg: '#f59e0b', border: '#d97706', light: '#fffbeb' }
};

async function initGraph() {
  const resp = await fetch('/noemid-topsolid-automation/graph-data.json');
  graphData = await resp.json();

  document.getElementById('graph-stats').innerHTML =
    `<strong>${graphData.stats.totalInterfaces}</strong> interfaces · ` +
    `<strong>${graphData.stats.totalMethods}</strong> methodes · ` +
    `<strong>${graphData.stats.totalEdges}</strong> edges · ` +
    `<strong>${graphData.stats.totalExamples}</strong> avec exemples`;

  cy = cytoscape({
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
          'border-color': function(ele) { return colors[ele.data('module')]?.border || '#888'; },
          'background-color': function(ele) { return colors[ele.data('module')]?.bg || '#888'; },
          'color': '#fff',
          'font-weight': 'bold',
          'text-outline-width': 1,
          'text-outline-color': function(ele) { return colors[ele.data('module')]?.border || '#888'; }
        }
      },
      {
        selector: 'node[examples = 0]',
        style: { 'border-style': 'dashed', 'opacity': 0.7 }
      },
      {
        selector: 'edge',
        style: {
          'width': function(ele) { return Math.max(0.5, ele.data('weight') * 0.5); },
          'line-color': '#d1d5db',
          'curve-style': 'bezier',
          'opacity': 0.3
        }
      },
      {
        selector: 'node:selected',
        style: { 'border-width': 4, 'border-color': '#ef4444', 'overlay-opacity': 0.1 }
      },
      {
        selector: '.highlighted',
        style: { 'opacity': 1 }
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
      refresh: 20,
      fit: true,
      padding: 30,
      randomize: false,
      componentSpacing: 60,
      nodeRepulsion: 8000,
      edgeElasticity: 100,
      gravity: 0.25
    },
    minZoom: 0.3,
    maxZoom: 3
  });

  cy.on('tap', 'node', function(evt) {
    const d = evt.target.data();
    const pct_ex = d.edges > 0 ? Math.round(d.examples / d.edges * 100) : 0;
    const pct_hint = d.edges > 0 ? Math.round(d.hints / d.edges * 100) : 0;

    // Highlight neighbors
    cy.elements().removeClass('highlighted faded');
    const neighborhood = evt.target.neighborhood().add(evt.target);
    cy.elements().addClass('faded');
    neighborhood.addClass('highlighted').removeClass('faded');

    const info = document.getElementById('node-info');
    info.style.display = 'block';
    document.getElementById('info-title').textContent = 'I' + d.label + ' (' + d.module + ')';
    document.getElementById('info-content').innerHTML =
      '<table style="width:100%; border-collapse:collapse;">' +
      '<tr><td style="padding:4px 8px;">Methodes</td><td style="padding:4px 8px;"><strong>' + d.methods + '</strong></td></tr>' +
      '<tr><td style="padding:4px 8px;">Edges graphe</td><td style="padding:4px 8px;"><strong>' + d.edges + '</strong></td></tr>' +
      '<tr><td style="padding:4px 8px;">Avec exemples</td><td style="padding:4px 8px;"><strong>' + d.examples + '</strong> (' + pct_ex + '%)</td></tr>' +
      '<tr><td style="padding:4px 8px;">Hints semantiques</td><td style="padding:4px 8px;"><strong>' + d.hints + '</strong> (' + pct_hint + '%)</td></tr>' +
      '<tr><td style="padding:4px 8px;">Connexions</td><td style="padding:4px 8px;"><strong>' + evt.target.neighborhood('node').length + '</strong> interfaces liees</td></tr>' +
      '</table>';
  });

  cy.on('tap', function(evt) {
    if (evt.target === cy) {
      cy.elements().removeClass('highlighted faded');
      document.getElementById('node-info').style.display = 'none';
    }
  });
}

function filterModule(mod) {
  if (mod === 'all') {
    cy.elements().show();
  } else {
    cy.nodes().forEach(n => {
      if (n.data('module') === mod) { n.show(); }
      else { n.hide(); }
    });
    cy.edges().forEach(e => {
      if (e.source().visible() && e.target().visible()) { e.show(); }
      else { e.hide(); }
    });
  }
  cy.fit(cy.elements(':visible'), 30);
}

if (typeof window !== 'undefined') {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initGraph);
  } else {
    setTimeout(initGraph, 100);
  }
}
</script>

## Legende

- **Taille du noeud** = nombre de methodes
- **Couleur** : <span style="color:#4a9eff">Kernel</span> / <span style="color:#10b981">Design</span> / <span style="color:#f59e0b">Drafting</span>
- **Bordure pointillee** = 0 exemples de code
- **Liens** = types partages entre interfaces (DocumentId, ElementId, PdmObjectId...)
- **Clic sur un noeud** = details + highlight des connexions

## Comment lire le graphe

Chaque noeud est une **interface TopSolid** (IPdm, IDocuments, IParameters...). Les liens montrent quelles interfaces partagent des types communs et sont souvent utilisees ensemble.

Les plus gros noeuds (IParameters, IPdm, IDocuments) sont les interfaces centrales. Les interfaces Design (assemblages, familles, materiaux) et Drafting (mise en plan, nomenclature) sont des extensions specialisees.
