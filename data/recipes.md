# TopSolid MCP — Recettes (Patterns chaines)
# Ce fichier decrit comment combiner les appels API pour realiser des taches courantes.
# Chaque recette est un script C# 5 complet pour topsolid_execute_script.
# Les valeurs entre {ACCOLADES} sont des parametres a remplacer.

---

## TRANSACTION — Pattern D (obligatoire pour toute ecriture)

Toute modification DOIT suivre ce pattern. JAMAIS de modification sans StartModification.
TOUJOURS utiliser `topsolid_execute_script` (PAS topsolid_modify_script qui a un bug connu M-51).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
try
{
    TopSolidHost.Application.StartModification("Description", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // *** docId a potentiellement CHANGE ici (nouvelle revision) ***

    // TOUTE recherche d'elements (GetParameters, GetFunctions, etc.)
    // DOIT etre faite ICI, APRES EnsureIsDirty, avec le nouveau docId
    // Les ElementId obtenus AVANT EnsureIsDirty ne sont plus valides !

    // ... recherches + modifications ici ...

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

**Regles absolues :**
- `EnsureIsDirty(ref docId)` — TOUJOURS `ref`, jamais sans
- **docId CHANGE apres EnsureIsDirty** — re-chercher tous les elements avec le nouveau docId
- `EndModification(false, false)` dans le catch — sinon TopSolid bloque
- Unites SI : 50mm = `0.05`, 1m = `1.0`, 45deg = `0.785398` (radians)
- NE PAS utiliser `topsolid_modify_script` — son wrapper a un bug (M-51)

---

## R-001 : Ouvrir un document par nom (S-023)
Pattern: READ+WRITE | Scenarios: S-005, S-023
Piege: SearchDocumentByName fait un CONTAINS, pas un exact match.
Piege: GetDocument retourne un DocumentId, Open prend ref DocumentId.

```csharp
var projectId = TopSolidHost.Pdm.GetCurrentProject();
string searchName = "{NOM_DOCUMENT}";
var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, searchName);
if (results.Count == 0) return "Document '" + searchName + "' non trouve.";

var sb = new System.Text.StringBuilder();
foreach (var pdmId in results)
{
    string name = TopSolidHost.Pdm.GetName(pdmId);
    // Exact match ou Contains selon le besoin
    if (name == searchName || name.Contains(searchName))
    {
        DocumentId docId = TopSolidHost.Documents.GetDocument(pdmId);
        sb.AppendLine("Trouve: " + name + " (DocId=" + docId + ")");
    }
}
return sb.ToString();
```

---

## R-002 : Modifier un parametre reel (S-043)
Pattern: WRITE | Scenarios: S-043
Piege: La valeur est en SI (metres). 50mm = 0.05, 200mm = 0.2.
Piege: Trouver le parametre par FriendlyName, pas par Name interne.
Piege: CHERCHER le parametre APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string paramName = "{NOM_PARAMETRE}";
double newValue = {VALEUR_SI}; // ex: 0.2 pour 200mm

try
{
    TopSolidHost.Application.StartModification("Modify " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId targetParam = ElementId.Empty;
    foreach (var p in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(p).Contains(paramName))
        {
            targetParam = p;
            break;
        }
    }
    if (targetParam.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }
    TopSolidHost.Parameters.SetRealValue(targetParam, newValue);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + paramName + " = " + newValue;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-003 : Naviguer dans l'arbre PDM recursif (S-006)
Pattern: READ | Scenarios: S-006, S-002, S-003
Piege: GetConstituents a des parametres `out`, utiliser des variables List<PdmObjectId>.

```csharp
var projectId = TopSolidHost.Pdm.GetCurrentProject();
string projectName = TopSolidHost.Pdm.GetName(projectId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Projet: " + projectName);

List<PdmObjectId> folders;
List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(projectId, out folders, out docs);

foreach (var f in folders)
{
    string fname = TopSolidHost.Pdm.GetName(f);
    sb.AppendLine("  [Dossier] " + fname);

    List<PdmObjectId> subFolders;
    List<PdmObjectId> subDocs;
    TopSolidHost.Pdm.GetConstituents(f, out subFolders, out subDocs);
    foreach (var d in subDocs)
    {
        string ext;
        TopSolidHost.Pdm.GetType(d, out ext);
        sb.AppendLine("    " + TopSolidHost.Pdm.GetName(d) + " (" + ext + ")");
    }
}
foreach (var d in docs)
{
    string ext;
    TopSolidHost.Pdm.GetType(d, out ext);
    sb.AppendLine("  " + TopSolidHost.Pdm.GetName(d) + " (" + ext + ")");
}
return sb.ToString();
```

---

## R-004 : Exporter en STEP (S-113)
Pattern: WRITE | Scenarios: S-110, S-112, S-113
Piege: Trouver le bon index exporteur en bouclant sur ExporterCount.
Piege: Le chemin de sortie doit etre un chemin complet avec extension.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

// Trouver l'index de l'exporteur STEP
int stepIndex = -1;
int count = TopSolidHost.Application.ExporterCount;
for (int i = 0; i < count; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains("stp") || ext.ToLower().Contains("step"))
        {
            stepIndex = i;
            break;
        }
    }
    if (stepIndex >= 0) break;
}
if (stepIndex < 0) return "Exporteur STEP non trouve.";

if (!TopSolidHost.Documents.CanExport(stepIndex, docId))
    return "Ce document ne peut pas etre exporte en STEP.";

string docName = TopSolidHost.Documents.GetName(docId);
string outputPath = "{CHEMIN_SORTIE}\\" + docName + ".stp";
TopSolidHost.Documents.Export(stepIndex, docId, outputPath);
return "Exporte: " + outputPath;
```

---

## R-005 : Creer un parametre reel (S-047)
Pattern: WRITE | Scenarios: S-047, S-048, S-049, S-050

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string paramName = "{NOM_PARAMETRE}";
double value = {VALEUR_SI}; // ex: 0.1 pour 100mm

try
{
    TopSolidHost.Application.StartModification("Create " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // UnitType : 0=None, 1=Length, 2=Angle, 3=Area, etc.
    ElementId newParam = TopSolidHost.Parameters.CreateRealParameter(docId, UnitType.Length, value);
    TopSolidHost.Elements.SetName(newParam, paramName);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Parametre '" + paramName + "' cree avec valeur " + value;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-006 : Copier parametres entre documents (S-052)
Pattern: READ+WRITE | Scenarios: S-052
Piege: Lire la source AVANT StartModification sur la cible.

```csharp
// Phase 1 : lire les parametres de la source
string sourceName = "{NOM_SOURCE}";
var projectId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, sourceName);
if (results.Count == 0) return "Source '" + sourceName + "' non trouvee.";
DocumentId sourceDoc = TopSolidHost.Documents.GetDocument(results[0]);

var parameters = TopSolidHost.Parameters.GetParameters(sourceDoc);
var paramData = new List<string>();
foreach (var p in parameters)
{
    var pType = TopSolidHost.Parameters.GetParameterType(p);
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    string val = "";
    if (pType == ParameterType.Real) val = TopSolidHost.Parameters.GetRealValue(p).ToString();
    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);
    else continue;
    paramData.Add(name + "|" + pType + "|" + val);
}

// Phase 2 : creer sur la cible (document actif)
DocumentId targetDoc = TopSolidHost.Documents.EditedDocument;
try
{
    TopSolidHost.Application.StartModification("Copy params from " + sourceName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref targetDoc);

    int created = 0;
    foreach (string entry in paramData)
    {
        string[] parts = entry.Split('|');
        string name = parts[0];
        string type = parts[1];
        string val = parts[2];

        ElementId newParam = ElementId.Empty;
        if (type == "Real")
            newParam = TopSolidHost.Parameters.CreateRealParameter(targetDoc, UnitType.None, double.Parse(val));
        else if (type == "Integer")
            newParam = TopSolidHost.Parameters.CreateIntegerParameter(targetDoc, int.Parse(val));
        else if (type == "Boolean")
            newParam = TopSolidHost.Parameters.CreateBooleanParameter(targetDoc, bool.Parse(val));
        else if (type == "Text")
            newParam = TopSolidHost.Parameters.CreateTextParameter(targetDoc, val);

        if (!newParam.IsEmpty)
        {
            TopSolidHost.Elements.SetName(newParam, name);
            created++;
        }
    }

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(targetDoc);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + created + " parametres copies depuis " + sourceName;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-007 : Lire masse, volume, surface (S-040 via proprietes systeme)
Pattern: READ | Scenarios: S-040
Piege: Mass/Volume/Surface sont des parametres systeme, pas utilisateur.
Piege: GetRealValue retourne en SI (kg, m3, m2).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

var parameters = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Proprietes physiques de " + TopSolidHost.Documents.GetName(docId) + ":");

foreach (var p in parameters)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    if (name == "Mass" || name == "Volume" || name == "Surface Area"
        || name == "Masse" || name == "Volume" || name == "Surface")
    {
        var pType = TopSolidHost.Parameters.GetParameterType(p);
        if (pType == ParameterType.Real)
        {
            double val = TopSolidHost.Parameters.GetRealValue(p);
            sb.AppendLine("  " + name + " = " + val);
        }
    }
}
if (sb.Length < 50) sb.AppendLine("  (aucune propriete physique trouvee)");
return sb.ToString();
```

---

## R-008 : Supprimer un parametre (S-051)
Pattern: WRITE | Scenarios: S-051
Piege: CHERCHER le parametre APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string paramName = "{NOM_PARAMETRE}";

try
{
    TopSolidHost.Application.StartModification("Delete " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId target = ElementId.Empty;
    foreach (var p in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(p) == paramName)
        {
            target = p;
            break;
        }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }
    TopSolidHost.Elements.Delete(target);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Parametre '" + paramName + "' supprime.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-009 : Renommer un document (S-024)
Pattern: WRITE | Scenarios: S-024

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string oldName = TopSolidHost.Documents.GetName(docId);
string newName = "{NOUVEAU_NOM}";

try
{
    TopSolidHost.Application.StartModification("Rename to " + newName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    TopSolidHost.Documents.SetName(docId, newName);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: '" + oldName + "' renomme en '" + newName + "'";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-010 : Lire metadata PDM d'un document (S-007)
Pattern: READ | Scenarios: S-007, S-011

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Document: " + TopSolidHost.Pdm.GetName(pdmId));

string ext;
TopSolidHost.Pdm.GetType(pdmId, out ext);
sb.AppendLine("Type: " + ext);
sb.AppendLine("Description: " + TopSolidHost.Pdm.GetDescription(pdmId));
sb.AppendLine("Reference: " + TopSolidHost.Pdm.GetPartNumber(pdmId));
sb.AppendLine("Fabricant: " + TopSolidHost.Pdm.GetManufacturer(pdmId));
sb.AppendLine("Ref fabricant: " + TopSolidHost.Pdm.GetManufacturerPartNumber(pdmId));

return sb.ToString();
```

---

---

## R-011 : Modifier parametre texte (S-044)
Pattern: WRITE
Piege: CHERCHER le parametre APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string paramName = "{NOM_PARAMETRE}";
string newValue = "{NOUVELLE_VALEUR}";

try
{
    TopSolidHost.Application.StartModification("Set " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId target = ElementId.Empty;
    foreach (var p in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(p).Contains(paramName))
        { target = p; break; }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }
    TopSolidHost.Parameters.SetTextValue(target, newValue);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + paramName + " = " + newValue;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-012 : Modifier parametre booleen (S-045)
Pattern: WRITE
Piege: CHERCHER le parametre APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string paramName = "{NOM_PARAMETRE}";
bool newValue = {true_ou_false};

try
{
    TopSolidHost.Application.StartModification("Set " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId target = ElementId.Empty;
    foreach (var p in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(p).Contains(paramName))
        { target = p; break; }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }
    TopSolidHost.Parameters.SetBooleanValue(target, newValue);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + paramName + " = " + newValue;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-013 : Modifier parametre enumeration (S-046)
Pattern: WRITE
Piege: SetEnumerationValue prend un int (index de la valeur dans l'enum).
Piege: CHERCHER le parametre APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string paramName = "{NOM_PARAMETRE}";
int newValue = {INDEX_ENUM}; // 0, 1, 2, etc.

try
{
    TopSolidHost.Application.StartModification("Set " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId target = ElementId.Empty;
    foreach (var p in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(p).Contains(paramName))
        { target = p; break; }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }
    TopSolidHost.Parameters.SetEnumerationValue(target, newValue);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + paramName + " = enum[" + newValue + "]";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-014 : Creer parametre avec formule SmartReal (S-053)
Pattern: WRITE
Piege: La formule reference d'autres parametres par nom interne.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string paramName = "{NOM_PARAMETRE}";
string formula = "{FORMULE}"; // ex: "Longueur * 2" ou "floor(Hauteur/0.18)"

try
{
    TopSolidHost.Application.StartModification("Create SmartReal " + paramName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    var smartReal = new SmartReal(formula);
    ElementId newParam = TopSolidHost.Parameters.CreateSmartRealParameter(docId, smartReal);
    TopSolidHost.Elements.SetName(newParam, paramName);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: SmartReal '" + paramName + "' = " + formula;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-015 : Export avec options — FBX, GLTF, Parasolid (S-114)
Pattern: WRITE
Piege: Les options sont des KeyValue, il faut connaitre les cles.
Piege: Pour FBX/GLTF, on peut specifier la representation.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string targetExt = "{EXTENSION}"; // "fbx", "gltf", "x_t"
string outputPath = "{CHEMIN_SORTIE}";

// Trouver l'index de l'exporteur
int exporterIndex = -1;
int count = TopSolidHost.Application.ExporterCount;
for (int i = 0; i < count; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains(targetExt))
        { exporterIndex = i; break; }
    }
    if (exporterIndex >= 0) break;
}
if (exporterIndex < 0) return "Exporteur '" + targetExt + "' non trouve.";

if (!TopSolidHost.Documents.CanExport(exporterIndex, docId))
    return "Ce document ne peut pas etre exporte en " + targetExt;

// Lire les options par defaut et les utiliser
var options = TopSolidHost.Application.GetExporterOptions(exporterIndex);
string docName = TopSolidHost.Documents.GetName(docId);
string fullPath = outputPath + "\\" + docName + "." + targetExt;
TopSolidHost.Documents.ExportWithOptions(exporterIndex, options, docId, fullPath);
return "Exporte: " + fullPath;
```

---

## R-016 : Importer STEP avec options (S-115)
Pattern: READ+WRITE
Piege: Import modifie le document actif, besoin de Pattern D.
Piege: Il faut trouver l'index importeur comme pour l'export.

```csharp
string importPath = "{CHEMIN_FICHIER_STEP}";
DocumentId docId = TopSolidHost.Documents.EditedDocument;

// Trouver l'index importeur STEP
int importerIndex = -1;
int count = TopSolidHost.Application.ImporterCount;
for (int i = 0; i < count; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetImporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains("stp") || ext.ToLower().Contains("step"))
        { importerIndex = i; break; }
    }
    if (importerIndex >= 0) break;
}
if (importerIndex < 0) return "Importeur STEP non trouve.";

var options = TopSolidHost.Application.GetImporterOptions(importerIndex);
try
{
    TopSolidHost.Application.StartModification("Import STEP", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    TopSolidHost.Documents.ImportWithOptions(importerIndex, options, docId, importPath);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Importe " + importPath;
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-017 : Check-in / Check-out document (S-018)
Pattern: READ+WRITE
Piege: Check-in necessite que le document soit sauvegarde.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string action = "{ACTION}"; // "checkin" ou "checkout" ou "undo_checkout"

if (action == "checkout")
{
    TopSolidHost.Pdm.CheckOut(pdmId);
    return "OK: Document checked out.";
}
else if (action == "checkin")
{
    TopSolidHost.Pdm.CheckIn(pdmId, true);
    return "OK: Document checked in.";
}
else if (action == "undo_checkout")
{
    TopSolidHost.Pdm.UndoCheckOut(pdmId);
    return "OK: Checkout annule.";
}
return "Action inconnue: " + action;
```

---

## R-018 : Lire historique revisions (S-028)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Revisions de " + TopSolidHost.Pdm.GetName(pdmId) + ":");

var majors = TopSolidHost.Pdm.GetMajorRevisions(pdmId);
foreach (var major in majors)
{
    string majorText = TopSolidHost.Pdm.GetMajorRevisionText(major);
    string state = TopSolidHost.Pdm.GetMajorRevisionLifeCycleMainState(major).ToString();
    sb.AppendLine("  Major " + majorText + " (etat: " + state + ")");

    var minors = TopSolidHost.Pdm.GetMinorRevisions(major);
    foreach (var minor in minors)
    {
        string minorText = TopSolidHost.Pdm.GetMinorRevisionText(minor);
        sb.AppendLine("    Minor " + minorText);
    }
}
return sb.ToString();
```

---

## R-019 : Lister tous les exporteurs/importeurs (S-110, S-111)
Pattern: READ

```csharp
var sb = new System.Text.StringBuilder();

sb.AppendLine("=== EXPORTEURS ===");
int expCount = TopSolidHost.Application.ExporterCount;
for (int i = 0; i < expCount; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    sb.AppendLine("[" + i + "] " + typeName + " (" + string.Join(", ", extensions) + ")");
}

sb.AppendLine("\n=== IMPORTEURS ===");
int impCount = TopSolidHost.Application.ImporterCount;
for (int i = 0; i < impCount; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetImporterFileType(i, out typeName, out extensions);
    sb.AppendLine("[" + i + "] " + typeName + " (" + string.Join(", ", extensions) + ")");
}
return sb.ToString();
```

---

## R-020 : Reconstruire et sauvegarder un document (S-026, S-027)
Pattern: WRITE

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

try
{
    TopSolidHost.Application.StartModification("Rebuild", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    TopSolidHost.Documents.Rebuild(docId);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Document reconstruit et sauvegarde.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

# ============================================================
# TIER 2 — Esquisses, Assemblages, Familles, Geometrie 3D
# ============================================================

## R-030 : Creer une esquisse sur le plan XY (S-061)
Pattern: WRITE
Piege: CreateSketchIn3D prend un plan, un point, un bool et une direction.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

try
{
    TopSolidHost.Application.StartModification("Create Sketch", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    ElementId plane = TopSolidHost.Geometries3D.GetAbsoluteXYPlane(docId);
    ElementId origin = TopSolidHost.Geometries3D.GetAbsoluteOriginPoint(docId);
    ElementId yAxis = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId);

    ElementId sketch = TopSolidHost.Sketches2D.CreateSketchIn3D(docId, plane, origin, false, yAxis);
    TopSolidHost.Elements.SetName(sketch, "{NOM_ESQUISSE}");

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Esquisse creee.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-031 : Creer un rectangle dans une esquisse (S-062)
Pattern: WRITE
Piege: Sketches2D.StartModification/EndModification distinct du Pattern D.
Piege: Les coordonnees 2D sont en metres SI.
Piege: CHERCHER l'esquisse APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
double w = {LARGEUR_M}; // ex: 0.1 pour 100mm
double h = {HAUTEUR_M}; // ex: 0.05 pour 50mm

try
{
    TopSolidHost.Application.StartModification("Draw Rectangle", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var functions = TopSolidHost.Entities.GetFunctions(docId);
    ElementId sketchId = ElementId.Empty;
    foreach (var f in functions)
    {
        string typeName = TopSolidHost.Elements.GetTypeFullName(f);
        if (typeName.Contains("Sketch"))
        { sketchId = f; } // prend le dernier
    }
    if (sketchId.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Aucune esquisse trouvee.";
    }

    TopSolidHost.Sketches2D.StartModification(sketchId);

    var p1 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, 0));
    var p2 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, 0));
    var p3 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(w, h));
    var p4 = TopSolidHost.Sketches2D.CreateVertex(new Point2D(0, h));

    var s1 = TopSolidHost.Sketches2D.CreateLineSegment(p1, p2);
    var s2 = TopSolidHost.Sketches2D.CreateLineSegment(p2, p3);
    var s3 = TopSolidHost.Sketches2D.CreateLineSegment(p3, p4);
    var s4 = TopSolidHost.Sketches2D.CreateLineSegment(p4, p1);

    var segments = new List<ElementId> { s1, s2, s3, s4 };
    TopSolidHost.Sketches2D.CreateProfile(segments);

    TopSolidHost.Sketches2D.EndModification();

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Rectangle " + (w * 1000) + "x" + (h * 1000) + "mm cree.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-032 : Creer une extrusion depuis la derniere esquisse (S-063)
Pattern: WRITE
Piege: CreateExtrudedShape prend le profil de l'esquisse, une direction, une distance.
Piege: CHERCHER l'esquisse APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
double distance = {DISTANCE_M}; // ex: 0.02 pour 20mm

try
{
    TopSolidHost.Application.StartModification("Extrude", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var functions = TopSolidHost.Entities.GetFunctions(docId);
    ElementId sketchId = ElementId.Empty;
    foreach (var f in functions)
    {
        string typeName = TopSolidHost.Elements.GetTypeFullName(f);
        if (typeName.Contains("Sketch")) sketchId = f;
    }
    if (sketchId.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Aucune esquisse trouvee.";
    }

    ElementId yAxis = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId);
    // section = le sketch, direction = axe Z normal au plan, distance en metres
    ElementId shape = TopSolidHost.Shapes.CreateExtrudedShape(
        docId, sketchId, yAxis, distance, 0.0, true, false);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Extrusion " + (distance * 1000) + "mm creee.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-033 : Creer un point 3D (S-072)
Pattern: WRITE

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
double x = {X_M}; // metres
double y = {Y_M};
double z = {Z_M};

try
{
    TopSolidHost.Application.StartModification("Create Point", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    ElementId point = TopSolidHost.Geometries3D.CreatePoint(docId, new Point3D(x, y, z));
    TopSolidHost.Elements.SetName(point, "{NOM_POINT}");

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Point cree a (" + x + ", " + y + ", " + z + ")";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-034 : Creer un repere (frame) 3D (S-073)
Pattern: WRITE

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;

try
{
    TopSolidHost.Application.StartModification("Create Frame", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    var frame = new Frame3D(
        new Point3D({X_M}, {Y_M}, {Z_M}),     // origine
        new Direction3D(1, 0, 0),               // axe X
        new Direction3D(0, 1, 0),               // axe Y
        new Direction3D(0, 0, 1)                // axe Z
    );
    ElementId frameId = TopSolidHost.Geometries3D.CreateFrame(docId, frame);
    TopSolidHost.Elements.SetName(frameId, "{NOM_REPERE}");

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Repere cree.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-035 : Lister les inclusions d'un assemblage (S-081, S-082)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var sb = new System.Text.StringBuilder();
sb.AppendLine("Inclusions de " + TopSolidHost.Documents.GetName(docId) + ":");

var functions = TopSolidHost.Entities.GetFunctions(docId);
int count = 0;
foreach (var f in functions)
{
    string typeName = TopSolidHost.Elements.GetTypeFullName(f);
    if (typeName.Contains("Inclusion"))
    {
        count++;
        string name = TopSolidHost.Elements.GetFriendlyName(f);
        try
        {
            DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(f);
            string defName = TopSolidHost.Documents.GetName(defDoc);
            sb.AppendLine("  " + name + " → " + defName);
        }
        catch
        {
            sb.AppendLine("  " + name + " → (definition non resolue)");
        }
    }
}
sb.AppendLine("Total: " + count + " inclusion(s)");
return sb.ToString();
```

---

## R-036 : Creer une inclusion simple dans un assemblage (S-083)
Pattern: WRITE
Piege: Le document assemblage doit etre actif.
Piege: Il faut le PdmObjectId du document a inclure, pas le DocumentId.

```csharp
DocumentId asmDoc = TopSolidHost.Documents.EditedDocument;
string partName = "{NOM_PIECE_A_INCLURE}";

// Trouver la piece
var projectId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, partName);
if (results.Count == 0) return "Piece '" + partName + "' non trouvee.";
DocumentId partDoc = TopSolidHost.Documents.GetDocument(results[0]);

try
{
    TopSolidHost.Application.StartModification("Include " + partName, false);
    TopSolidHost.Documents.EnsureIsDirty(ref asmDoc);

    ElementId positioning = TopSolidDesignHost.Assemblies.CreatePositioning(asmDoc);
    ElementId inclusion = TopSolidDesignHost.Assemblies.CreateInclusion2(
        asmDoc, positioning, "", partDoc, new List<SmartObject>());

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(asmDoc);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: " + partName + " incluse dans l'assemblage.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-037 : Detecter et lire info famille (S-100, S-101, S-102, S-103)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var sb = new System.Text.StringBuilder();
string docName = TopSolidHost.Documents.GetName(docId);

bool isFamily = TopSolidHost.Families.IsFamily(docId);
sb.AppendLine("Document: " + docName);
sb.AppendLine("Est une famille: " + isFamily);

if (!isFamily) return sb.ToString();

bool isExplicit = TopSolidHost.Families.IsExplicit(docId);
sb.AppendLine("Type: " + (isExplicit ? "Explicite" : "Generique"));

// Codes
var codes = TopSolidHost.Families.GetCodes(docId);
sb.AppendLine("Codes (" + codes.Count + "):");
foreach (var code in codes) sb.AppendLine("  - " + code);

// Document generique
try
{
    DocumentId genericDoc = TopSolidHost.Families.GetGenericDocument(docId);
    if (!genericDoc.IsEmpty)
        sb.AppendLine("Document generique: " + TopSolidHost.Documents.GetName(genericDoc));
}
catch { }

// Instances explicites
if (isExplicit)
{
    List<string> instanceCodes;
    List<DocumentId> instanceDocs;
    TopSolidHost.Families.GetExplicitInstances(docId, out instanceCodes, out instanceDocs);
    sb.AppendLine("Instances (" + instanceCodes.Count + "):");
    for (int i = 0; i < instanceCodes.Count; i++)
    {
        string iName = TopSolidHost.Documents.GetName(instanceDocs[i]);
        sb.AppendLine("  [" + instanceCodes[i] + "] → " + iName);
    }
}
return sb.ToString();
```

---

## R-038 : Creer famille explicite + ajouter instances (S-104, S-105, S-106)
Pattern: WRITE
Piege: SetAsExplicit AVANT AddExplicitInstance.
Piege: Chaque instance a un code (string) unique.

```csharp
string familyName = "{NOM_FAMILLE}";
var projectId = TopSolidHost.Pdm.GetCurrentProject();

// Creer le document famille
PdmObjectId familyPdm = TopSolidHost.Pdm.CreateDocument(projectId, ".TopFam", true);
TopSolidHost.Pdm.SetName(familyPdm, familyName);
DocumentId familyDoc = TopSolidHost.Documents.GetDocument(familyPdm);

try
{
    TopSolidHost.Application.StartModification("Create Family", false);
    TopSolidHost.Documents.EnsureIsDirty(ref familyDoc);

    TopSolidHost.Families.SetAsExplicit(familyDoc);

    // Ajouter des instances (pieces existantes)
    // {INSTANCES} = liste de paires code:nomPiece
    string[] instances = new string[] { "{CODE1}:{NOM_PIECE1}", "{CODE2}:{NOM_PIECE2}" };
    foreach (string inst in instances)
    {
        string[] parts = inst.Split(':');
        string code = parts[0];
        string pieceName = parts[1];
        var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, pieceName);
        if (results.Count > 0)
        {
            DocumentId instanceDoc = TopSolidHost.Documents.GetDocument(results[0]);
            TopSolidHost.Families.AddExplicitInstance(familyDoc, code, instanceDoc);
        }
    }

    TopSolidHost.Application.EndModification(true, true);
    TopSolidHost.Pdm.Save(familyPdm, true);
    return "OK: Famille '" + familyName + "' creee avec instances.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-039 : Transformer un element (translation/rotation) (S-077)
Pattern: WRITE
Piege: CHERCHER l'element APRES EnsureIsDirty (docId change).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;

// Translation de {DX}, {DY}, {DZ} metres
double dx = {DX_M};
double dy = {DY_M};
double dz = {DZ_M};
string elementName = "{NOM_ELEMENT}";

try
{
    TopSolidHost.Application.StartModification("Transform", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId a change — rechercher MAINTENANT
    var functions = TopSolidHost.Entities.GetFunctions(docId);
    ElementId target = ElementId.Empty;
    foreach (var f in functions)
    {
        if (TopSolidHost.Elements.GetFriendlyName(f).Contains(elementName))
        { target = f; break; }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Element '" + elementName + "' non trouve.";
    }

    var translation = Transform3D.CreateTranslation(new Vector3D(dx, dy, dz));
    TopSolidHost.Entities.Transform(target, translation);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Element deplace de (" + dx + ", " + dy + ", " + dz + ")";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-040 : Lister les esquisses du document (S-060)
Pattern: READ
Piege: GetSketches retourne les esquisses du dossier Sketches uniquement.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

var sketches = TopSolidHost.Sketches2D.GetSketches(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Esquisses (" + sketches.Count + "):");
foreach (ElementId sketch in sketches)
{
    string name = TopSolidHost.Elements.GetFriendlyName(sketch);
    Plane3D plane = TopSolidHost.Sketches2D.GetPlane(sketch);
    var profiles = TopSolidHost.Sketches2D.GetProfiles(sketch);
    sb.AppendLine("  " + name + " — " + profiles.Count + " profil(s)");
}
return sb.ToString();
```

---

## R-041 : Lire les faces d'un shape (S-071)
Pattern: READ
Piege: GetFaceArea retourne en m2. Pour cm2 multiplier par 10000.
Piege: GetFaceSurfaceType retourne un enum SurfaceType (Plane, Cylinder, Sphere, Cone, Torus, BSpline...).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

var shapes = TopSolidHost.Shapes.GetShapes(docId);
if (shapes.Count == 0) return "Aucun shape dans le document.";

var sb = new System.Text.StringBuilder();
foreach (ElementId shape in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(shape);
    int faceCount = TopSolidHost.Shapes.GetFaceCount(shape);
    int edgeCount = TopSolidHost.Shapes.GetEdgeCount(shape);
    sb.AppendLine("Shape: " + name + " — " + faceCount + " faces, " + edgeCount + " aretes");

    var faces = TopSolidHost.Shapes.GetFaces(shape);
    foreach (var face in faces)
    {
        double area = TopSolidHost.Shapes.GetFaceArea(face);
        SurfaceType surfType = TopSolidHost.Shapes.GetFaceSurfaceType(face);
        sb.AppendLine("  Face: " + surfType + " — " + (area * 10000).ToString("F2") + " cm2");
    }
}
return sb.ToString();
```

---

## R-042 : Lire plans et axes absolus (S-076)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

var sb = new System.Text.StringBuilder();

ElementId xyPlane = TopSolidHost.Geometries3D.GetAbsoluteXYPlane(docId);
ElementId xzPlane = TopSolidHost.Geometries3D.GetAbsoluteXZPlane(docId);
ElementId yzPlane = TopSolidHost.Geometries3D.GetAbsoluteYZPlane(docId);
sb.AppendLine("Plan XY: " + xyPlane);
sb.AppendLine("Plan XZ: " + xzPlane);
sb.AppendLine("Plan YZ: " + yzPlane);

ElementId frame = TopSolidHost.Geometries3D.GetAbsoluteFrame(docId);
ElementId origin = TopSolidHost.Geometries3D.GetAbsoluteOriginPoint(docId);
ElementId xAxis = TopSolidHost.Geometries3D.GetAbsoluteXAxis(docId);
ElementId yAxis = TopSolidHost.Geometries3D.GetAbsoluteYAxis(docId);
ElementId zAxis = TopSolidHost.Geometries3D.GetAbsoluteZAxis(docId);
sb.AppendLine("Repere absolu: " + frame);
sb.AppendLine("Origine: " + origin);
sb.AppendLine("Axe X: " + xAxis + " | Axe Y: " + yAxis + " | Axe Z: " + zAxis);

return sb.ToString();
```

---

## R-043 : Detecter si document est assemblage + lister inclusions (S-080, S-081, S-082)
Pattern: READ
Piege: GetParts retourne les pieces ET assemblages du dossier Parts.
Piege: GetInclusionCodeAndDrivers ne fonctionne que sur les inclusions de famille.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

bool isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId);
if (!isAsm) return "Ce document n'est pas un assemblage.";

var sb = new System.Text.StringBuilder();
sb.AppendLine("Type: Assemblage");

var operations = TopSolidHost.Operations.GetOperations(docId);
int inclusionCount = 0;
foreach (ElementId ope in operations)
{
    if (!TopSolidDesignHost.Assemblies.IsInclusion(ope)) continue;
    inclusionCount++;
    string name = TopSolidHost.Elements.GetFriendlyName(ope);
    DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(ope);
    string defName = defDoc.IsEmpty ? "?" : TopSolidHost.Documents.GetName(defDoc);
    sb.AppendLine("  Inclusion: " + name + " → " + defName);

    try
    {
        string outCode;
        List<string> outDriverNames;
        List<SmartObject> outDriverValues;
        TopSolidDesignHost.Assemblies.GetInclusionCodeAndDrivers(ope, out outCode, out outDriverNames, out outDriverValues);
        if (!string.IsNullOrEmpty(outCode))
            sb.AppendLine("    Code: " + outCode);
        for (int i = 0; i < outDriverNames.Count; i++)
            sb.AppendLine("    Pilote: " + outDriverNames[i] + " = " + outDriverValues[i]);
    }
    catch { }
}
sb.AppendLine("Total inclusions: " + inclusionCount);
return sb.ToString();
```

---

## R-044 : Inclusion parametree avec pilotes (S-084)
Pattern: WRITE
Piege: Les pilotes SmartObject doivent correspondre exactement aux noms declares dans la famille.
Piege: SmartReal utilise UnitType (Length pour des metres, Angle pour des radians).
Piege: Le docId de la famille doit etre obtenu via GetDocument(pdmId).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

// Famille a inclure
string familyName = "{NOM_FAMILLE}";
var projectId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, familyName);
if (results.Count == 0) return "Famille '" + familyName + "' non trouvee.";
DocumentId familyDoc = TopSolidHost.Documents.GetDocument(results[0]);
if (familyDoc.IsEmpty) return "Document famille invalide.";

try
{
    TopSolidHost.Application.StartModification("Parametric Inclusion", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Definir les pilotes (adapter selon la famille)
    List<string> driverNames = new List<string> { "{PILOTE_1}", "{PILOTE_2}" };
    List<SmartObject> driverValues = new List<SmartObject>
    {
        new SmartReal(UnitType.Length, {VALEUR_1_M}),
        new SmartReal(UnitType.Length, {VALEUR_2_M})
    };

    ElementId inclusion = TopSolidDesignHost.Assemblies.CreateInclusion(
        docId, ElementId.Empty, "{NOM_OCCURRENCE}",
        familyDoc, null,
        driverNames, driverValues,
        true, ElementId.Empty, ElementId.Empty,
        false, false, false, false,
        Transform3D.Identity, false);

    if (inclusion.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "ERREUR: Inclusion vide.";
    }

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Inclusion parametree creee.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-045 : Contrainte frame-on-frame (S-085)
Pattern: WRITE
Piege: inPositioningId doit etre le positionnement de l'inclusion (pas l'inclusion elle-meme).
Piege: SmartFrame3D avec SmartFrame3DType.Element prend le repere comme reference.
Piege: Offsets et angles en metres/radians SI.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

try
{
    TopSolidHost.Application.StartModification("Frame Constraint", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Trouver le positionnement (derniere operation de type positioning)
    var operations = TopSolidHost.Operations.GetOperations(docId);
    ElementId positioningId = ElementId.Empty;
    foreach (ElementId ope in operations)
    {
        string typeName = TopSolidHost.Elements.GetTypeFullName(ope);
        if (typeName.Contains("Positioning"))
            positioningId = ope;
    }
    if (positioningId.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Aucun positionnement trouve.";
    }

    // Reperes source et destination (reperes absolus ici)
    ElementId srcFrame = TopSolidHost.Geometries3D.GetAbsoluteFrame(docId);
    Frame3D srcGeom = TopSolidHost.Geometries3D.GetFrameGeometry(srcFrame);
    SmartFrame3D src = new SmartFrame3D(SmartFrame3DType.Element, srcGeom, 0, 0, 0, 0, 0, 0, srcFrame, ItemLabel.Empty, false);

    ElementId dstFrame = TopSolidHost.Geometries3D.GetAbsoluteFrame(docId);
    Frame3D dstGeom = TopSolidHost.Geometries3D.GetFrameGeometry(dstFrame);
    SmartFrame3D dst = new SmartFrame3D(SmartFrame3DType.Element, dstGeom, 0, 0, 0, 0, 0, 0, dstFrame, ItemLabel.Empty, false);

    SmartReal offset = new SmartReal(UnitType.Length, 0);
    SmartReal angleX = new SmartReal(UnitType.Angle, 0);
    SmartReal angleY = new SmartReal(UnitType.Angle, 0);
    SmartReal angleZ = new SmartReal(UnitType.Angle, 0);

    TopSolidDesignHost.Assemblies.CreateFrameOnFrameConstraint(
        positioningId, src, dst,
        offset, false,
        angleX, false,
        angleY, false,
        angleZ, false,
        false);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Contrainte frame-on-frame creee.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-046 : Modifier code et pilotes d'une inclusion (S-086)
Pattern: WRITE
Piege: CHERCHER l'inclusion APRES EnsureIsDirty.
Piege: SetInclusionCodeAndDrivers remplace tous les pilotes d'un coup.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

string inclusionName = "{NOM_INCLUSION}";
string newCode = "{NOUVEAU_CODE}";

try
{
    TopSolidHost.Application.StartModification("Modify Drivers", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Trouver l'inclusion par nom
    var operations = TopSolidHost.Operations.GetOperations(docId);
    ElementId target = ElementId.Empty;
    foreach (ElementId ope in operations)
    {
        if (!TopSolidDesignHost.Assemblies.IsInclusion(ope)) continue;
        if (TopSolidHost.Elements.GetFriendlyName(ope).Contains(inclusionName))
        { target = ope; break; }
    }
    if (target.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Inclusion '" + inclusionName + "' non trouvee.";
    }

    // Lire pilotes actuels
    string outCode;
    List<string> outDriverNames;
    List<SmartObject> outDriverValues;
    TopSolidDesignHost.Assemblies.GetInclusionCodeAndDrivers(target, out outCode, out outDriverNames, out outDriverValues);

    // Modifier les pilotes (adapter selon le besoin)
    // Exemple : changer le premier pilote de type reel
    List<SmartObject> newValues = new List<SmartObject>(outDriverValues);
    // newValues[0] = new SmartReal(UnitType.Length, {NOUVELLE_VALEUR_M});

    TopSolidDesignHost.Assemblies.SetInclusionCodeAndDrivers(target, newCode, outDriverNames, newValues);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Pilotes modifies pour '" + inclusionName + "'.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-047 : Repere par point et deux directions (S-074)
Pattern: WRITE
Piege: inFirstDirection et inSecondDirection sont des SmartDirection3D.
Piege: isSecondDirectionOY = true si la seconde direction definit l'axe Y (sinon c'est Z).

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

try
{
    TopSolidHost.Application.StartModification("Create Frame", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Point d'origine
    ElementId originPoint = TopSolidHost.Geometries3D.GetAbsoluteOriginPoint(docId);
    Point3D originGeom = TopSolidHost.Geometries3D.GetPointGeometry(originPoint);
    SmartPoint3D smartOrigin = new SmartPoint3D(SmartPoint3DType.Element, originGeom, originPoint, ItemLabel.Empty);

    // Directions
    SmartDirection3D dirX = new SmartDirection3D(SmartDirection3DType.Absolute, new Direction3D(1, 0, 0), ElementId.Empty, ItemLabel.Empty, false);
    SmartDirection3D dirY = new SmartDirection3D(SmartDirection3DType.Absolute, new Direction3D(0, 1, 0), ElementId.Empty, ItemLabel.Empty, false);

    ElementId frame = TopSolidHost.Geometries3D.CreateFrameByPointAndTwoDirections(docId, smartOrigin, dirX, dirY, true);
    TopSolidHost.Elements.SetName(frame, "{NOM_REPERE}");

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Repere cree.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-048 : Repere avec offset (S-075)
Pattern: WRITE
Piege: inFrame est le repere source (SmartFrame3D).
Piege: inOffsetDistance en metres SI.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

double offsetM = {OFFSET_M}; // ex: 0.05 pour 50mm

try
{
    TopSolidHost.Application.StartModification("Create Offset Frame", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Repere source = repere absolu
    ElementId srcFrame = TopSolidHost.Geometries3D.GetAbsoluteFrame(docId);
    Frame3D srcGeom = TopSolidHost.Geometries3D.GetFrameGeometry(srcFrame);
    SmartFrame3D smartFrame = new SmartFrame3D(SmartFrame3DType.Element, srcGeom, 0, 0, 0, 0, 0, 0, srcFrame, ItemLabel.Empty, false);

    // Direction d'offset (Z par defaut)
    SmartDirection3D offsetDir = new SmartDirection3D(SmartDirection3DType.Absolute, new Direction3D(0, 0, 1), ElementId.Empty, ItemLabel.Empty, false);
    SmartReal offsetDist = new SmartReal(UnitType.Length, offsetM);

    ElementId newFrame = TopSolidHost.Geometries3D.CreateFrameWithOffset(docId, smartFrame, offsetDir, offsetDist);
    TopSolidHost.Elements.SetName(newFrame, "{NOM_REPERE}");

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Repere offset de " + (offsetM * 1000) + "mm cree.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-049 : Lire contraintes et conditions famille (S-107)
Pattern: READ
Piege: GetDriverCondition retourne null si pas de condition.
Piege: GetConstrainedEntityCount concerne les contraintes pilotees.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

bool isFamily = TopSolidHost.Families.IsFamily(docId);
if (!isFamily) return "Ce document n'est pas une famille.";

var sb = new System.Text.StringBuilder();
var codes = TopSolidHost.Families.GetCodes(docId);
sb.AppendLine("Famille: " + TopSolidHost.Documents.GetName(docId));
sb.AppendLine("Codes: " + codes.Count);

// Lire les parametres et leurs conditions
var parameters = TopSolidHost.Parameters.GetParameters(docId);
sb.AppendLine("Parametres (" + parameters.Count + "):");
foreach (ElementId param in parameters)
{
    string name = TopSolidHost.Elements.GetFriendlyName(param);
    ParameterType pType = TopSolidHost.Parameters.GetParameterType(param);
    sb.Append("  " + name + " (" + pType + ")");

    try
    {
        SmartBoolean condition = TopSolidHost.Families.GetDriverCondition(param);
        if (condition != null)
            sb.Append(" [Condition active]");
    }
    catch { }

    try
    {
        int constrainedCount = TopSolidHost.Families.GetConstrainedEntityCount(param);
        if (constrainedCount > 0)
            sb.Append(" [" + constrainedCount + " contrainte(s)]");
    }
    catch { }

    sb.AppendLine();
}

return sb.ToString();
```

---

## R-050b : Gerer colonnes catalogue famille (S-108)
Pattern: WRITE
Piege: AddCatalogColumn prend un ElementId de parametre, pas un nom.
Piege: RemoveCatalogColumn prend un index (0-based).
Piege: CHERCHER les parametres APRES EnsureIsDirty.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

bool isFamily = TopSolidHost.Families.IsFamily(docId);
if (!isFamily) return "Ce document n'est pas une famille.";

string paramName = "{NOM_PARAMETRE}";

try
{
    TopSolidHost.Application.StartModification("Catalog Column", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Lire colonnes existantes
    var existingCols = TopSolidHost.Families.GetCatalogColumnParameters(docId);

    // Trouver le parametre a ajouter
    var parameters = TopSolidHost.Parameters.GetParameters(docId);
    ElementId targetParam = ElementId.Empty;
    foreach (ElementId param in parameters)
    {
        if (TopSolidHost.Elements.GetFriendlyName(param) == paramName)
        { targetParam = param; break; }
    }
    if (targetParam.IsEmpty)
    {
        TopSolidHost.Application.EndModification(false, false);
        return "Parametre '" + paramName + "' non trouve.";
    }

    // Verifier si deja dans le catalogue
    bool alreadyExists = false;
    foreach (ElementId col in existingCols)
    {
        if (col.Id == targetParam.Id) { alreadyExists = true; break; }
    }

    if (!alreadyExists)
    {
        TopSolidHost.Families.AddCatalogColumn(docId, targetParam);
    }

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);

    string status = alreadyExists ? "deja present" : "ajoute";
    return "OK: Parametre '" + paramName + "' " + status + " dans le catalogue. Colonnes: " + (existingCols.Count + (alreadyExists ? 0 : 1));
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-051b : Gerer collisions assemblage (S-089)
Pattern: WRITE
Piege: SetCollisionsManagement prend un ElementId de representation.
Piege: Si aucune representation, utiliser ElementId.Empty pour la representation par defaut.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

bool isAsm = TopSolidDesignHost.Assemblies.IsAssembly(docId);
if (!isAsm) return "Ce document n'est pas un assemblage.";

try
{
    TopSolidHost.Application.StartModification("Collisions", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Activer la gestion des collisions
    // inRepresentationId: ElementId.Empty pour la representation active
    // inFindsIntersections: true pour detecter les intersections
    // inExcludesThreadingTapping: true pour exclure filetages/taraudages
    // inIsRefreshAuto: true pour rafraichir automatiquement
    TopSolidDesignHost.Assemblies.SetCollisionsManagement(
        docId, ElementId.Empty,
        true,  // findsIntersections
        true,  // excludesThreadingTapping
        true); // isRefreshAuto

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Gestion des collisions activee.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

# ============================================================
# TIER 3 — Drafting, BOM, Materiaux, Multi-couches, Batch, etc.
# ============================================================

## R-050 : Lire un tableau de cotation (Drafting) (S-120)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var tables = TopSolidDraftingHost.Tables.GetDraftTables(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Tableaux (" + tables.Count + "):");

foreach (var table in tables)
{
    string tName = TopSolidHost.Elements.GetFriendlyName(table);
    int rows = TopSolidDraftingHost.Tables.GetDraftTableRowCount(table);
    int cols = TopSolidDraftingHost.Tables.GetDraftTableColumnCount(table);
    sb.AppendLine("\n" + tName + " (" + rows + "x" + cols + "):");

    for (int r = 0; r < rows; r++)
    {
        var line = new List<string>();
        for (int c = 0; c < cols; c++)
        {
            try
            {
                string val = TopSolidDraftingHost.Tables.GetDraftTableCellText(table, r, c);
                line.Add(val);
            }
            catch { line.Add("?"); }
        }
        sb.AppendLine("  " + string.Join(" | ", line));
    }
}
return sb.ToString();
```

---

## R-051 : Parcourir une nomenclature BOM (S-130, S-131, S-132)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (!TopSolidDesignHost.Boms.IsBom(docId))
    return "Le document actif n'est pas une nomenclature.";

var sb = new System.Text.StringBuilder();
int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);
sb.AppendLine("Nomenclature — " + colCount + " colonnes:");

// En-tetes
for (int c = 0; c < colCount; c++)
{
    string colDef = TopSolidDesignHost.Boms.GetColumnPropertyDefinition(docId, c).ToString();
    sb.Append(colDef + " | ");
}
sb.AppendLine();

// Parcourir les lignes recursivement
int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);
var childRows = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);
foreach (int row in childRows)
{
    List<string> props;
    List<string> texts;
    TopSolidDesignHost.Boms.GetRowContents(docId, row, out props, out texts);
    sb.AppendLine(string.Join(" | ", texts));
}
return sb.ToString();
```

---

## R-052 : Affecter materiau a une piece (S-140)
Pattern: WRITE
Piege: Le materiau est un DocumentId, pas un nom.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string materialName = "{NOM_MATERIAU}";

// Trouver le materiau dans les projets references
var projectId = TopSolidHost.Pdm.GetCurrentProject();
var results = TopSolidHost.Pdm.SearchDocumentByName(projectId, materialName);
if (results.Count == 0)
{
    // Chercher dans les projets de bibliotheque
    List<PdmObjectId> workingProjects;
    List<PdmObjectId> libraryProjects;
    TopSolidHost.Pdm.GetProjects(false, true); // libraries only
    // Fallback: retourner erreur
    return "Materiau '" + materialName + "' non trouve.";
}
DocumentId materialDoc = TopSolidHost.Documents.GetDocument(results[0]);

try
{
    TopSolidHost.Application.StartModification("Set material", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    TopSolidDesignHost.Parts.SetMaterial(docId, materialDoc);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Materiau '" + materialName + "' affecte.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-053 : Batch — Modifier un parametre sur tous les documents d'un dossier (S-160, S-163)
Pattern: READ+WRITE
Piege: StartModification GLOBAL, puis boucle sur les docs.

```csharp
var projectId = TopSolidHost.Pdm.GetCurrentProject();
string folderName = "{NOM_DOSSIER}";
string paramName = "{NOM_PARAMETRE}";
double newValue = {VALEUR_SI};

// Trouver le dossier
List<PdmObjectId> folders;
List<PdmObjectId> rootDocs;
TopSolidHost.Pdm.GetConstituents(projectId, out folders, out rootDocs);

PdmObjectId targetFolder = PdmObjectId.Empty;
foreach (var f in folders)
{
    if (TopSolidHost.Pdm.GetName(f).Contains(folderName))
    { targetFolder = f; break; }
}
if (targetFolder.IsEmpty) return "Dossier '" + folderName + "' non trouve.";

List<PdmObjectId> subFolders;
List<PdmObjectId> docs;
TopSolidHost.Pdm.GetConstituents(targetFolder, out subFolders, out docs);

int modified = 0;
try
{
    TopSolidHost.Application.StartModification("Batch modify " + paramName, false);

    foreach (var pdmDoc in docs)
    {
        DocumentId docId = TopSolidHost.Documents.GetDocument(pdmDoc);
        TopSolidHost.Documents.EnsureIsDirty(ref docId);

        var parameters = TopSolidHost.Parameters.GetParameters(docId);
        foreach (var p in parameters)
        {
            if (TopSolidHost.Elements.GetFriendlyName(p).Contains(paramName))
            {
                TopSolidHost.Parameters.SetRealValue(p, newValue);
                modified++;
                break;
            }
        }
    }

    TopSolidHost.Application.EndModification(true, true);

    // Sauvegarder tous les documents modifies
    var pdmIds = new List<PdmObjectId>(docs);
    TopSolidHost.Pdm.SaveSeveral(pdmIds, true);
    return "OK: " + modified + " documents modifies.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-054 : Creer document dans un projet (S-014)
Pattern: WRITE
Piege: L'extension determine le type (.TopPrt, .TopAsm, .TopDft, .TopMat, .TopFam).

```csharp
var projectId = TopSolidHost.Pdm.GetCurrentProject();
string docName = "{NOM_DOCUMENT}";
string extension = "{EXTENSION}"; // ".TopPrt", ".TopAsm", ".TopDft", etc.

PdmObjectId newPdm = TopSolidHost.Pdm.CreateDocument(projectId, extension, true);
TopSolidHost.Pdm.SetName(newPdm, docName);
TopSolidHost.Pdm.Save(newPdm, true);

return "OK: Document '" + docName + "' (" + extension + ") cree dans le projet.";
```

---

## R-055 : Creer un dossier dans un projet (S-015)
Pattern: WRITE

```csharp
var projectId = TopSolidHost.Pdm.GetCurrentProject();
string folderName = "{NOM_DOSSIER}";

PdmObjectId newFolder = TopSolidHost.Pdm.CreateFolder(projectId, folderName);
return "OK: Dossier '" + folderName + "' cree.";
```

---

## R-056 : Export parametres en CSV (S-193)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var parameters = TopSolidHost.Parameters.GetParameters(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Nom;Type;Valeur");

foreach (var p in parameters)
{
    string name = TopSolidHost.Elements.GetFriendlyName(p);
    var pType = TopSolidHost.Parameters.GetParameterType(p);
    string val = "";
    if (pType == ParameterType.Real) val = TopSolidHost.Parameters.GetRealValue(p).ToString();
    else if (pType == ParameterType.Integer) val = TopSolidHost.Parameters.GetIntegerValue(p).ToString();
    else if (pType == ParameterType.Boolean) val = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
    else if (pType == ParameterType.Text) val = TopSolidHost.Parameters.GetTextValue(p);
    else val = pType.ToString();
    sb.AppendLine(name + ";" + pType + ";" + val);
}
return sb.ToString();
```

---

## R-057 : Lire les shapes du document (S-070)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var shapes = TopSolidHost.Shapes.GetShapes(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Shapes (" + shapes.Count + "):");

foreach (var s in shapes)
{
    string name = TopSolidHost.Elements.GetFriendlyName(s);
    string typeName = TopSolidHost.Elements.GetTypeFullName(s);
    sb.AppendLine("  " + name + " (" + typeName + ")");
}
return sb.ToString();
```

---

## R-058 : Lire les vues d'une mise en plan (S-122)
Pattern: READ

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Vues (" + views.Count + "):");

foreach (var v in views)
{
    string name = TopSolidHost.Elements.GetFriendlyName(v);
    sb.AppendLine("  " + name);
}
return sb.ToString();
```

---

## R-059 : Modifier tolerances de modelisation (S-162)
Pattern: WRITE

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
double linearTol = {TOLERANCE_LINEAIRE_M}; // ex: 0.0001 pour 0.1mm
double angularTol = {TOLERANCE_ANGULAIRE_RAD}; // ex: 0.001 rad

try
{
    TopSolidHost.Application.StartModification("Set tolerances", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    TopSolidHost.Options.SetModelingTolerances(docId, linearTol, angularTol);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Tolerances modifiees.";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## CONVERSIONS UNITES

| Grandeur | TopSolid SI | Utilisateur | Conversion |
|----------|-----------|-------------|-----------|
| Longueur | metres (m) | millimetres (mm) | x1000 pour afficher, /1000 pour saisir |
| Angle | radians (rad) | degres (deg) | x(180/PI) pour afficher, x(PI/180) pour saisir |
| Masse | kilogrammes (kg) | kg | identique |
| Volume | m3 | cm3 | x1e6 pour afficher |
| Surface | m2 | cm2 | x1e4 pour afficher |
| Force | Newtons (N) | N | identique |

# ============================================================
# DEMO — Scenarios document TopSolid "Cas d'usages Serveur MCP"
# ============================================================

## R-060 : Diagnostic esquisse — profils ouverts (Scenario 3)
Pattern: READ
Piege: IsProfileClosed retourne false si le profil n'est pas ferme.
Piege: GetProfiles peut retourner 0 profils si l'esquisse est vide.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "FAIL: Aucun document ouvert.";

var sketches = TopSolidHost.Sketches2D.GetSketches(docId);
if (sketches.Count == 0) return "Aucune esquisse dans le document.";

var sb = new System.Text.StringBuilder();
sb.AppendLine("Diagnostic esquisses (" + sketches.Count + "):");
int totalProblems = 0;

foreach (ElementId sketch in sketches)
{
    string name = TopSolidHost.Elements.GetFriendlyName(sketch);
    var profiles = TopSolidHost.Sketches2D.GetProfiles(sketch);

    if (profiles.Count == 0)
    {
        sb.AppendLine("  " + name + ": VIDE (aucun profil)");
        totalProblems++;
        continue;
    }

    foreach (var profile in profiles)
    {
        bool closed = TopSolidHost.Sketches2D.IsProfileClosed(profile);
        var segments = TopSolidHost.Sketches2D.GetProfileSegments(profile);
        if (!closed)
        {
            sb.AppendLine("  " + name + ": PROFIL OUVERT (" + segments.Count + " segments) — ne peut pas etre extrude");
            totalProblems++;
        }
        else
        {
            sb.AppendLine("  " + name + ": OK (" + segments.Count + " segments, ferme)");
        }
    }
}

sb.AppendLine("Problemes detectes: " + totalProblems);
return sb.ToString();
```

---

## R-061 : Tracabilite PDM — revisions et historique (Scenario 4)
Pattern: READ
Piege: GetRevisionTexts retourne major + minor en out params.
Piege: GetFinalMinorRevision retourne la derniere revision disponible dans le PDM.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "FAIL: Aucun document ouvert.";

PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
var sb = new System.Text.StringBuilder();

string docName = TopSolidHost.Pdm.GetName(pdmId);
sb.AppendLine("Document: " + docName);

string ext;
TopSolidHost.Pdm.GetType(pdmId, out ext);
sb.AppendLine("Type: " + ext);

// Revision actuelle
string majorText;
string minorText;
TopSolidHost.Pdm.GetRevisionTexts(pdmId, out majorText, out minorText);
sb.AppendLine("Revision courante: " + majorText + "." + minorText);

// Derniere revision disponible
try
{
    PdmMinorRevisionId finalRev = TopSolidHost.Pdm.GetFinalMinorRevision(pdmId);
    sb.AppendLine("Derniere revision PDM: " + finalRev);
    PdmMinorRevisionId currentRev = TopSolidHost.Documents.GetPdmMinorRevision(docId);
    if (finalRev.Id != currentRev.Id)
        sb.AppendLine("ATTENTION: Le document n'est PAS a la derniere revision !");
    else
        sb.AppendLine("Le document est a jour.");
}
catch { sb.AppendLine("Info revision detaillee non disponible."); }

// Description
try
{
    string desc = TopSolidHost.Pdm.GetDescription(pdmId);
    sb.AppendLine("Description: " + (string.IsNullOrEmpty(desc) ? "(vide)" : desc));
}
catch { }

return sb.ToString();
```

---

## R-062 : Cas d'emploi — ou est utilise ce document (Scenario 6)
Pattern: READ
Piege: SearchObjectsWithProperties cherche dans une liste de projets.
Piege: Pour trouver les assemblages qui utilisent une piece, il faut parcourir chaque assemblage et lister ses inclusions.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "FAIL: Aucun document ouvert.";

PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string targetName = TopSolidHost.Pdm.GetName(pdmId);
PdmObjectId projectId = TopSolidHost.Pdm.GetCurrentProject();

var sb = new System.Text.StringBuilder();
sb.AppendLine("Recherche des cas d'emploi de: " + targetName);

// Lister tous les assemblages du projet
var asmDocs = TopSolidHost.Pdm.SearchObjectsWithProperties(
    new List<PdmObjectId> { projectId }, true,
    new List<string> { ".TopAsm" }, true, -1);

int usageCount = 0;
foreach (PdmObjectId asmPdm in asmDocs)
{
    DocumentId asmDoc = TopSolidHost.Documents.GetDocument(asmPdm);
    if (asmDoc.IsEmpty) continue;
    if (!TopSolidDesignHost.Assemblies.IsAssembly(asmDoc)) continue;

    var ops = TopSolidHost.Operations.GetOperations(asmDoc);
    foreach (ElementId ope in ops)
    {
        if (!TopSolidDesignHost.Assemblies.IsInclusion(ope)) continue;
        try
        {
            DocumentId defDoc = TopSolidDesignHost.Assemblies.GetInclusionDefinitionDocument(ope);
            if (defDoc.IsEmpty) continue;
            PdmObjectId defPdm = TopSolidHost.Documents.GetPdmObject(defDoc);
            if (defPdm.Id == pdmId.Id)
            {
                string asmName = TopSolidHost.Pdm.GetName(asmPdm);
                sb.AppendLine("  Utilise dans: " + asmName);
                usageCount++;
                break;
            }
        }
        catch { }
    }
}

sb.AppendLine("Total: " + usageCount + " assemblage(s)");
if (usageCount > 0)
    sb.AppendLine("ATTENTION: toute modification impactera ces assemblages.");
return sb.ToString();
```

---

## R-063 : Standardisation matieres — changer le materiau (Scenario 15)
Pattern: WRITE
Piege: SetMaterial prend un DocumentId du materiau, pas un nom.
Piege: IParts.GetMaterial retourne DocumentId.Empty si aucun materiau.
Piege: CHERCHER les pieces APRES EnsureIsDirty si on modifie le doc courant.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "FAIL: Aucun document ouvert.";

// Verifier que c'est un assemblage
if (!TopSolidDesignHost.Assemblies.IsAssembly(docId))
{
    // Document simple — lire le materiau
    try
    {
        DocumentId matDoc = TopSolidDesignHost.Parts.GetMaterial(docId);
        string matName = matDoc.IsEmpty ? "(aucun)" : TopSolidHost.Documents.GetName(matDoc);
        return "Materiau actuel: " + matName + ". Pour changer: fournir le DocumentId du nouveau materiau.";
    }
    catch (Exception ex)
    {
        return "Lecture materiau impossible: " + ex.Message;
    }
}

// Assemblage — lister les materiaux de chaque piece
var parts = TopSolidDesignHost.Assemblies.GetParts(docId);
var sb = new System.Text.StringBuilder();
sb.AppendLine("Materiaux des pieces (" + parts.Count + "):");

int withMat = 0;
int withoutMat = 0;
foreach (ElementId part in parts)
{
    try
    {
        DocumentId partDoc = TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(part);
        if (partDoc.IsEmpty) continue;
        string partName = TopSolidHost.Documents.GetName(partDoc);
        DocumentId matDoc = TopSolidDesignHost.Parts.GetMaterial(partDoc);
        if (matDoc.IsEmpty)
        {
            sb.AppendLine("  " + partName + ": AUCUN MATERIAU");
            withoutMat++;
        }
        else
        {
            string matName = TopSolidHost.Documents.GetName(matDoc);
            sb.AppendLine("  " + partName + ": " + matName);
            withMat++;
        }
    }
    catch { }
}

sb.AppendLine("Avec materiau: " + withMat + " | Sans materiau: " + withoutMat);
if (withoutMat > 0)
    sb.AppendLine("ATTENTION: " + withoutMat + " piece(s) sans materiau — masses approximatives !");
return sb.ToString();
```

---

## R-064 : Back-references PDM — cas d'emploi natif (S-006 improved)
Pattern: READ | Remplace: R-062 (qui utilisait GetMajorRevisionChildren)
Méthode clé: `IPdm.SearchMajorRevisionBackReferences`

```csharp
// Trouver tous les documents qui référencent un document donné (where-used natif)
DocumentId docId = TopSolidHost.Documents.EditedDocument;
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
PdmObjectId majorRevId = TopSolidHost.Pdm.GetMajorRevision(pdmId);

// Chercher dans le projet courant
PdmObjectId projectId = TopSolidHost.Pdm.GetCurrentProject();
var backRefs = TopSolidHost.Pdm.SearchMajorRevisionBackReferences(projectId, majorRevId);

var sb = new System.Text.StringBuilder();
sb.AppendLine($"Cas d'emploi ({backRefs.Count} references) :");
for (int i = 0; i < backRefs.Count; i++)
{
    string name = TopSolidHost.Pdm.GetName(backRefs[i]);
    sb.AppendLine($"  - {name}");
}
return sb.ToString();
```

---

## R-065 : Stock/Brut — gestion manuelle du brut (Scenario 5 TopSolid)
Pattern: WRITE | Méthode clé: `ITools.SetStockManagement`

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
try
{
    TopSolidHost.Application.StartModification("Set stock management", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Activer la gestion manuelle du brut (pas de document source, pas d'élément)
    TopSolidDesignHost.Tools.SetStockManagement(
        docId,
        StockManagementType.Manual,  // Manuel
        DocumentId.Empty,            // Pas de document brut
        ElementId.Empty,             // Pas d'élément brut
        null, null, null, null       // Noms optionnels (null = auto)
    );

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Stock management set to Manual";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-066 : Filtrer shapes par type — Solid vs Sheet (Scenario 14 TopSolid)
Pattern: READ | Méthode clé: `IShapes.GetShapeType`

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var shapes = TopSolidHost.Shapes.GetShapes(docId);

var sb = new System.Text.StringBuilder();
int solidCount = 0, sheetCount = 0;
for (int i = 0; i < shapes.Count; i++)
{
    var shapeType = TopSolidHost.Shapes.GetShapeType(shapes[i]);
    string name = TopSolidHost.Elements.GetFriendlyName(shapes[i]);
    sb.AppendLine($"  {name} : {shapeType}");
    if (shapeType.ToString().Contains("Solid")) solidCount++;
    else if (shapeType.ToString().Contains("Sheet")) sheetCount++;
}
sb.Insert(0, $"Shapes: {solidCount} solids, {sheetCount} sheets\n");
return sb.ToString();
```

---

## R-067 : Smart parameters — paramètre avec formule (Scenario 19 TopSolid)
Pattern: WRITE | Méthode clé: `IParameters.CreateSmartRealParameter`

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
try
{
    TopSolidHost.Application.StartModification("Create smart parameter", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // Créer un paramètre réel piloté par formule
    // Note: la formule référence d'autres paramètres par leur nom
    TopSolidHost.Parameters.CreateSmartRealParameter(
        docId,
        "{NOM_PARAMETRE}",          // Nom du paramètre
        "{FORMULE_OU_VALEUR}",      // Ex: "Longueur * 2" ou "0.1"
        UnitId.Empty                 // Unité (Empty = sans unité)
    );

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Smart parameter created";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-068 : Metadata fichier PDM — taille et version (Scenario 12 TopSolid)
Pattern: READ | Méthodes: `IPdm.GetMinorRevisionFileSize`, `IPdm.GetMinorRevisionFileVersion`

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
PdmObjectId majorRevId = TopSolidHost.Pdm.GetMajorRevision(pdmId);
PdmObjectId minorRevId = TopSolidHost.Pdm.GetMinorRevision(majorRevId);

long fileSize = TopSolidHost.Pdm.GetMinorRevisionFileSize(minorRevId);
int fileVersion = TopSolidHost.Pdm.GetMinorRevisionFileVersion(minorRevId);
string name = TopSolidHost.Pdm.GetName(pdmId);

return $"Document: {name}\nTaille: {fileSize / 1024} KB\nVersion fichier: {fileVersion}";
```

---

## R-069 : Redirect inclusion — changer la définition (Scenario 18 TopSolid)
Pattern: WRITE | Méthode clé: `IAssemblies.RedirectInclusion`

```csharp
// Rediriger une inclusion vers un autre document (ex: remplacer une pièce)
DocumentId docId = TopSolidHost.Documents.EditedDocument;
try
{
    TopSolidHost.Application.StartModification("Redirect inclusion", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);

    // inclusionId = l'ElementId de l'inclusion à rediriger
    // newDefinitionDocId = le DocumentId du nouveau document cible
    ElementId inclusionId = {INCLUSION_ELEMENT_ID};
    DocumentId newDefDocId = {NEW_DEFINITION_DOC_ID};

    TopSolidDesignHost.Assemblies.RedirectInclusion(inclusionId, newDefDocId);

    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return "OK: Inclusion redirected";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-070 : Tolérances de visualisation (Scenario 7 TopSolid variante)
Pattern: READ+WRITE | Méthodes: `IOptions.Get/SetVisualizationTolerances`

```csharp
// Lire les tolérances de visualisation actuelles
double chordError = 0, angularTolerance = 0;
TopSolidHost.Options.GetVisualizationTolerances(out chordError, out angularTolerance);
return $"Tolerances: chordError={chordError}, angularTolerance={angularTolerance} rad";
```

Variante WRITE (modifier les tolérances) :
```csharp
try
{
    TopSolidHost.Application.StartModification("Set visualization tolerances", false);
    TopSolidHost.Options.SetVisualizationTolerances({CHORD_ERROR}, {ANGULAR_TOLERANCE});
    TopSolidHost.Application.EndModification(true, true);
    return "OK: Tolerances updated";
}
catch (Exception ex)
{
    TopSolidHost.Application.EndModification(false, false);
    return "ERREUR: " + ex.Message;
}
```

---

## R-071 : Documents synchronisés (variante multi-documents)
Pattern: READ | Méthode: `IDocuments.GetSynchronizedDocuments`

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
var syncDocs = TopSolidHost.Documents.GetSynchronizedDocuments(docId);

var sb = new System.Text.StringBuilder();
sb.AppendLine($"Documents synchronises avec le document actif ({syncDocs.Count}) :");
for (int i = 0; i < syncDocs.Count; i++)
{
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(syncDocs[i]);
    string name = TopSolidHost.Pdm.GetName(pdm);
    sb.AppendLine($"  - {name}");
}
return sb.ToString();
```

---

## R-072 : Exporter en DXF (mise a plat / mise en plan)
Pattern: READ | Base: R-004 pattern (export generique)
Piege: L'index de l'exporteur change selon l'installation. Toujours le chercher dynamiquement.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

int exporterCount = TopSolidHost.Application.ExporterCount;
int dxfIndex = -1;
for (int i = 0; i < exporterCount; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains("dxf"))
        { dxfIndex = i; break; }
    }
    if (dxfIndex >= 0) break;
}
if (dxfIndex < 0) return "Exporteur DXF non trouve.";

if (!TopSolidHost.Documents.CanExport(dxfIndex, docId))
    return "Ce document ne peut pas etre exporte en DXF.";

string outputPath = @"{CHEMIN_SORTIE}"; // ex: @"C:\temp\output.dxf"
TopSolidHost.Documents.Export(dxfIndex, docId, outputPath);
return "OK: Document exporte en DXF → " + outputPath;
```

---

## R-073 : Exporter en PDF (mise en plan)
Pattern: READ | Base: R-004 pattern (export generique)
Piege: L'index de l'exporteur change selon l'installation. Toujours le chercher dynamiquement.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

int exporterCount = TopSolidHost.Application.ExporterCount;
int pdfIndex = -1;
for (int i = 0; i < exporterCount; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains("pdf"))
        { pdfIndex = i; break; }
    }
    if (pdfIndex >= 0) break;
}
if (pdfIndex < 0) return "Exporteur PDF non trouve.";

if (!TopSolidHost.Documents.CanExport(pdfIndex, docId))
    return "Ce document ne peut pas etre exporte en PDF.";

string outputPath = @"{CHEMIN_SORTIE}"; // ex: @"C:\temp\plan.pdf"
TopSolidHost.Documents.Export(pdfIndex, docId, outputPath);
return "OK: Document exporte en PDF → " + outputPath;
```

---

## R-074 : Exporter en IFC (batiment)
Pattern: READ | Base: R-004 pattern (export generique)
Piege: L'index de l'exporteur change selon l'installation. Toujours le chercher dynamiquement.

```csharp
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

int exporterCount = TopSolidHost.Application.ExporterCount;
int ifcIndex = -1;
for (int i = 0; i < exporterCount; i++)
{
    string typeName;
    string[] extensions;
    TopSolidHost.Application.GetExporterFileType(i, out typeName, out extensions);
    foreach (string ext in extensions)
    {
        if (ext.ToLower().Contains("ifc"))
        { ifcIndex = i; break; }
    }
    if (ifcIndex >= 0) break;
}
if (ifcIndex < 0) return "Exporteur IFC non trouve (necessite licence TopSolid Steel/Wood).";

if (!TopSolidHost.Documents.CanExport(ifcIndex, docId))
    return "Ce document ne peut pas etre exporte en IFC.";

string outputPath = @"{CHEMIN_SORTIE}"; // ex: @"C:\temp\batiment.ifc"
TopSolidHost.Documents.Export(ifcIndex, docId, outputPath);
return "OK: Document exporte en IFC → " + outputPath;
```

---

## R-075 : Lire/ecrire une propriete utilisateur (S-custom)
Pattern: READ or WRITE depending on usage
Piege: La propriete utilisateur doit exister dans le projet. SearchUserPropertyParameter retourne Empty si elle n'existe pas.
Piege: Distinguer IPdm.GetTextUserProperty (niveau PDM, rapide) de IParameters.SearchUserPropertyParameter (niveau parametre, plus puissant).

```csharp
// --- LECTURE via IPdm (simple, niveau PDM) ---
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
string propName = "{NOM_PROPRIETE}"; // ex: "Type de production"

string textValue = TopSolidHost.Pdm.GetTextUserProperty(pdmId, propName);
return "Propriete '" + propName + "' = " + (string.IsNullOrEmpty(textValue) ? "(vide)" : textValue);
```

```csharp
// --- ECRITURE via IPdm (simple, niveau PDM) ---
// ATTENTION: utiliser topsolid_modify_script pour cette partie
DocumentId docId = TopSolidHost.Documents.EditedDocument;
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);

string propName = "{NOM_PROPRIETE}"; // ex: "Type de production"
string newValue = "{NOUVELLE_VALEUR}"; // ex: "Fabrique"

TopSolidHost.Pdm.SetTextUserProperty(pdmId, propName, newValue);
__message = "OK: Propriete '" + propName + "' = " + newValue;
```

```csharp
// --- LECTURE via IParameters (niveau parametre, avec ElementId) ---
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "Aucun document ouvert.";

// Il faut le DocumentId de la definition de la propriete utilisateur
// (c'est un document TopSolid qui definit la propriete)
// SearchUserPropertyParameter cherche si le document a cette propriete
DocumentId propDefDoc = default; // {DOCUMENT_DEFINITION_PROPRIETE}
ElementId paramId = TopSolidHost.Parameters.SearchUserPropertyParameter(docId, propDefDoc);
if (paramId.IsEmpty) return "Propriete utilisateur non trouvee dans ce document.";

int paramType = TopSolidHost.Parameters.GetParameterType(paramId);
string value = "";
if (paramType == 4) // Text
    value = TopSolidHost.Parameters.GetTextValue(paramId);
else if (paramType == 0) // Real
    value = TopSolidHost.Parameters.GetRealValue(paramId).ToString();
else if (paramType == 3) // Boolean
    value = TopSolidHost.Parameters.GetBooleanValue(paramId).ToString();

return "Propriete utilisateur = " + value;
```

---

## TYPES DE PARAMETRES (ParameterType enum)

| Type | Getter | Setter | Creator |
|------|--------|--------|---------|
| Real | GetRealValue(ElementId) | SetRealValue(ElementId, double) | CreateRealParameter(DocId, UnitType, double) |
| Integer | GetIntegerValue(ElementId) | SetIntegerValue(ElementId, int) | CreateIntegerParameter(DocId, int) |
| Boolean | GetBooleanValue(ElementId) | SetBooleanValue(ElementId, bool) | CreateBooleanParameter(DocId, bool) |
| Text | GetTextValue(ElementId) | SetTextValue(ElementId, string) | CreateTextParameter(DocId, string) |
| DateTime | GetDateTimeValue(ElementId) | — | — |
| Enumeration | GetEnumerationValue(ElementId) | SetEnumerationValue(ElementId, int) | — |
| Color | GetColorValue(ElementId) | SetColorValue(ElementId, Color) | CreateColorParameter(DocId, Color) |
