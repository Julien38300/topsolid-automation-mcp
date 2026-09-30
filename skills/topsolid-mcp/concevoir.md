---
type: skill-reference
scope: topsolid-mcp
domain: conception-orchestration
version: 1.0.0
created: 2026-09-30
task: T-1206 (Couche 3 — pipeline design-to-gabarits)
sources:
  - server/src/Tools/RecipeTool.cs (catalogue 132 recipes, verbatim ; wrappers R/RW/RD l.2615-2630)
  - server/src/Tools/ExecuteScriptTool.cs (contrat de script, verbatim l.31-48)
  - skills/topsolid-mcp/SKILL.md v5.1.0
  - skills/topsolid-mcp/references/ (concepts-core.md, tolerie.md, assemblages-pilotes.md, gabarits-recipes.md)
  - vault 01_TopSolid/Transverse/Tasks/T-1206-skill-topsolid-conception.md
---

# Concevoir — du besoin client au livrable validé (pipeline d'orchestration)

Cette page est le CHAINON d'orchestration de T-1206 (couche 3) : comment transformer un besoin client
en séquence de pilotage MCP, la valider, puis livrer. Elle n'invente aucun nom d'API, de recipe ou de
paramètre : tout nom ci-dessous est verbatim d'une source citee, ou absent (et alors dit "absent").

Tags de ce document : `[REC]` = lu dans RecipeTool.cs ou ExecuteScriptTool.cs (ligne citee quand le
detail porte) ; `[SKILL]` = SKILL.md v5.1.0 ; `[REF:<fichier> §N]` = contenu VERIFIE dans une
reference tier-2 (non reduplique ici) ; `[API-7.21]` = methods.json 7.21.304.0 / CHM 7.21 ;
`[V]` = vault ; `[A VALIDER]` = notion non confirmee (lire comme un point ouvert, pas une regle).

## 0. Rôle de cette page (et anti-doublons)

- Elle contient : l'arbre de decision du besoin (§3), la sequence d'orchestration (§4), les regles de
  validation avant de dire OK (§5), les regles d'export (§6), les patterns d'escalade API en contexte
  de conception (§7), 3 cas files (§8), la checklist (§10).
- Elle NE reduplique PAS : le modele mental / feature tree / parametres / SmartReal
  (`[REF:concepts-core]`) ; la mecanique unfolding/plis (`[REF:tolerie]`) ; inclusions/codes/familles
  (`[REF:assemblages-pilotes]`) ; le patron C# d'une recipe et son processus d'ajout
  (`[REF:gabarits-recipes §2 et §5]`). Quand ces pages suffisent, ce document renvoie et s'arrete.
- Contrat d'entree : serveur MCP lance, TopSolid connecte (verifie par `topsolid_get_state`),
  catalogue = 132 recipes en 3 modes R/RW/RD et 17 categories (`[REC]`, `[REF:gabarits-recipes §1]`).
- Rappel fondamental (borne le probleme ENTIER) : l'API Automation ne cree PAS la geometrie A-Z ;
  on pilote des GABARITS par parametres. La conception = decider du gabarit et de ses parametres,
  pas modeliser a distance (`[REF:concepts-core §6 et §9]`, `[REF:gabarits-recipes §3]`).
- La couche 4 (outil RAG `topsolid_search_doc`, appel de doc AVANT tout C#) est en cours (T-1206 C4,
  non livree) : cet outil n'existe PAS encore — ne pas l'appeler.

## 1. Le pipeline en un coup d'oeil

| # | Etape | Objectif | Outils / recipes (noms exacts) | Sortie-contrat |
|---|---|---|---|---|
| 0 | Contexte | savoir ou l'on est | `topsolid_get_state` toujours en premier, puis recipe `document_type`, les recipes `detect_*` | connexion + doc actif + type |
| 1 | Cadrage | besoin -> criteres mesurables | questions au client (§2) | liste de criteres chiffres = future checklist de validation |
| 2 | Decomposition | decidir gabarit + parametres + verifs | `topsolid_list_recipes`, `topsolid_get_recipe`, recipes `read_parameters`, `read_operations`, `read_shapes`, `list_sketches`, `read_family_codes` | arbre de decision §3 rempli |
| 3 | Pilotage | executer la sequence d'ecriture | recipes `set_real_parameter` / `set_text_parameter` + `rebuild_document` | delta baseline -> post, valeurs relues |
| 4 | Validation | PROUVER que ca tient | recipes R du domaine + cross-checks (§5) | verdict ecrit OK/NOK justifie |
| 5 | Export | livrer | recipes RD (`export_step`...), `print_drafting`, `export_bom_csv` (R) | chemin(s) retournes et verifie(s) |

Convention de lecture : les recipes s'appellent via `topsolid_run_recipe` (params : recipe, value
optionnel `[SKILL]`) ; ci-dessous, seuls les noms de recipes sont listes pour alleger.

Un etage ne se franchit que vers le bas ; on remonte (de la validation vers la decomposition, ou de
la decomposition vers l'escalade) uniquement sur NOK ou decouverte. Ne jamais sauter du besoin client
directement a l'export : c'est le chemin des "OK" non prouves (§5).

## 2. Etapes 0-1 — Contexte et cadrage du besoin

### 2.1 Contexte (etape 0)
- `topsolid_get_state` : TOUJOURS premier appel (`[SKILL]`). Confirme connexion, document actif, projet.
- Taper le document : recipe `document_type` puis les `detect_*` du domaine (`detect_assembly`,
  `detect_family`, `detect_unfolding`, `detect_drafting`, `detect_bom`). Sur un unfolding,
  `detect_unfolding` affiche aussi "Source part: <nom PDM>" (pipeline verbatim `GetPartToUnfold` +
  `Pdm.GetName`, `[REC] l.687-702`).
- Mauvais document ouvert : `search_document` (nom), puis `open_document_by_name` (mode R, verbatim
  `[REC] l.2111` : l'ouverture est traitee comme lecture — ne modifie pas le PDM).

### 2.2 Cadrage (etape 1)
Le cadrage transforme la phrase du client en criteres mesurables ; ces criteres deviennent la
checklist de l'etape validation (§4-V). Un critere non cadre au depart ne pourra JAMAIS etre valide
ensuite. Cadrage minimum :

1. Document cible : lequel (nom/gabarit), existe-t-il ? (`search_document`)
2. Grandeur du changement : cotes cibles (mm), valeurs de parametres, variante de famille, code.
3. Unite de compte : NB de plis attendu (tolerie), NB de lignes/contenu BOM (assemblage), NB de
   pieces (`count_assembly_parts`).
4. Variantes : une execution ? une famille de variantes ? une serie (`copy_parameters_to`) ?
5. Sorties : quels exports (`export_step`, `export_dxf`, `export_pdf`, `export_stl`, `export_iges`,
   `print_drafting`) ; `list_exporters` pour les formats disponibles (`[SKILL]`).
6. Contraintes : materiel (`read_material`), PDM (designation/reference a poser), projet.

Questions standard si le client est vague — meme table que `[SKILL]` ("QUAND DEMANDER
CLARIFICATION") : element cible ? quel parametre/valeur ? quel format d'export ? quelle variante ?

## 3. Etape 2 — Arbre de decision de la decomposition

### 3.1 L'arbre (Q1 -> Q7)

Q1. **Le document cible existe et est type ?** `get_state` + `search_document` + `document_type`.
   Non existant -> c'est un GABARIT A CREER A LA MAIN (Q6) : la creation A-Z hors perimetre API
   (`[REF:concepts-core §6]`). Le dire explicitement au client avec la liste des parametres DDL
   attendus (§3.2) — c'est une prescription de conception, pas un refus.

Q2. **Une recipe existante couvre-t-elle le besoin tel quel ?** `topsolid_list_recipes` (17
   categories verbatim `[REC]`) puis `topsolid_get_recipe` pour le format exact de `value`.
   Oui -> passer directement a la sequence §4. Non -> Q3.

Q3. **Le besoin reduit-il a une PRIMITIVE verifiee ?** Seules creations de geometrie verifiees :
   chaine recipe `creer_esquisse_rectangle` + `extruder_esquisse` (profil rectangle extrude),
   et les entites de reference (point/axe/plan/repere, `[REF:concepts-core §6]`).
   Une piece simple et neuve sans gabarit -> chaine primitive. Tout composite (percages, plis,
   contraintes, formes complexes) -> Q4. En pratique pour un usage reproductible : meme une piece
   primitive merite un gabarit des la 2e occurrence.

Q4. **Quelles capacites exige le besoin SANS recipe ?** (branche caracteristique de la conception ;
   en depannage elle n'arrive qu'a la fin, ici elle arrive PENDANT la decomposition) -> probe de
   capacite §7.2, puis arbitrage V1/V2/V3 §7.3 NECESSAIRE avant de sequencer : si l'ecriture d'un
   parametre cle exige `topsolid_modify_script`, la sequence §4 doit le mentionner et le client doit
   savoir que cet appel n'est pas reproductible comme une recipe.

Q5. **Quel gabarit, quels parametres DDL ?** Ouvrir le gabarit, lire son etat :
   `read_parameters` (les parametres nommes), `read_operations` (arbre), `read_shapes`, `list_sketches`,
   `read_material`. Un parametre ne pilote la forme QUE SI le gabarit le consomme via SmartReal
   (`[REF:concepts-core §5]`) — creer un parametre de plus (`creer_parametre_reel`,
   `creer_parametre_formule`) sur un gabarit qui ne le consomme pas ne pilote RIEN.
   Gabarit sans DDL exploitable -> "gabarit a reprendre a la main" (prescription humaine, Q6), jamais
   script de contournement du gabarit.

Q6. **Comment structurer/retablir le gabarit si besoin ?** Prescription de conception a l'humain :
   cotes cles en parametres nommes consommes par SmartReal ; pour un composant catalogue, exposer le
   code/pilotes d'inclusion ; pour une cascade famille->code fragile, pattern "famille+code manuel"
   (ticket 238848, `[REF:assemblages-pilotes §3]`).

Q7. **Quelles verifications et quels exports ?** Table §3.3 (verifs, fixe le contenu de §5) et §6
   (exports). Cadres-les comme criteres chiffres (etape 1), pas comme "je regarderai apres".

### 3.2 Choisir les parametres pilotables (DDL) — criteres et voies

| Critere de choix | Detail |
|---|---|
| Cette cote DECIDE-t-elle du resultat ? | largeur/hauteur/epaisseur, positions, nombre d'occurrences, code de variante. Ne pas exposer les internes du feature tree. |
| Est-il deja un parametre nomme du gabarit ? | `read_parameters` + FriendlyName — le nom REEL se lit sur le gabarit, jamais devine (`[REF:concepts-core §3]`). |
| Type | Real (recipe `set_real_parameter`, value=name:SIvalue) ; Text (`set_text_parameter`, value=name:valeur) ; Integer : AUCUNE recipe d'ecriture dans le catalogue verbatim (PARAMETERS l.173-290, `[REC]`) -> arbitrage §7.3 (script one-shot ponctuel, ou issue de recipe) ; Boolean : idem, pas de recipe. |
| Est-il calculable ? | `creer_parametre_formule` (value=name:unite:formule, ex. DiagBolt:Length:Longueur * 1.414) — la formule rend la valeur derivee du pilote, pas un DDL a ecrire. |
| Est-il un code de variante d'inclusion ? | lire D'ABORD `read_family_codes` + `check_family_drivers` (`[REF:assemblages-pilotes §4]`) ; bascule via le parametre du gabarit si expose, sinon §7.3. |
| Unite | SI partout : m, rad, kg (`[SKILL]`, section "UNITES (TopSolid = SI)"). Entrer 0.08 pour 80 mm. Les recipes acceptent souvent les mm en entree — verifier le format de `value` via `topsolid_get_recipe`. |

Anti-pattern : ne JAMAIS "fabriquer" un DDL en renommant a distance un element systeme
(noms systeme traduits, `[REF:concepts-core §3]`) — c'est une creation de gabarit, pas un pilotage.

### 3.3 Choisir les verifications (par domaine) — avant de dire OK

| Domaine du gabarit | Recipes de controle (apres rebuild) | Cross-checks |
|---|---|---|
| Piece parametrique | `read_part_dimensions`, `read_bounding_box`, `read_mass_volume` | cotes relues == cotes cadrees (mm, conversion deja faite par la recipe, verbatim `[REC] l.1159`) ; masse dans un ordre de grandeur plausible vs baseline |
| Assemblage | `count_assembly_parts`, `read_occurrences`, `list_inclusions`, `detect_bom`, `read_bom_contents`, `count_bom_rows`, `audit_assembly` | occurrences vs BOM ; code variante relu apres rebuild ; assemblage "stable" : `IsPositioningUnderconstrained` (API uniquement, `[API-7.21]`, comportement si sous-contraint `[A VALIDER]` `[REF:assemblages-pilotes §8]`) |
| Variante de famille | `read_family_codes`, `check_family_drivers`, puis `compare_parameters` c. reference | pilotes TOUS avec designation avant ecriture (`fix_family_drivers` si besoin, `[REF:assemblages-pilotes §4]`) |
| Tolerie/unfolding | `detect_unfolding`, `read_bend_features`, `read_unfolding_dimensions`, `read_mass_volume` | `bends.Count` == propriete "Bends Number" — bonne pratique pas obligation API, verbatim l.704-716 et l.741-742 (`[REC]`, `[REF:tolerie §2]`) ; "Thickness"/"Sheet Metal" lues SUR l'unfolding |
| Mise en plan | `detect_drafting`, `list_drafting_views`, `read_drafting_scale` | echelle relue apres `set_drafting_scale` (verifier la valeur, pas le message) |
| Materiel | `read_material` | materiel non vide (`check_missing_materials` sur projet) |

Regles d'essence : on ne valide JAMAIS a la voix des messages RW ("OK: ...") mais a des RECETTES R
relues apres `rebuild_document` (§5). Et une validation dimensionnelle ne prouve pas un COMPTAGE
(plis, BOM, percages) : toujours la ligne cross-checks.

## 4. Etape 3 — Sequence de pilotage (la recette d'orchestration)

Sequence canonique (les noms sont de vraies recipes, lignes RecipeTool.cs verifiees ci-dessus) :

    1. baseline       read_parameters ( + lectures du domaine, table §3.3)   AVANT toute ecriture
    2. ecriture       set_real_parameter / set_text_parameter ( value en SI, une valeur par appel)
    3. reconstruction rebuild_document                                  ( mode RW, transactionnel)
    4. relecture      MEMES recipes R que la baseline (delta comparable)
    5. verdict        §5 — OK sinon retour diagnostic (V6)
    6. sauvegarde     save_document (doc actif) — save_all_project pour un projet multi-docs
    7. export         §6 — en dernier seulement

Regles d'orchestration :
- R1 **Baseline d'abord** : sans lecture "avant", aucun delta prouvable apres (§5). La baseline sert
  aussi a DETECTER les surprises (parametre absent, materiel manquant, assemblage strict) avant
  d'ecrire une seule valeur.
- R2 **Une ecriture = une confirmation** : le mode RW requiert l'accord du client par appel
  (`[SKILL]` §REGLES) ; ne jamais enchainer deux `set_*` dans le meme tour de parole non confirme.
- R3 **`rebuild_document` entre ecriture et relecture** : c'est le point de verite du feature tree
  (`[REF:concepts-core §3]`, arbre lineaire : l'aval se reconstruit). Rebuild = recipe RW
  (transactionnelle, verbatim `[REC] l.166`).
- R4 **Groupement** : pour un premier reglage d'un gabarit, aller pas-a-pas (set 1 -> rebuild ->
  relecture) ; ensuite, sur un gabarit deja qualifie, poser tout le lot de parametres puis un seul
  rebuild — comportement du rebuild sur ecritures rapprochees dans une meme transaction :
  `[A VALIDER]`.
- R5 **Unite SI** : 0.08 (m) = 80 mm ; angles en rad. Ne JAMAIS re-converter une valeur deja
  convertie affichee en mm par une recipe (`[REF:tolerie §2]`)..
- R6 **Serie de variantes** : derouler la meme sequence par occurrence ; reporter un jeu de parametres
  sur une autre piece : recipe `copy_parameters_to` (nom doc cible, `[SKILL]`) ; comparer les
  variantes : `compare_parameters` / `compare_document_operations` / `compare_document_entities`.

Mode d'emploi des outils au-dela des recipes : ordre `get_state` -> `run_recipe` -> `api_help` ->
scripts canonique de `[SKILL]` ; en conception il se COMPLETE par la matrice §7 (l'escalade est une
branche de l'arbre, pas un devoir en fin de chain).

## 5. Etape 4 — Les regles de validation avant de dire OK

- V0 `topsolid_get_state` encore : connexion et doc actif inchanges depuis la baseline.
- V1 Un message "OK: ..." d'une recipe RW est un ACCUSE D'ECRITURE, pas une preuve de resultat
  geometrique (verbatim `set_real_parameter` : `__message = "OK: " + name + "->" + value`,
  `[REC] l.220-240`, `[REF:gabarits-recipes §2.2]`). Preuve = relecture apres `rebuild_document`.
- V2 Dimensions : `read_part_dimensions` / `read_bounding_box` == criteres cadres (§2). Les
  conversions mm sont faites par les recipes (verbatim `val*1000`, `[REC] l.1159`, l.1126) — ne
  pas re-converter, ne pas re-decider l'unites.
- V3 Comptages et cross-checks du domaine (table §3.3) : NB de plis (bends != Bends Number = NOK),
  BOM `count_bom_rows` vs `count_assembly_parts` (assemblage), occurrences et code variante relus.
- V4 Variante de reference : `compare_parameters` ( et `compare_document_operations` /
  `compare_document_entities` si l'arbre ou la topologie doivent bouger) contre la variante de
  reference (`[SKILL]` §Navigation projet).
- V5 Masse/volume : sans valeur de reference, valider en PLAUSIBILITE (masse relue, delta documente
  vs baseline ; volume > 0). Un delta attendu et absent = NOK. Seuils chiffres : `[A VALIDER]`
  (depend du materiel et du gabarit).
- V6 Echec de rebuild, dimensions absentes ("No dimensions found" verbatim `[REC] l.1162`), valeurs
  incoherantes : NE PAS encahainer vers save/export. Diagnostic ordonne : `read_operations`
  (l'arbre dit si un noeud amont a casse), `audit_part` / `audit_assembly` (`[SKILL]`), puis retour
  a la baseline ou rollback manuel par re-set des valeurs de la baseline ( re-validation obligatoire).
  3 echecs consecutifs sur la meme sequence -> STOP, monter (issue ou fil de mission) avec les
  tentatives et leurs sorties `T-1206, reflexe repo`.
- V7 Le verdict s'ECRIT et se prouve : "OK — dims 80/40/3 mm reeles, plis 2/2, BOM 5 lignes" ou
  "NOK + ecart chiffre". Jamais "c'est fait" sans valeurs relues. NE JAMAIS marquer done sans preuve
  runtime (regle repo).
- V8 `save_document` (ou `save_all_project`) SEULEMENT apres V0-V7. Sauvegarder un etat non valide
  = produire une revision defaillante dans le PDM.

## 6. Etape 5 — Export

| Livrable | Recipe | Mode | Value / notes |
|---|---|---|---|
| STEP | `export_step` | RD | chemin optionnel (defaut : a cote du document, `[SKILL]`) |
| DXF | `export_dxf` | RD | mise a plat pour la decoupe (cas §8.3), chemin optionnel |
| PDF | `export_pdf` | RD | chemin optionnel |
| STL / IGES | `export_stl` / `export_iges` | RD | chemin optionnel |
| projet en batch | `batch_export_step` | RD | dossier optionnel (`[SKILL]`) |
| impression plan | `print_drafting` | RD | N&B, 300 DPI, a l'echelle — verbatim pipeline `Draftings.Print(... PrintToScale ...)` `[REF:gabarits-recipes §2.3]` |
| nomenclature (texte) | `export_bom_csv` | R | ATTENTION : retourne le TEXT des colonnes separees, n'ecrit PAS sur disque (verbatim `[REC] l.1922`) — pas un export RD |

Regles :
- E1 RD = ecriture disque -> confirmation obligatoire (`[SKILL]` §REGLES) ; un RD ne modifie rien au PDM
  (pas de save) — la chaine correcte reste : verdict OK -> save -> export.
- E2 `list_exporters` avant de promettre un format (`[SKILL]`).
- E3 l'export retourne le chemin ecrit (ex. "Export STEP OK: C:\...\fichier.step", `[SKILL]`) :
  le verifie, n'annonce JAMAIS un chemin non retourne par la recipe.
- E4 un DXF de decoupe est produit DEPUIS le document UNFOLDING (pas la piece pliee) ; chaine complete
  §8.3 et `[REF:tolerie §5]`.

## 7. Escalade API en contexte de conception

### 7.1 Changement de regard
En depannage, l'escalade est le dernier recours ; en conception elle est une branche NORMALE de
l'arbre (Q4) : la decomposition revele ce que le gabarit exige. La bonne question n'est PAS
"puis-je scripter ?" mais : **cette capacite est-elle wrappee, wrappable ou fermee ?** La reponse
decide de la reproductibilite de tout le pipeline.

### 7.2 Probe de capacite — ordre d'exploration (jamais d'ecriture pour tester)

    1. topsolid_list_recipes + topsolid_get_recipe   le besoin "existe deja" ? (17 categories verbatim)
    2. topsolid_api_help (query FR ou EN)            doc de la methode (graphe embarque)
    3. SKILL.md «Surface API 7.21» + server/data/api/7.21.304.0/methods.json (champ since)
       — le graphe d'api_help est ANTERIEUR a 7.21 (4119 aretes du 09/09) : une methode 7.20/7.21
       y manque ; la surface 7.21 du SKILL.md et le methods.json la connaissent.
    4. topsolid_find_path (Dijkstra entre 2 types) / topsolid_explore_paths (BFS multi-chemins)
       quand le besoin est "comment acceder a X depuis Y" (ex. IDocumentId -> IShapeId, `[SKILL]`).
    5. topsolid_search_examples / topsolid_search_commands   exemples et commandes de menu.
    6. topsolid_compile          valider qu'un extrait C# COMPILE — sans l'executer.

Interdits :
- explorer en ECRIVANT sur le modele : `topsolid_execute_script` est "NOT sandboxed and NOT
  read-only: it runs fully trusted and is auto-wrapped in a write transaction when it mutates. Do
  not auto-approve" (verbatim ExecuteScriptTool.cs l.32-34 `[REC]`) ;
- supposer une methode : un nom non prouve par methods.json/api_help n'existe pas. Cas reel
  (corrige en redaction des references) : `AddVertex` n'existe pas — le vrai nom est
  `CreateVertex` (CHM 7.21 + methods.json, `[REF:concepts-core §6]`) ;
- appeler `topsolid_search_doc` (n'existe pas encore — T-1206 C4).

### 7.3 Recipe manquante — 3 voies et criteres de decision

| Voie | Quand | Comment | Garde-fous |
|---|---|---|---|
| V1 — script one-shot | besoin PONCTUEL (revient pas), surtout pour LIRE/sonder (mesurer un etat non couvert, lister un element, verifier avant arbitrage) | `topsolid_execute_script` (lecture) ; `topsolid_modify_script` (ecriture) — C# 5 method-body-only, usings fixes, retour "..." (verbatim ExecuteScriptTool.cs l.35-48 `[REC]`, patron complet `[REF:gabarits-recipes §2.4]`) | api_help AVANT tout (verbatim l.37) ; ecriture = confirmation explicite, UN appel = UNE modification ; interdit de le mettre comme etape standard du pipeline |
| V2 — issue + recipe | besoin RECURRENT (2e occurrence), couvrable par un mode R/RW/RD, categorie existante | patron C# verbatim `[REF:gabarits-recipes §2]` (R/RW/RD, validation de `{value}`, `__message`) + processus `[REF:gabarits-recipes §5]` (issue, tests 3 suites, doc, CI, conventional commit) | mode correct : lecture->R, PDM->RW, disque->RD ; validation d'entree en tete (injection `{value}`, `[REF:gabarits-recipes §2.4]`) |
| V3 — ne pas piloter | capacite FERMEE de l'API (ex. creation de plis : aucune methode Fold/Bend/Unfold dans IShapes 7.21, `[REF:tolerie §3]`) | le dire au client ; bascule sur humain/gabarit | jamais de script qui "essaie quand meme" |

Decision en 2 questions :

    Qa. Le besoin reviendra-t-il (2e fois) ?            non -> V1 (lecture de preference)
    Qb. La methode API demandee existe-t-elle ?         non -> V3
        oui, mais hors recipes ?                    -> V2 si Qa=oui, V1 sinon si Qa=oui, V1 sinon

Regles d'escalade propres a la conception :
- Un pipeline de conception doit rester REPRODUCTIBLE : recipes nommees > script ad-hoc. Un script
  one-shot qui survit a sa session devient une dette : soit issue+recipe (V2), soit il sort du
  pipeline (rappel anti-pattern "dupliquer la logique d'une recipe cote agent", `[REF:gabarits-recipes §6]`).
- Cas integer/boolean des DDL : aucune recipe d'ecriture pour eux dans le catalogue verbatim (§3.2).
  Arbitrage type : besoin ponctuel (un seul reglage) -> script one-shot CONFIRME (lecture :
  execute_script ; ecriture : modify_script avec une methode API verifiee — api_help + champ since,
  existence `[A VALIDER]` avant tout appel, cf. `[REF:concepts-core §4]`) ; besoin recurrent
  (plusieurs variantes a derouler) -> issue + recipe (V2, patron RW standard), ou pattern de gabarit
  qui ramene le pilote sur un type wrappable (set_real/set_text, §3.2).
- Arret de boucle : 3 echecs consecutifs d'escalade (compile, crash, sortie incoherente) -> STOP :
  abandon de la voie, retour au plan gabarit/humain, signalement (issue ou fil de mission) AVEC les
  tentatives. Reference : la regle repo « 3 echecs consecutifs -> STOP et remonter » (canon Julien,
  memo session).

### 7.4 Anti-patterns d'escalade
- auto-approbation d'un script modifiant (interdit verbatim `[REC]`) ;
- methode 7.20/7.21 cherchee dans le graphe obsolete d'api_help puis declaree inexistante (elle vit
  dans la surface 7.21 / methods.json) ;
- recipe "inventee" appelee par son nom suppose (ex. "count_holes") : les noms se verifient par
  `topsolid_list_recipes`, un nom absent = V1/V2/V3, jamais une invocation ;
- script one-shot repete au lieu d'une recipe (dette silencieuse) ;
- ecrire dans une recipe RD une operation PDM, ou incliner un R vers RW par facilite (mode =
  transaction, `[REF:gabarits-recipes §2.3 et §2.4]`).

## 8. Cas files (tires du catalogue recipe, orchestres)

Tous les noms cites sont des recipes verbatim RecipeTool.cs (lignes verifiees ci-dessus) ; les
valeurs dimensionnelles sont ILLUSTRATIVES (pas de session TopSolid derriere ce document).
Chaque cas suit le pipeline §1 integralment : c'est la demonstration, pas la redite des references.

### 8.1 Cas A — Equerre a N lumieres (piece parametrique)

- **Besoin client** : "des equerres 80x40x3, avec N lumieres de diametre D, reparties" — famille de
  variantes a derouler.
- **Etape 0-1** : `topsolid_get_state` ; `search_document` "equerre"; `open_document_by_name`
  ; `document_type` (attendu : piece). Cadrage chiffre : L=80, H=40, E=3 mm, D=?, N=?, pas=? ; export
  voulu (STEP ? DXF ?).
- **Etape 2 (arbre)** : Q2 : aucune recipe ne cree une equerre — voulu : le modele est gabarit client, pas piece virtuelle.
  Q3 : pas une primitive simple (multi-percages). Q4 : probe — percages en LECTURE seule cote API
  (`IFeatures GetDrilling*Primitive`, 7.14, `[REF:gabarits-recipes §3.2]`) : la CREATION des lumieres
  reste dans le GABARIT (travail humain) (V3 pour la creation, pas de contournement). Q5 : DDL
  attendues du gabarit : Largeur/Hauteur/Epaisseur + NbLumieres + DiamLumiere — verifier par
  `read_parameters` que le gabarit les expose et les consomme (SmartReal, `[REF:concepts-core §5]`).
  Q7 : verifs chiffrees = cotes relues + masse vs baseline ; export = STEP (ou DXF).
- **Etape 3 (sequence)** :
    1. `read_parameters` — baseline (ex. Largeur = 0.08, NbLumieres = 2)
    2. `set_real_parameter` "Largeur:0.08", "Hauteur:0.04", "Epaisseur:0.003", "DiamLumiere:0.006"   (noms lus, SI)
    3. `rebuild_document`
    4. relectures (etape 4)
- Si NbLumieres est un parametre INTEGER : pas de recipe d'ecriture (§3.2) -> arbitrage §7.3 avant
  de sequencer (modify_script one-shot confirme, ou issue de recipe).
- **Etape 4 (valider)** : `read_part_dimensions` (attendu 80.0/40.0/3.0 mm, conversion recipe faite) ;
  `read_mass_volume` ; `read_bounding_box`. NB lumieres : AUCUNE recipe de comptage verifiee dans le
  catalogue (`[REC]` categories GEOMETRY/UNFOLDING, l.391-746) -> probe V1 lecture possible
  (`[A VALIDER : script de comptage drilling, jamais run en conditions reelles]`) ou validation par
  l'utilisateur. Cross-check masse vs baseline (delta signe du changement, V5).
- **Etapes 5-6 (livrer)** : `save_document` apres verdict ; puis `export_step` (chemin retourne, verifie).
- Variante suivante : re-set du lot + rebuild + relectures ; ou `copy_parameters_to` puis re-set des
  DDL differentielles.
- **Ce que ce cas enseigne** : la creation des lumieres et leur COMPTE restent hors API (V3) — prescrire le gabarit et cadrer
  un cross-check lisible, sans script de complaisance.

### 8.2 Cas B — Variante d'assemblage par code/famille + BOM

- **Besoin client** : "sur le gabarit d'assemblage du capot, bascule la variante vers le code 2000,
  verifie la nomenclature, exporte la BOM."
- **Etape 0-1** : `get_state`, `document_type` + `detect_assembly` ; cadrage : code cible 2000 (texte
  ? entier ? — le type se voit a `read_parameters`), BOM attendue (`count_bom_rows` cible), exports
  (texte BOM : `export_bom_csv` mode R).
- **Etape 2 (arbre)** : Q2 : les recettes assemblage couvrent la LECTURE
  (`list_inclusions`, `read_occurrences`, `count_assembly_parts`) ; la bascule de code n'a PAS de
  recipe : `SetInclusionCodeAndDrivers(2)` n'est pas wrappé
  (`[REF:assemblages-pilotes §3]`, `[REC-aucune-trace]`). Q4/V : 2 voies legitimes :
  (a) FIX A LA SOURCE : restructurer le gabarit pour que le code soit un parametre pilotable
  (pattern "famille+code manuel", ticket 238848, `[REF:assemblages-pilotes §3]`) — si le besoin est
  recurrent, c'est LA voie (reproductive, sans script) ;
  (b) voie V1 ponctuelle (§7.3) : `topsolid_modify_script` + `SetInclusionCodeAndDrivers2` apres
  api_help + methods.json (since 7.17, `[API-7.21]`), confirmation obligatoire — ponctuel seulement.
- **Etape 3 (sequence, voie a)** :
    1. `read_family_codes` + `check_family_drivers` sur la piece pilotee par le code — SI un pilote manque :
       `fix_family_drivers` d'abord (sinon la selection de code est fragile, `[REF:assemblages-pilotes §4]`)
    2. baseline : `read_occurrences`, `count_assembly_parts`, `read_bom_contents`, `count_bom_rows`
    3. `set_text_parameter` "Code:2000" (ou `set_real_parameter` si code numerique — type lu, pas devine)
    4. `rebuild_document`
- **Etape 4 (valider)** : relecture de tous les marqueurs (2) ; `compare_parameters` vs variante de
  reference (V4) ; cross-check BOM vs occurrences ; code relu via `read_*` (V1). Sous-contrainte :
  `IsPositioningUnderconstrained` cote API pour qualifier la stabilite (comportement
  `[A VALIDER]`, `[REF:assemblages-pilotes §8]`).
- **Etapes 5-6 (livrer)** : `save_document` (et `save_all_project` si plusieurs docs ouverts ont
  change — description verbatim `[REC] l.2100`) ; `export_bom_csv` (mode R : retourne le texte,
  `[REC] l.1922`) ; pas d'ecriture disque -> si fichier BOM requis, c'est une demande V1/V2 (aucune
  recipe RD "bom vers fichier" dans le catalogue).
- **Enseignements** : la bascule de variante la plus fiable est CELLE DU GABARIT (un parametre),
  pas celle d'un script API ; verifier que la BOM s'est reconstituee est autant important que le
  code pose.

### 8.3 Cas C — Mise a plat tolerie : verifier et livrer la decoupe

- **Besoin client** : "verifie la mise a plat de la piece de tolerie T-4321 et livre le DXF decoupe + le plan."
- **Etape 0-1** : `get_state` ; `search_document` "T-4321" ; `document_type`.
  `detect_unfolding` : confirme le type ET rend "Source part: <nom PDM>" (le gabarit source, verbatim
  `[REC] l.695-701`). Cadrage chiffre : NB de plis et epaisseur ATTENDUS (du plan), cotes du
  developpe attendues (lues sur l'unfolding, jamais recalculees — §8.3), export DXF (+ STEP remontee CAO si besoin, `[REF:tolerie §5]`).
- **Etape 2 (arbre)** : Q2 : les recipes de lecture couvrent le besoin integralement (detect/plis/dimensions,
  `[REF:tolerie §5]`) ; Q4 : creation/modification de plis = FERMEE (V3,
  `[REF:tolerie §3]`) ; le pilotage passe par les parametres du GABARIT SOURCE — jamais sur
  l'unfolding.
- **Etape 3 (sequence de CONTROLE, sans ecriture si la mise a plat est juste)** :
    1. `read_bend_features` — inventaire des plis (angles en degres, rayons/longueurs en mm, conversions faites par la recipe, verbatim l.715)
    2. `read_unfolding_dimensions` — "Bends Number" (Integer), "Thickness", Width/Length (mm), verbatim l.718-746
    3. cross-check : bends.Count == "Bends Number" ; Thickness == epaisseur cadre ; cotes du developpe == valeurs cadrees
- **Variant A — mise a plat conforme** : verdict OK (§5), `save_document`, puis `export_dxf`
  (decoupe) et `export_step` (remonte).
- **Variant B — ecart** : correction a la SOURCE : `open_document_by_name` sur la piece source (c'est son nom PDM
  qu'affiche `detect_unfolding`), baseline `read_parameters` du GABARIT, `set_real_parameter`,
  `rebuild_document`, retour sur l'unfolding, relire (1)-(3) + `save_document`. L'ecriture
  d'epaisseur n'a PAS de recipe dediee (`[A VALIDER : voie d'ecriture]`, `[REF:tolerie §3]`) —
  arbitrage §7.3 si le besoin est recurrent.
- **Etape 5 (plan)** : chaine mise en plan : `detect_drafting` / `open_document_by_name` /
  `set_drafting_scale` puis `read_drafting_scale` (relire la valeur posee, V1), `set_drafting_format`
  ; impression `print_drafting` (RD, a l'echelle).
- **Enseignements** : on ne recalcule JAMAIS un developpe (formule K, V de pli : noyau ferme IP,
  `[REF:tolerie §6]`) — on LIT les dimensions. L'ordre de pliage n'est pas expose (liste plate
  angle/rayon/longueur, `[REF:tolerie §4]`) : ne pas construire une sequence d'usinage dessus.

## 9. Pieges d'orchestration (recapitulatif)

| Piege | Detection | Parade | Reference |
|---|---|---|---|
| "OK" annonce sans relecture | messages RW acceptes comme preuve | V1 : relecture apres rebuild | §5 |
| Parametre cree mais muet | `creer_parametre_reel` sans SmartReal consommateur | Q5 : verifier la consommation dans le gabarit | `[REF:concepts-core §5]` |
| DDL integer/boolean sans recipe | catalogue verbatim PARAMETERS | §7.3 (script pointuel ou issue recipe) | §3.2 |
| Code de variante par script recurrent | modify_script repete | V2 : pattern gabarit ou recipe | §7.3, cas §8.2 |
| Unites re-converties / SI ignore | valeurs mm/rad incoherentes | R5 : SI en entree, ne pas re-converter les sorties | §4-R5 |
| Developpe recalcule | formule K-maison | V3 : lire, ne pas reimplémenter | `[REF:tolerie §6]` |
| Recette RD contaminee PDM / R glissée en RW | mode de la recipe | patrons verbatim, pas d'inclinaison | `[REF:gabarits-recipes §2]` |
| Script d'exploration qui ecrit | auto-approbation | probe SANS ecriture (§7.2), api_help avant | §7.2-7.4 |
| Export sur la piece pliee au lieu de l'unfolding | DXF de decoupe faux | E4 / chaine §8.3 | §6 |
| Save avant verdict | revision defaillante en PDM | V8 | §5 |
| Methode 7.21 declaree inexistante | api_help muet | surface 7.21 + methods.json (§7.2-3) | `[SKILL]` |
| 3 echecs consecutifs, on insiste | boucle compile/crash | V6 : stop, remonter | §5, §7.3 |

## 10. Checklist de conception (resume execute a chaque mission)

- [ ] `topsolid_get_state` + `document_type` / `detect_*` (etape 0)
- [ ] besoin cadre en criteres CHIFFRES (cotes, comptages, exports, chemins)
- [ ] recipes verifiees par `topsolid_list_recipes` + `topsolid_get_recipe` (noms reels, formats de value)
- [ ] DDL lues (`read_parameters`, `read_operations`, `read_shapes`, `list_sketches`) ET consommees par le gabarit
- [ ] capacites sans recipe arbitrees V1/V2/V3 (§7.3) AVANT de sequencer
- [ ] baseline lue avant toute ecriture
- [ ] ecritures confirmees une a une, valeurs SI
- [ ] `rebuild_document` entre ecritures et relectures
- [ ] validation : dimensions + cross-checks du domaine relues, verdict ecrit OK/NOK (§5)
- [ ] `save_document` / `save_all_project` APRES verdict seulement
- [ ] exports en dernier (RD confirmes), chemins retournes verifie
- [ ] escalades consignees : issue si recipe a creer, sinon note de mission
- [ ] `[A VALIDER]` restants listes au client, jamais presents comme des faits

Fin de la couche 3 (T-1206). Prochain maillon prevu au plan : couche 4 — RAG hybride et outil
`topsolid_search_doc` (non livre : ne pas appeler).