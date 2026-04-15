<script setup>
import { ref, computed } from 'vue'

const recipes = [
  { name: "lire_designation", description: "Lit la designation (description fonctionnelle) du document actif", mode: "READ", category: "PDM", api: "Pdm.GetDescription" },
  { name: "lire_nom", description: "Lit le nom unique du document dans le projet", mode: "READ", category: "PDM", api: "Pdm.GetName" },
  { name: "lire_reference", description: "Lit la reference / part number du document", mode: "READ", category: "PDM", api: "Pdm.GetPartNumber" },
  { name: "lire_fabricant", description: "Lit le fabricant ou fournisseur du document", mode: "READ", category: "PDM", api: "Pdm.GetManufacturer" },
  { name: "lire_proprietes_pdm", description: "Retourne nom, designation, reference et fabricant d'un coup", mode: "READ", category: "PDM", api: "Pdm.Get{Name,Description,PartNumber,Manufacturer}" },
  { name: "modifier_designation", description: "Change la designation. Sauvegarde auto", mode: "WRITE", category: "PDM", api: "Pdm.SetDescription — value = texte" },
  { name: "modifier_nom", description: "Renomme le document dans le projet", mode: "WRITE", category: "PDM", api: "Pdm.SetName — value = nouveau nom" },
  { name: "modifier_reference", description: "Change la reference / part number", mode: "WRITE", category: "PDM", api: "Pdm.SetPartNumber — value = ref" },
  { name: "modifier_fabricant", description: "Change le fabricant. Sauvegarde auto", mode: "WRITE", category: "PDM", api: "Pdm.SetManufacturer — value = nom" },
  { name: "lire_projet_courant", description: "Retourne le nom du projet actif", mode: "READ", category: "Navigation", api: "Pdm.GetCurrentProject" },
  { name: "lire_contenu_projet", description: "Parcourt recursivement toute l'arborescence : dossiers, sous-dossiers, documents", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents (recursif)" },
  { name: "chercher_document", description: "Recherche un document par nom (match partiel CONTAINS)", mode: "READ", category: "Navigation", api: "Pdm.SearchDocumentByName — value = texte" },
  { name: "chercher_dossier", description: "Recherche un dossier par nom dans le projet", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents + filtre — value = texte" },
  { name: "ouvrir_document_par_nom", description: "Cherche puis ouvre un document dans l'editeur TopSolid", mode: "WRITE", category: "Navigation", api: "Documents.Open — value = nom" },
  { name: "lister_documents_projet", description: "Liste TOUS les documents du projet avec designation et reference", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents iteration" },
  { name: "lister_documents_dossier", description: "Liste les documents d'un dossier specifique", mode: "READ", category: "Navigation", api: "Pdm.GetConstituents(folder) — value = nom dossier" },
  { name: "resumer_projet", description: "Resume : nombre de documents par type, nombre de dossiers", mode: "READ", category: "Navigation", api: "Comptage par Pdm.GetType" },
  { name: "compter_documents_par_type", description: "Documents groupes par type (.TopPrt, .TopAsm, .TopDft...)", mode: "READ", category: "Navigation", api: "Pdm.GetType sur chaque doc" },
  { name: "lister_documents_sans_reference", description: "Detecte les pieces sans part number (audit qualite)", mode: "READ", category: "Audit", api: "Filtre GetPartNumber vide" },
  { name: "lister_documents_sans_designation", description: "Detecte les documents sans designation", mode: "READ", category: "Audit", api: "Filtre GetDescription vide" },
  { name: "chercher_pieces_par_materiau", description: "Liste les pieces avec materiau et masse. Filtre optionnel", mode: "READ", category: "Navigation", api: "Parameters $Mass + materiau — value = filtre" },
  { name: "lire_cas_emploi", description: "Where-used : documents qui referencent le doc courant", mode: "READ", category: "Navigation", api: "Pdm.SearchMajorRevisionBackReferences" },
  { name: "lire_historique_revisions", description: "Timeline des revisions majeures/mineures avec auteur et date", mode: "READ", category: "Navigation", api: "Pdm.GetMajorRevisions + GetMinorRevisions" },
  { name: "comparer_revisions", description: "Compare parametres revision courante vs precedente", mode: "READ", category: "Comparaison", api: "Diff GetParameters entre 2 revisions" },
  { name: "chercher_documents_modifies", description: "Documents non sauvegardes (dirty) du projet", mode: "READ", category: "Navigation", api: "Documents.IsDirty en boucle" },
  { name: "exporter_batch_step", description: "Exporte TOUTES les pieces du projet en STEP", mode: "READ", category: "Export", api: "Documents.Export en boucle — value = dossier" },
  { name: "lire_propriete_batch", description: "Lit une propriete sur tous les documents du projet", mode: "READ", category: "Batch", api: "Pdm.Get* en boucle — value = nom propriete" },
  { name: "vider_auteur_batch", description: "Vide le champ Auteur de tous les documents du projet", mode: "WRITE", category: "Batch", api: "Pdm.SetAuthor(\"\") en boucle" },
  { name: "vider_auteur_document", description: "Vide le champ Auteur du document courant", mode: "WRITE", category: "Document", api: "Pdm.SetAuthor(pdmId, \"\")" },
  { name: "verifier_virtuel_batch", description: "Verifie le mode virtuel sur tous les documents", mode: "READ", category: "Batch", api: "Documents.IsVirtualDocument en boucle" },
  { name: "activer_virtuel_batch", description: "Active le mode virtuel sur tous les docs non-virtuels", mode: "WRITE", category: "Batch", api: "Documents.SetVirtualDocumentMode(true)" },
  { name: "activer_virtuel_document", description: "Active le mode virtuel sur le document courant", mode: "WRITE", category: "Document", api: "Documents.SetVirtualDocumentMode" },
  { name: "lire_parametres", description: "Liste tous les parametres avec nom, valeur et type (Real, Integer, Boolean, Text)", mode: "READ", category: "Parametres", api: "Parameters.GetParameters + GetParameterType + Get*Value" },
  { name: "lire_parametre_reel", description: "Lit un parametre reel par nom. Valeur en SI (metres)", mode: "READ", category: "Parametres", api: "Parameters.GetRealValue — value = nom" },
  { name: "lire_parametre_texte", description: "Lit un parametre texte par nom (match partiel)", mode: "READ", category: "Parametres", api: "Parameters.GetTextValue — value = nom" },
  { name: "modifier_parametre_reel", description: "Modifie un parametre reel. Valeur en SI (50mm = 0.05)", mode: "WRITE", category: "Parametres", api: "Parameters.SetRealValue — value = nom:valeurSI" },
  { name: "modifier_parametre_texte", description: "Modifie un parametre texte", mode: "WRITE", category: "Parametres", api: "Parameters.SetTextValue — value = nom:valeur" },
  { name: "comparer_parametres", description: "Compare parametres doc actif vs autre doc. Montre differences", mode: "READ", category: "Comparaison", api: "Diff par nom de parametre — value = nom autre doc" },
  { name: "lire_masse_volume", description: "Masse (kg), volume (mm3), surface (mm2) depuis proprietes systeme", mode: "READ", category: "Physique", api: "Parameters systeme $Mass, $Volume, $Surface Area" },
  { name: "rapport_masse_assemblage", description: "Masse totale assemblage, volume, surface, nombre de pieces", mode: "READ", category: "Physique", api: "GetParts + proprietes systeme agreges" },
  { name: "lire_densite_materiau", description: "Densite calculee (kg/m3) a partir de masse/volume", mode: "READ", category: "Physique", api: "masse / volume depuis params systeme" },
  { name: "lire_materiau", description: "Materiau affecte et densite calculee", mode: "READ", category: "Physique", api: "Parameters systeme $Mass + $Volume" },
  { name: "lire_dimensions_piece", description: "Height, Width, Length, Box Size en mm", mode: "READ", category: "Physique", api: "Parameters systeme $Height, $Width, $Length" },
  { name: "lire_boite_englobante", description: "Boite englobante min/max XYZ en mm + dimensions LxlxH", mode: "READ", category: "Physique", api: "Parameters systeme bounding box" },
  { name: "lire_moments_inertie", description: "Moments principaux d'inertie X, Y, Z", mode: "READ", category: "Physique", api: "Parameters systeme $Principal Moment of Inertia" },
  { name: "lire_points_3d", description: "Liste les points 3D avec coordonnees X, Y, Z en mm", mode: "READ", category: "Geometrie", api: "Geometries3D.GetPoints → GetPointGeometry" },
  { name: "lire_reperes_3d", description: "Liste les reperes 3D (frames) par nom", mode: "READ", category: "Geometrie", api: "Geometries3D.GetFrames" },
  { name: "lister_esquisses", description: "Liste les esquisses 2D du document par nom", mode: "READ", category: "Geometrie", api: "Sketches2D.GetSketches" },
  { name: "lire_shapes", description: "Liste les shapes (corps solides) avec nombre de faces", mode: "READ", category: "Geometrie", api: "Shapes.GetShapes → GetFaceCount" },
  { name: "lire_operations", description: "Arbre de construction : operations avec nom et type", mode: "READ", category: "Geometrie", api: "Operations.GetOperations → GetTypeFullName" },
  { name: "attribut_lire_tout", description: "Lit couleur, transparence, calque et visibilite de tous les elements", mode: "READ", category: "Attributs", api: "Elements.GetColor/Transparency + Layers.GetLayer" },
  { name: "attribut_lire_couleur", description: "Couleur RGB de chaque shape", mode: "READ", category: "Attributs", api: "Elements.HasColor → GetColor" },
  { name: "attribut_lire_couleurs_faces", description: "Couleurs face par face (quand chaque face a sa propre couleur)", mode: "READ", category: "Attributs", api: "Shapes.GetFaces → Elements.GetColor" },
  { name: "attribut_modifier_couleur", description: "Change la couleur d'un shape. Selection si plusieurs", mode: "WRITE", category: "Attributs", api: "Elements.SetColor — value = R,G,B" },
  { name: "attribut_modifier_couleur_tout", description: "Change la couleur de TOUS les elements", mode: "WRITE", category: "Attributs", api: "Elements.SetColor en boucle — value = R,G,B" },
  { name: "attribut_remplacer_couleur", description: "Remplace une couleur par une autre sur tous les elements", mode: "WRITE", category: "Attributs", api: "Filtre couleur → SetColor — value = R1,G1,B1:R2,G2,B2" },
  { name: "attribut_lire_transparence", description: "Transparence de chaque shape (0.0 a 1.0)", mode: "READ", category: "Attributs", api: "Elements.HasTransparency → GetTransparency" },
  { name: "attribut_modifier_transparence", description: "Change la transparence. Selection si plusieurs shapes", mode: "WRITE", category: "Attributs", api: "Elements.SetTransparency — value = 0.0 a 1.0" },
  { name: "attribut_lister_calques", description: "Liste les calques (layers) du document", mode: "READ", category: "Attributs", api: "Layers.GetLayers" },
  { name: "attribut_affecter_calque", description: "Deplace un element vers un calque", mode: "WRITE", category: "Attributs", api: "Layers.SetLayer — value = elem:calque" },
  { name: "selectionner_shape", description: "L'utilisateur clique un corps solide. Retourne nom + faces", mode: "ASK", category: "Selection", api: "User.AskShape" },
  { name: "selectionner_face", description: "L'utilisateur clique une face. Retourne shape parent + index", mode: "ASK", category: "Selection", api: "User.AskFace" },
  { name: "selectionner_point_3d", description: "L'utilisateur clique un point 3D. Retourne coordonnees mm", mode: "ASK", category: "Selection", api: "User.AskPoint3D" },
  { name: "detecter_assemblage", description: "Verifie si le document est un assemblage. Liste les pieces", mode: "READ", category: "Assemblage", api: "Assemblies.IsAssembly → GetParts" },
  { name: "lister_inclusions", description: "Inclusions avec document de definition de chaque instance", mode: "READ", category: "Assemblage", api: "IsInclusion → GetInclusionDefinitionDocument" },
  { name: "lire_occurrences", description: "Occurrences avec leur document de definition", mode: "READ", category: "Assemblage", api: "Assemblies.GetOccurrences" },
  { name: "compter_pieces_assemblage", description: "Pieces groupees par type avec quantites. Ex: Vis M8: 4", mode: "READ", category: "Assemblage", api: "GetParts groupe par definition" },
  { name: "renommer_occurrence", description: "Renomme une occurrence (match partiel insensible casse)", mode: "WRITE", category: "Assemblage", api: "Entities.SetFunctionOccurrenceName — value = ancien:nouveau" },
  { name: "detecter_famille", description: "Verifie si famille. Indique explicite ou implicite", mode: "READ", category: "Famille", api: "Families.IsFamily + IsExplicit" },
  { name: "lire_codes_famille", description: "Liste les codes (variantes) du catalogue", mode: "READ", category: "Famille", api: "Families.GetCodes" },
  { name: "verifier_drivers_famille", description: "Verifie que les drivers ont une designation", mode: "READ", category: "Famille", api: "Families.GetCatalogColumnParameters → GetDescription" },
  { name: "corriger_drivers_famille", description: "Genere automatiquement une designation pour les drivers sans", mode: "WRITE", category: "Famille", api: "Elements.SetDescription auto-genere" },
  { name: "verifier_drivers_famille_batch", description: "Audit drivers de TOUTES les familles du projet", mode: "READ", category: "Famille", api: "IsFamily → GetCatalogColumnParameters en boucle" },
  { name: "exporter_step", description: "Export STEP (AP214). Format universel CAO", mode: "READ", category: "Export", api: "Documents.Export STEP — value = chemin" },
  { name: "exporter_dxf", description: "Export DXF (AutoCAD). Pour plans et profils 2D", mode: "READ", category: "Export", api: "Documents.Export DXF — value = chemin" },
  { name: "exporter_pdf", description: "Export PDF. Pour mises en plan", mode: "READ", category: "Export", api: "Documents.Export PDF — value = chemin" },
  { name: "exporter_stl", description: "Export STL (maillage). Pour impression 3D", mode: "READ", category: "Export", api: "Documents.Export STL — value = chemin" },
  { name: "exporter_iges", description: "Export IGES. Format historique aeronautique", mode: "READ", category: "Export", api: "Documents.Export IGES — value = chemin" },
  { name: "lister_exporteurs", description: "Liste tous les formats d'export disponibles", mode: "READ", category: "Export", api: "Documents.GetExporterNames" },
  { name: "exporter_nomenclature_csv", description: "Exporte la BOM en texte avec colonnes separees", mode: "READ", category: "Export", api: "Nomenclatures lecture → format texte" },
  { name: "audit_piece", description: "Audit complet : PDM, parametres, shapes, masse, volume, materiau", mode: "READ", category: "Audit", api: "Pdm + Parameters + Shapes + systeme" },
  { name: "audit_assemblage", description: "Audit assemblage : pieces, inclusions, occurrences, masse totale", mode: "READ", category: "Audit", api: "Assemblies + masse systeme" },
  { name: "verifier_piece", description: "Check-list qualite : designation, reference, materiau remplis ?", mode: "READ", category: "Audit", api: "GetDescription + GetPartNumber + materiau" },
  { name: "verifier_projet", description: "Qualite projet : pieces sans designation et sans reference", mode: "READ", category: "Audit", api: "Iteration + filtres" },
  { name: "verifier_materiaux_manquants", description: "Pieces du projet sans materiau affecte (masse = 0)", mode: "READ", category: "Audit", api: "Filtre $Mass == 0" },
  { name: "auditer_noms_parametres", description: "Incoherences convention noms : CamelCase vs snake_case, doublons", mode: "READ", category: "Audit", api: "Analyse syntaxique regex des noms" },
  { name: "auditer_noms_parametres_batch", description: "Audit noms parametres sur tout le projet", mode: "READ", category: "Audit", api: "Iteration + analyse syntaxique" },
  { name: "auditer_designations_drivers_batch", description: "Designations drivers de toutes les familles (fautes, incoherences)", mode: "READ", category: "Audit", api: "GetCatalogColumnParameters → GetDescription" },
  { name: "detecter_mise_en_plan", description: "Verifie si mise en plan. Retourne nombre de vues et format", mode: "READ", category: "Mise en plan", api: "DraftingDocuments.IsDraftingDocument" },
  { name: "ouvrir_mise_en_plan", description: "Cherche la mise en plan associee (back-references) et l'ouvre", mode: "WRITE", category: "Mise en plan", api: "SearchBackReferences .TopDft → Documents.Open" },
  { name: "lister_vues_mise_en_plan", description: "Vues du plan avec nom, echelle et type", mode: "READ", category: "Mise en plan", api: "DraftingDocuments.GetViews → GetViewScale" },
  { name: "lire_echelle_mise_en_plan", description: "Echelle globale et echelle par vue individuellement", mode: "READ", category: "Mise en plan", api: "GetDocumentScale + GetViewScale par vue" },
  { name: "lire_format_mise_en_plan", description: "Format papier : taille, dimensions mm, orientation, pages", mode: "READ", category: "Mise en plan", api: "GetSheetSize / Margins / PageCount" },
  { name: "lire_projection_principale", description: "Piece source et vues principales du plan", mode: "READ", category: "Mise en plan", api: "GetProjectionDocument" },
  { name: "detecter_nomenclature", description: "Verifie si BOM presente. Retourne nombre de lignes", mode: "READ", category: "Nomenclature", api: "Nomenclatures.GetNomenclatures" },
  { name: "lire_colonnes_nomenclature", description: "Colonnes du tableau (Repere, Designation, Quantite...)", mode: "READ", category: "Nomenclature", api: "Nomenclatures.GetColumns → GetColumnName" },
  { name: "lire_contenu_nomenclature", description: "Tableau complet : toutes lignes avec toutes cellules", mode: "READ", category: "Nomenclature", api: "GetRows → GetCellValue par colonne" },
  { name: "compter_lignes_nomenclature", description: "Lignes actives et inactives de la BOM", mode: "READ", category: "Nomenclature", api: "GetRows + IsRowActive" },
  { name: "detecter_mise_a_plat", description: "Verifie si depliage tolerie. Liste les plis detectes", mode: "READ", category: "Depliage", api: "Detection operations FlatPattern" },
  { name: "lire_plis_depliage", description: "Plis avec angle (deg), rayon interieur (mm), longueur (mm)", mode: "READ", category: "Depliage", api: "Proprietes operations de pliage" },
  { name: "lire_dimensions_depliage", description: "Dimensions du contour deplie : longueur et largeur du flan", mode: "READ", category: "Depliage", api: "Parametres systeme depliage" },
  { name: "type_document", description: "Type du document : piece, assemblage, mise en plan, famille...", mode: "READ", category: "Document", api: "IsAssembly + IsDrafting + IsFamily + GetType" },
  { name: "sauvegarder_document", description: "Sauvegarde le document actif", mode: "WRITE", category: "Document", api: "Pdm.Save(pdmId, true)" },
  { name: "reconstruire_document", description: "Force la reconstruction (recalcul toutes operations)", mode: "WRITE", category: "Document", api: "Documents.Rebuild(docId)" },
  { name: "sauvegarder_tout_projet", description: "Sauvegarde tous les documents du projet en une passe", mode: "WRITE", category: "Document", api: "Pdm.Save en boucle" },
  { name: "lire_propriete_utilisateur", description: "Lit une propriete utilisateur personnalisee (texte)", mode: "READ", category: "Document", api: "Pdm.GetUserPropertyValue — value = nom" },
  { name: "modifier_propriete_utilisateur", description: "Modifie une propriete utilisateur", mode: "WRITE", category: "Document", api: "Pdm.SetUserPropertyValue — value = nom:valeur" },
  { name: "invoquer_commande", description: "Execute une commande menu TopSolid par nom interne", mode: "WRITE", category: "Document", api: "Application.InvokeCommand — value = commande" },
  { name: "comparer_operations_documents", description: "Compare arbre construction entre 2 documents", mode: "READ", category: "Comparaison", api: "Operations.GetOperations diff — value = nom autre doc" },
  { name: "comparer_entites_documents", description: "Compare entites (shapes, esquisses, points, reperes)", mode: "READ", category: "Comparaison", api: "Comptage Shapes/Sketches/Points — value = nom autre doc" },
  { name: "reporter_parametres", description: "Copie valeurs parametres doc actif vers un autre. Match par nom", mode: "WRITE", category: "Batch", api: "Parameters.Set*Value — value = nom doc cible" },
  { name: "reporter_proprietes_pdm", description: "Copie designation/reference/fabricant vers un autre doc", mode: "WRITE", category: "Batch", api: "Pdm.Set{Description,PartNumber,Manufacturer} — value = nom doc cible" },
]

const search = ref('')
const sortKey = ref('name')
const sortAsc = ref(true)
const filterMode = ref('ALL')
const filterCategory = ref('ALL')

const categories = computed(() => {
  const cats = [...new Set(recipes.map(r => r.category))].sort()
  return ['ALL', ...cats]
})

const modes = ['ALL', 'READ', 'WRITE', 'ASK']

const modeColors = { READ: '#3b82f6', WRITE: '#ef4444', ASK: '#f59e0b' }

const filtered = computed(() => {
  let list = recipes
  if (search.value) {
    const q = search.value.toLowerCase()
    list = list.filter(r =>
      r.name.includes(q) || r.description.toLowerCase().includes(q) || r.api.toLowerCase().includes(q)
    )
  }
  if (filterMode.value !== 'ALL') list = list.filter(r => r.mode === filterMode.value)
  if (filterCategory.value !== 'ALL') list = list.filter(r => r.category === filterCategory.value)
  list = [...list].sort((a, b) => {
    const va = a[sortKey.value] || ''
    const vb = b[sortKey.value] || ''
    return sortAsc.value ? va.localeCompare(vb) : vb.localeCompare(va)
  })
  return list
})

const stats = computed(() => ({
  total: recipes.length,
  read: recipes.filter(r => r.mode === 'READ').length,
  write: recipes.filter(r => r.mode === 'WRITE').length,
  ask: recipes.filter(r => r.mode === 'ASK').length,
  shown: filtered.value.length
}))

function toggleSort(key) {
  if (sortKey.value === key) sortAsc.value = !sortAsc.value
  else { sortKey.value = key; sortAsc.value = true }
}

function sortIcon(key) {
  if (sortKey.value !== key) return ' \u2195'
  return sortAsc.value ? ' \u2191' : ' \u2193'
}
</script>

<template>
  <div class="recipe-table-container">
    <div class="controls">
      <input v-model="search" type="text" placeholder="Rechercher (nom, description, API)..." class="search-input" />
      <select v-model="filterCategory" class="filter-select">
        <option v-for="c in categories" :key="c" :value="c">{{ c === 'ALL' ? 'Toutes categories' : c }}</option>
      </select>
      <select v-model="filterMode" class="filter-select">
        <option v-for="m in modes" :key="m" :value="m">{{ m === 'ALL' ? 'Tous modes' : m }}</option>
      </select>
    </div>

    <div class="stats-bar">
      <span class="stat">{{ stats.shown }}/{{ stats.total }} recettes</span>
      <span class="badge read">{{ stats.read }} READ</span>
      <span class="badge write">{{ stats.write }} WRITE</span>
      <span class="badge ask">{{ stats.ask }} ASK</span>
    </div>

    <div class="table-scroll">
      <table>
        <thead>
          <tr>
            <th @click="toggleSort('name')" class="sortable">Recette{{ sortIcon('name') }}</th>
            <th @click="toggleSort('description')" class="sortable">Description{{ sortIcon('description') }}</th>
            <th @click="toggleSort('category')" class="sortable">Categorie{{ sortIcon('category') }}</th>
            <th @click="toggleSort('api')" class="sortable">API TopSolid{{ sortIcon('api') }}</th>
            <th @click="toggleSort('mode')" class="sortable">Mode{{ sortIcon('mode') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in filtered" :key="r.name">
            <td><code>{{ r.name }}</code></td>
            <td>{{ r.description }}</td>
            <td><span class="cat-badge">{{ r.category }}</span></td>
            <td><code class="api-code">{{ r.api }}</code></td>
            <td>
              <span class="mode-badge" :style="{ background: modeColors[r.mode] }">{{ r.mode }}</span>
            </td>
          </tr>
          <tr v-if="filtered.length === 0">
            <td colspan="5" class="no-results">Aucune recette trouvee</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.recipe-table-container {
  margin: 1rem 0;
}
.controls {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}
.search-input {
  flex: 1;
  min-width: 200px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--vp-c-border);
  border-radius: 6px;
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-1);
  font-size: 0.9rem;
}
.search-input:focus {
  outline: none;
  border-color: var(--vp-c-brand-1);
}
.filter-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--vp-c-border);
  border-radius: 6px;
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-1);
  font-size: 0.9rem;
  cursor: pointer;
}
.stats-bar {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  color: var(--vp-c-text-2);
}
.stat { font-weight: 600; }
.badge {
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 0.75rem;
  font-weight: 600;
  color: white;
}
.badge.read { background: #3b82f6; }
.badge.write { background: #ef4444; }
.badge.ask { background: #f59e0b; }
.table-scroll {
  overflow-x: auto;
  border: 1px solid var(--vp-c-border);
  border-radius: 8px;
}
table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}
thead {
  background: var(--vp-c-bg-soft);
  position: sticky;
  top: 0;
}
th {
  padding: 0.6rem 0.75rem;
  text-align: left;
  font-weight: 600;
  border-bottom: 2px solid var(--vp-c-border);
  white-space: nowrap;
}
th.sortable {
  cursor: pointer;
  user-select: none;
}
th.sortable:hover {
  color: var(--vp-c-brand-1);
}
td {
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--vp-c-border);
  vertical-align: top;
}
tr:hover td {
  background: var(--vp-c-bg-soft);
}
td code {
  font-size: 0.8rem;
  padding: 1px 4px;
  border-radius: 3px;
  background: var(--vp-c-bg-mute);
}
.api-code {
  font-size: 0.75rem;
  color: var(--vp-c-text-2);
  word-break: break-word;
}
.cat-badge {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 0.75rem;
  background: var(--vp-c-bg-mute);
  color: var(--vp-c-text-2);
  white-space: nowrap;
}
.mode-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 700;
  color: white;
  text-transform: uppercase;
}
.no-results {
  text-align: center;
  color: var(--vp-c-text-3);
  padding: 2rem !important;
}
</style>
