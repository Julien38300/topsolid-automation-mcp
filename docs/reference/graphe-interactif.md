# Graphe API Interactif

<div id="graph-stats" style="text-align:center; margin:1em 0; font-size:0.9em; color:#888;"></div>

<div id="graph-toolbar" style="text-align:center; margin:0.5em 0; display:flex; flex-wrap:wrap; gap:6px; justify-content:center; align-items:center;">
  <div id="view-switcher" style="display:inline-flex; border:1px solid #d1d5db; border-radius:6px; overflow:hidden; margin-right:8px;">
    <button id="view-2d" onclick="switchView('2d')" style="padding:4px 14px; border:0; cursor:pointer; background:#4a9eff; color:#fff; font-weight:bold;">2D</button>
    <button id="view-3d" onclick="switchView('3d')" style="padding:4px 14px; border:0; cursor:pointer; background:#fff; color:#374151;">3D</button>
  </div>
  <button onclick="filterModule('all')" style="padding:4px 12px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Tout</button>
  <button onclick="filterModule('Kernel')" style="padding:4px 12px; cursor:pointer; border-radius:4px; border:1px solid #4a9eff; background:#e8f0fe; color:#1a56db;">Kernel</button>
  <button onclick="filterModule('Design')" style="padding:4px 12px; cursor:pointer; border-radius:4px; border:1px solid #10b981; background:#ecfdf5; color:#059669;">Design</button>
  <button onclick="filterModule('Drafting')" style="padding:4px 12px; cursor:pointer; border-radius:4px; border:1px solid #f59e0b; background:#fffbeb; color:#d97706;">Drafting</button>
  <button onclick="recenterGraph()" style="padding:4px 12px; cursor:pointer; border-radius:4px; border:1px solid #ccc;">Recentrer</button>
</div>

<div id="cy" style="width:100%; height:650px; border:1px solid #e5e7eb; border-radius:8px; background:#fafafa;"></div>

<div id="fg3d" style="width:100%; height:650px; border:1px solid #e5e7eb; border-radius:8px; background:#0a0e1a; display:none; position:relative;"></div>

<div id="node-info" style="margin-top:1em; padding:1em; background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; display:none;">
  <h3 id="info-title" style="margin:0 0 0.5em 0;"></h3>
  <div id="info-content"></div>
</div>

<script>
let cy, graphData, fg3d, currentView = '2d', currentFilter = 'all';
let cyReady = false, fg3dReady = false;

const colors = {
  Kernel: { bg: '#4a9eff', border: '#1a56db', light: '#e8f0fe' },
  Design: { bg: '#10b981', border: '#059669', light: '#ecfdf5' },
  Drafting: { bg: '#f59e0b', border: '#d97706', light: '#fffbeb' }
};

function getBasePath() {
  const parts = location.pathname.split('/').filter(Boolean);
  return parts.length > 0 ? '/' + parts[0] + '/' : '/';
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const s = document.createElement('script');
    s.src = src;
    s.onload = resolve;
    s.onerror = reject;
    document.head.appendChild(s);
  });
}

async function loadGraphData() {
  if (graphData) return graphData;
  const resp = await fetch(getBasePath() + 'graph-data.json');
  graphData = await resp.json();
  document.getElementById('graph-stats').innerHTML =
    '<strong>' + graphData.stats.totalInterfaces + '</strong> interfaces · ' +
    '<strong>' + graphData.stats.totalMethods + '</strong> methodes · ' +
    '<strong>' + graphData.stats.totalEdges + '</strong> edges · ' +
    '<strong>' + graphData.stats.totalExamples + '</strong> avec exemples';
  return graphData;
}

function showNodeDetails(d, neighborCount) {
  const pct_ex = d.edges > 0 ? Math.round(d.examples / d.edges * 100) : 0;
  const pct_hint = d.edges > 0 ? Math.round(d.hints / d.edges * 100) : 0;
  const info = document.getElementById('node-info');
  info.style.display = 'block';
  document.getElementById('info-title').textContent = 'I' + d.label + ' (' + d.module + ')';
  document.getElementById('info-content').innerHTML =
    '<table style="width:100%; border-collapse:collapse;">' +
    '<tr><td style="padding:4px 8px;">Methodes</td><td style="padding:4px 8px;"><strong>' + d.methods + '</strong></td></tr>' +
    '<tr><td style="padding:4px 8px;">Edges graphe</td><td style="padding:4px 8px;"><strong>' + d.edges + '</strong></td></tr>' +
    '<tr><td style="padding:4px 8px;">Avec exemples</td><td style="padding:4px 8px;"><strong>' + d.examples + '</strong> (' + pct_ex + '%)</td></tr>' +
    '<tr><td style="padding:4px 8px;">Hints semantiques</td><td style="padding:4px 8px;"><strong>' + d.hints + '</strong> (' + pct_hint + '%)</td></tr>' +
    '<tr><td style="padding:4px 8px;">Connexions</td><td style="padding:4px 8px;"><strong>' + neighborCount + '</strong> interfaces liees</td></tr>' +
    '</table>';
}

async function init2D() {
  if (cyReady) return;
  await loadScript('https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js');
  await loadGraphData();

  cy = cytoscape({
    container: document.getElementById('cy'),
    elements: [...graphData.nodes, ...graphData.edges],
    style: [
      { selector: 'node', style: {
        'label': 'data(label)', 'width': 'data(size)', 'height': 'data(size)',
        'font-size': '10px', 'text-valign': 'center', 'text-halign': 'center',
        'text-wrap': 'wrap', 'text-max-width': '70px', 'border-width': 2,
        'border-color': function(ele) { return colors[ele.data('module')]?.border || '#888'; },
        'background-color': function(ele) { return colors[ele.data('module')]?.bg || '#888'; },
        'color': '#fff', 'font-weight': 'bold',
        'text-outline-width': 1,
        'text-outline-color': function(ele) { return colors[ele.data('module')]?.border || '#888'; }
      }},
      { selector: 'node[examples = 0]', style: { 'border-style': 'dashed', 'opacity': 0.7 }},
      { selector: 'edge', style: {
        'width': function(ele) { return Math.max(0.5, ele.data('weight') * 0.5); },
        'line-color': '#d1d5db', 'curve-style': 'bezier', 'opacity': 0.3
      }},
      { selector: 'node:selected', style: { 'border-width': 4, 'border-color': '#ef4444', 'overlay-opacity': 0.1 }},
      { selector: '.highlighted', style: { 'opacity': 1 }},
      { selector: '.faded', style: { 'opacity': 0.15 }}
    ],
    layout: {
      name: 'cose', idealEdgeLength: 120, nodeOverlap: 20, refresh: 20,
      fit: true, padding: 30, randomize: false, componentSpacing: 60,
      nodeRepulsion: 8000, edgeElasticity: 100, gravity: 0.25
    },
    minZoom: 0.3, maxZoom: 3
  });

  cy.on('tap', 'node', function(evt) {
    cy.elements().removeClass('highlighted faded');
    const neighborhood = evt.target.neighborhood().add(evt.target);
    cy.elements().addClass('faded');
    neighborhood.addClass('highlighted').removeClass('faded');
    showNodeDetails(evt.target.data(), evt.target.neighborhood('node').length);
  });

  cy.on('tap', function(evt) {
    if (evt.target === cy) {
      cy.elements().removeClass('highlighted faded');
      document.getElementById('node-info').style.display = 'none';
    }
  });

  cyReady = true;
}

async function init3D() {
  if (fg3dReady) return;
  await loadScript('https://unpkg.com/3d-force-graph@1.73.4/dist/3d-force-graph.min.js');
  await loadGraphData();

  const nodes3d = graphData.nodes.map(n => ({
    id: n.data.id, label: n.data.label, module: n.data.module,
    methods: n.data.methods, edges: n.data.edges, examples: n.data.examples,
    hints: n.data.hints, descs: n.data.descs, size: n.data.size
  }));
  const links3d = graphData.edges.map(e => ({
    source: e.data.source, target: e.data.target, weight: e.data.weight
  }));

  const neighbors = new Map();
  nodes3d.forEach(n => neighbors.set(n.id, new Set()));
  links3d.forEach(l => {
    neighbors.get(l.source).add(l.target);
    neighbors.get(l.target).add(l.source);
  });

  let highlightNode = null;

  fg3d = ForceGraph3D()(document.getElementById('fg3d'))
    .graphData({ nodes: nodes3d, links: links3d })
    .backgroundColor('#0a0e1a')
    .nodeLabel(n => 'I' + n.label + ' (' + n.module + ')')
    .nodeVal(n => Math.max(2, n.methods / 4))
    .nodeColor(n => {
      if (highlightNode) {
        if (n.id === highlightNode.id) return '#ef4444';
        if (neighbors.get(highlightNode.id).has(n.id)) return colors[n.module]?.bg || '#888';
        return 'rgba(120,120,140,0.25)';
      }
      return colors[n.module]?.bg || '#888';
    })
    .nodeOpacity(0.95)
    .linkColor(l => {
      if (highlightNode) {
        const s = typeof l.source === 'object' ? l.source.id : l.source;
        const t = typeof l.target === 'object' ? l.target.id : l.target;
        if (s === highlightNode.id || t === highlightNode.id) return 'rgba(239,68,68,0.8)';
        return 'rgba(120,120,140,0.08)';
      }
      return 'rgba(180,180,200,0.25)';
    })
    .linkWidth(l => highlightNode ? 1.5 : 0.4)
    .linkOpacity(0.5)
    .onNodeClick(n => {
      highlightNode = n;
      const count = neighbors.get(n.id).size;
      showNodeDetails(n, count);
      fg3d.refresh();
      const dist = 140;
      const r = dist / Math.hypot(n.x || 1, n.y || 1, n.z || 1);
      fg3d.cameraPosition(
        { x: (n.x || 0) * (1 + r), y: (n.y || 0) * (1 + r), z: (n.z || 0) * (1 + r) },
        n, 1200
      );
    })
    .onBackgroundClick(() => {
      highlightNode = null;
      document.getElementById('node-info').style.display = 'none';
      fg3d.refresh();
    });

  fg3dReady = true;
}

function switchView(v) {
  currentView = v;
  const btn2d = document.getElementById('view-2d');
  const btn3d = document.getElementById('view-3d');
  const cont2d = document.getElementById('cy');
  const cont3d = document.getElementById('fg3d');
  if (v === '3d') {
    btn3d.style.background = '#4a9eff'; btn3d.style.color = '#fff'; btn3d.style.fontWeight = 'bold';
    btn2d.style.background = '#fff'; btn2d.style.color = '#374151'; btn2d.style.fontWeight = 'normal';
    cont2d.style.display = 'none';
    cont3d.style.display = 'block';
    init3D().then(() => applyFilter3D(currentFilter));
  } else {
    btn2d.style.background = '#4a9eff'; btn2d.style.color = '#fff'; btn2d.style.fontWeight = 'bold';
    btn3d.style.background = '#fff'; btn3d.style.color = '#374151'; btn3d.style.fontWeight = 'normal';
    cont3d.style.display = 'none';
    cont2d.style.display = 'block';
    init2D().then(() => applyFilter2D(currentFilter));
  }
  try { localStorage.setItem('graphe-view', v); } catch (e) {}
}

function applyFilter2D(mod) {
  if (!cyReady) return;
  if (mod === 'all') {
    cy.elements().show();
  } else {
    cy.nodes().forEach(n => { n.data('module') === mod ? n.show() : n.hide(); });
    cy.edges().forEach(e => { e.source().visible() && e.target().visible() ? e.show() : e.hide(); });
  }
  cy.fit(cy.elements(':visible'), 30);
}

function applyFilter3D(mod) {
  if (!fg3dReady) return;
  const all = graphData;
  let nodes3d = all.nodes.map(n => ({ ...n.data }));
  if (mod !== 'all') nodes3d = nodes3d.filter(n => n.module === mod);
  const visible = new Set(nodes3d.map(n => n.id));
  const links3d = all.edges
    .filter(e => visible.has(e.data.source) && visible.has(e.data.target))
    .map(e => ({ source: e.data.source, target: e.data.target, weight: e.data.weight }));
  fg3d.graphData({ nodes: nodes3d, links: links3d });
}

function filterModule(mod) {
  currentFilter = mod;
  if (currentView === '3d') applyFilter3D(mod);
  else applyFilter2D(mod);
}

function recenterGraph() {
  if (currentView === '3d' && fg3dReady) fg3d.zoomToFit(800, 40);
  else if (cyReady) cy.fit(cy.elements(':visible'), 30);
}

if (typeof window !== 'undefined') {
  window.filterModule = filterModule;
  window.recenterGraph = recenterGraph;
  window.switchView = switchView;

  function bootstrap() {
    let saved = '2d';
    try { saved = localStorage.getItem('graphe-view') || '2d'; } catch (e) {}
    switchView(saved);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootstrap);
  } else {
    setTimeout(bootstrap, 100);
  }
}
</script>

## Legende

- **Taille du noeud** = nombre de methodes
- **Couleur** : <span style="color:#4a9eff">Kernel</span> / <span style="color:#10b981">Design</span> / <span style="color:#f59e0b">Drafting</span>
- **Bordure pointillee (2D)** = 0 exemples de code
- **Liens** = types partages entre interfaces (DocumentId, ElementId, PdmObjectId...)
- **Clic sur un noeud** = details + highlight des connexions
- **2D / 3D** = bascule la vue (preference memorisee dans le navigateur). Le 3D se pilote a la souris : clic-gauche glisser pour tourner, molette pour zoomer, clic-droit glisser pour translater.

## Comment lire le graphe

Chaque noeud est une **interface TopSolid** (IPdm, IDocuments, IParameters...). Les liens montrent quelles interfaces partagent des types communs et sont souvent utilisees ensemble.

Les plus gros noeuds (IParameters, IPdm, IDocuments) sont les interfaces centrales. Les interfaces Design (assemblages, familles, materiaux) et Drafting (mise en plan, nomenclature) sont des extensions specialisees.

La vue **3D** (style Obsidian) est plus immersive et fait ressortir les clusters de modules. La vue **2D** reste plus pratique pour chercher un noeud precis et lire les labels.
