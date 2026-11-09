using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TopSolidApiGraph.Core;
using TopSolidApiGraph.Core.Models;
using TopSolidMcpServer.Protocol;
using TopSolidMcpServer.Protocol.Models;

namespace TopSolidMcpServer.Tools
{
    public class ApiHelpTool
    {
        /// <summary>Maximum size of the returned text, to keep a single call from flooding the caller's context.</summary>
        private const int MaxOutputChars = 8000;

        /// <summary>Methods listed by a keyword search.</summary>
        private const int MaxKeywordResults = 30;

        /// <summary>Usage tips appended to a keyword search.</summary>
        private const int MaxUsageTips = 3;

        private readonly Func<TypeGraph> _graphProvider;

        private static readonly Dictionary<string, string[]> Synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // --- PDM properties (TopSolid columns) ---
            { "designation", new[] { "Description" } },
            { "reference", new[] { "Part", "Number" } },
            { "fabricant", new[] { "Manufacturer" } },
            { "fournisseur", new[] { "Manufacturer" } },
            { "auteur", new[] { "Owner" } },
            { "renommer", new[] { "Set", "Name" } },
            { "rename", new[] { "Set", "Name" } },
            { "nom", new[] { "Name" } },
            // --- PDM operations (TopSolid terminology) ---
            { "coffre", new[] { "Check" } },
            { "archiver", new[] { "Check", "In" } },
            // --- Interfaces ---
            { "bom", new[] { "IBoms" } },
            { "nomenclature", new[] { "IBoms" } },
            { "collision", new[] { "CollisionsManagement" } },
            { "collisions", new[] { "CollisionsManagement" } },
            { "etape", new[] { "Stage" } },
            { "etapes", new[] { "Stage" } },
            { "stage", new[] { "Stage" } },
            { "stages", new[] { "Stage" } },
            { "matiere", new[] { "IMaterials" } },
            { "materiau", new[] { "IMaterials" } },
            { "material", new[] { "IMaterials" } },
            { "draft", new[] { "IDraftings" } },
            { "drafting", new[] { "IDraftings" } },
            { "peinture", new[] { "ICoatings" } },
            { "traitement", new[] { "ICoatings" } },
            { "coating", new[] { "ICoatings" } },
            { "revetement", new[] { "ICoatings" } },
            { "tolerance", new[] { "Visualization", "Tolerances" } },
            { "simulation", new[] { "ISimulations" } },
            { "texture", new[] { "ITextures" } },
            { "calque", new[] { "ILayers" } },
            { "entite", new[] { "IEntities" } },
            // --- Export formats ---
            { "step", new[] { "Export" } },
            { "iges", new[] { "Export" } },
            // --- Common operations ---
            { "brut", new[] { "Stock" } },
            { "supprimer", new[] { "Delete" } },
            { "effacer", new[] { "Delete" } },
            { "creer", new[] { "Create" } },
            { "lister", new[] { "Get" } },
            // --- Domain geometry (French TopSolid terms) ---
            { "esquisse", new[] { "Sketch" } },
            { "piece", new[] { "Part" } },
            { "assemblage", new[] { "Assembly" } },
            { "famille", new[] { "Family" } },
            { "extrusion", new[] { "Extruded", "Shape" } },
            { "percage", new[] { "Drilling" } },
            { "pliage", new[] { "Bend" } },
            { "plan", new[] { "Plane" } },
            { "repere", new[] { "Frame" } },
            { "contrainte", new[] { "Constraint" } },
            { "inclusion", new[] { "Inclusion" } },
            { "contour", new[] { "Profile" } },
            { "section", new[] { "Profile" } },
            { "conge", new[] { "Fillet" } },
            { "chanfrein", new[] { "Chamfer" } },
            { "gabarit", new[] { "Lofted" } },
            { "lissage", new[] { "Lofted" } },
            { "balayage", new[] { "Sweep" } },
            { "motif", new[] { "Pattern" } },
            { "repetition", new[] { "Pattern" } },
            { "symetrie", new[] { "Mirror" } },
            { "coque", new[] { "Shell" } },
            { "tole", new[] { "Sheet" } },
            { "filetage", new[] { "Thread" } },
            { "cote", new[] { "IDimensions" } },
            { "cotation", new[] { "IAnnotations" } },
            // --- Flat pattern / unfolding ---
            { "depliage", new[] { "Unfolding" } },
            { "mise a plat", new[] { "IUnfoldings" } },
            // --- Drafting / BOM ---
            { "mise en plan", new[] { "IDraftings" } },
            { "liasse", new[] { "IDraftings" } },
            { "rafale", new[] { "IBoms" } },
            { "modele", new[] { "Template" } },
            { "liste de debit", new[] { "IBoms" } },
            { "fiche", new[] { "IBoms" } },
            // --- Export ---
            { "dxf", new[] { "Export" } },
            { "pdf", new[] { "Export" } },
            { "ifc", new[] { "Export" } },
            // --- Localization / Translation / Dictionaries (.xliff / .TopDic) ---
            { "traduction", new[] { "Localizations" } },
            { "traductions", new[] { "Localizations" } },
            { "traduire", new[] { "Localizations" } },
            { "translation", new[] { "Localizations" } },
            { "translations", new[] { "Localizations" } },
            { "translate", new[] { "Localizations" } },
            { "localisation", new[] { "Localizations" } },
            { "localisations", new[] { "Localizations" } },
            { "localization", new[] { "Localizations" } },
            { "localizations", new[] { "Localizations" } },
            { "localize", new[] { "Localizations" } },
            { "xliff", new[] { "Localizations" } },
            { "topdic", new[] { "Localizations" } },
            { "dictionnaire", new[] { "Localizations" } },
            { "dictionnaires", new[] { "Localizations" } },
            { "dictionary", new[] { "Localizations" } },
            { "dictionaries", new[] { "Localizations" } },
            { "langue", new[] { "Localizations" } },
            { "langues", new[] { "Localizations" } },
            // --- PDM organisation (move, delete, browse) ---
            { "pdm", new[] { "IPdm" } },
            { "deplacer", new[] { "Move" } },
            { "deplace", new[] { "Move" } },
            { "deplacement", new[] { "Move" } },
            { "ranger", new[] { "Move" } },
            { "dossier", new[] { "Folder" } },
            { "dossiers", new[] { "Folder" } },
            { "projet", new[] { "Project" } },
            { "suppression", new[] { "Delete" } },
            { "supprime", new[] { "Delete" } },
            { "constituants", new[] { "Constituents" } },
            { "contenu", new[] { "Constituents" } },
            // --- File import / export ---
            { "importer", new[] { "Import" } },
            { "exporter", new[] { "Export" } }
        };

        /// <summary>
        /// Words that carry no search meaning. Without this list, "un" or "dans" matched almost
        /// every method in the substring fallback and pushed the real answer out of the result pool.
        /// </summary>
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "un", "une", "le", "la", "les", "l", "de", "des", "du", "d", "dans", "en", "a", "au", "aux",
            "et", "ou", "pour", "sur", "par", "avec", "vers", "ce", "cet", "cette", "ces", "mon", "ma", "mes",
            "comment", "faire", "the", "an", "in", "into", "to", "of", "for", "on", "and", "or", "with", "how"
        };

        private static string[] SplitCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return new string[0];
            return Regex.Split(name, @"(?=[A-Z])")
                .Where(s => s.Length > 1).ToArray();
        }

        private static readonly Dictionary<string, string> UsageTips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "move", "To move documents or folders into a folder: IPdm.MoveSeveral(objectIds, targetFolderId) with a List<PdmObjectId>. Find the target folder by EXACT name through IPdm.GetConstituents (SearchFolderByName is a CONTAINS match)." },
            { "deplac", "To move documents or folders into a folder: IPdm.MoveSeveral(objectIds, targetFolderId) with a List<PdmObjectId>. Find the target folder by EXACT name through IPdm.GetConstituents (SearchFolderByName is a CONTAINS match)." },
            { "delete", "PDM objects (documents, folders) are deleted with IPdm.DeleteSeveral(objectIds) (List<PdmObjectId>), project folders with IPdm.DeleteProjectFolder. Elements INSIDE an open document use IElements.Delete / IElements.DeleteSeveral. Before deleting, check that nothing you keep still references the objects." },
            { "supprim", "PDM objects (documents, folders) are deleted with IPdm.DeleteSeveral(objectIds) (List<PdmObjectId>), project folders with IPdm.DeleteProjectFolder. Elements INSIDE an open document use IElements.Delete / IElements.DeleteSeveral. Before deleting, check that nothing you keep still references the objects." },
            { "suppression", "PDM objects (documents, folders) are deleted with IPdm.DeleteSeveral(objectIds) (List<PdmObjectId>), project folders with IPdm.DeleteProjectFolder. Elements INSIDE an open document use IElements.Delete / IElements.DeleteSeveral. Before deleting, check that nothing you keep still references the objects." },
            { "isopen", "IDocuments has no IsOpen method: use TopSolidHost.Documents.GetOpenDocuments().Contains(docId). Close what you opened with Documents.Close(docId, false, false)." },
            { "pdmobjectid", "PdmObjectId.ToString() returns the type name, identical for every object: never use it as a dictionary or HashSet key. Compare ids with Equals, or keep the PdmObjectId itself as the key." },
            { "tostring", "PdmObjectId.ToString() returns the type name, identical for every object: never use it as a dictionary or HashSet key. Compare ids with Equals, or keep the PdmObjectId itself as the key." },
            { "material", "The material OF A PART: TopSolidDesignHost.Parts.SetMaterial(partDocId, materialDocId) inside StartModification, Parts.GetMaterial(partDocId) to read it (recipe set_material). IMaterials edits the material document itself (density, appearance)." },
            { "materiau", "The material OF A PART: TopSolidDesignHost.Parts.SetMaterial(partDocId, materialDocId) inside StartModification, Parts.GetMaterial(partDocId) to read it (recipe set_material). IMaterials edits the material document itself (density, appearance)." },
            { "matiere", "The material OF A PART: TopSolidDesignHost.Parts.SetMaterial(partDocId, materialDocId) inside StartModification, Parts.GetMaterial(partDocId) to read it (recipe set_material). IMaterials edits the material document itself (density, appearance)." },
            { "constituent", "IPdm.GetConstituents(containerId, out folderIds, out documentIds) lists the folders and documents of a project or folder. IElements.GetConstituents(elementId) is unrelated: it lists the sub-elements of a composite element inside a document." },
            { "import", "Prefer the 'import_file' recipe. In a script: find the importer with GetImporterFileType() + IsImporterValid(), take GetImporterOptions(ix), set IS_TRANSLATOR_SILENT=True, then IDocuments.ImportWithOptions(ix, options, path, ownerFolderId, out log, out badDocumentIds). Import one file per call; it runs outside StartModification." },
            { "parameter", "To list parameters: GetParameters(docId), then GetParameterType(p) to pick the right GetXxxValue." },
            { "sketch", "To list sketches: GetSketches(docId), or GetFunctions(docId) filtered on the Sketch type." },
            { "export", "Prefer the 'export_file' recipe. In a script: find the index with GetExporterFileType(), check CanExport(), then call Export() or ExportWithOptions(). NEVER export a 3D format from a document without shapes (IShapes.GetShapes(docId).Count == 0, e.g. an imported assembly): TopSolid opens a modal 'nothing to export' dialog that silent mode does not prevent, and the bridge times out." },
            { "assembly", "For assemblies, use TopSolidDesignHost.Assemblies (not TopSolidHost)." },
            { "collision", "Use TopSolidDesignHost.Assemblies.SetCollisionsManagement(asmId, designRep, true, true, true) + SetCollisionsManagementCheckingMechanisms(asmId, false) + SetCollisionsManagementInvalidWhenColliding(asmId, false), then inspect IElements.GetElements(asmId) where GetTypeFullName == 'TopSolid.Cad.Design.DB.Collisions.ShapeShapeCollisionEntity'. IMPORTANT: analyzing collisions switches TopSolid to the Analysis stage; before resuming modeling/inclusions, return to the Modeling stage via TopSolidHost.Operations.SetWorkingStage(TopSolidHost.Operations.GetModelingStage(asmId)); TopSolidHost.Operations.ResetInsertionOperation(asmId);" },
            { "collisions", "Use TopSolidDesignHost.Assemblies.SetCollisionsManagement(asmId, designRep, true, true, true) + SetCollisionsManagementCheckingMechanisms(asmId, false) + SetCollisionsManagementInvalidWhenColliding(asmId, false), then inspect IElements.GetElements(asmId) where GetTypeFullName == 'TopSolid.Cad.Design.DB.Collisions.ShapeShapeCollisionEntity'. IMPORTANT: analyzing collisions switches TopSolid to the Analysis stage; before resuming modeling/inclusions, return to the Modeling stage via TopSolidHost.Operations.SetWorkingStage(TopSolidHost.Operations.GetModelingStage(asmId)); TopSolidHost.Operations.ResetInsertionOperation(asmId);" },
            { "stage", "TopSolid operation trees are organized in Stages (Modeling, Mechanism, Analysis, Annotation/Final). Use TopSolidHost.Operations.GetStages(docId), GetWorkingStage(docId), GetModelingStage(docId), SetWorkingStage(stageId), and ResetInsertionOperation(docId). After running Analysis operations (like SetCollisionsManagement), always switch back to GetModelingStage(docId) before adding new modeling features or inclusions." },
            { "stages", "TopSolid operation trees are organized in Stages (Modeling, Mechanism, Analysis, Annotation/Final). Use TopSolidHost.Operations.GetStages(docId), GetWorkingStage(docId), GetModelingStage(docId), SetWorkingStage(stageId), and ResetInsertionOperation(docId). After running Analysis operations (like SetCollisionsManagement), always switch back to GetModelingStage(docId) before adding new modeling features or inclusions." },
            { "etape", "TopSolid operation trees are organized in Stages (Modeling, Mechanism, Analysis, Annotation/Final). Use TopSolidHost.Operations.GetStages(docId), GetWorkingStage(docId), GetModelingStage(docId), SetWorkingStage(stageId), and ResetInsertionOperation(docId). After running Analysis operations (like SetCollisionsManagement), always switch back to GetModelingStage(docId) before adding new modeling features or inclusions." },
            { "etapes", "TopSolid operation trees are organized in Stages (Modeling, Mechanism, Analysis, Annotation/Final). Use TopSolidHost.Operations.GetStages(docId), GetWorkingStage(docId), GetModelingStage(docId), SetWorkingStage(stageId), and ResetInsertionOperation(docId). After running Analysis operations (like SetCollisionsManagement), always switch back to GetModelingStage(docId) before adding new modeling features or inclusions." },
            { "project", "SearchProjectByName does a CONTAINS match. Always confirm the exact name with GetName() afterwards." },
            { "folder", "GetConstituents() splits folders and documents. SearchFolderByName does a CONTAINS match." },
            { "document", "SearchDocumentByName does a CONTAINS match (not exact). GetType() returns the extension (.TopPrt, .TopAsm)." },
            { "name", "Three GetName methods: Pdm.GetName(PdmObjectId), Documents.GetName(DocumentId), Elements.GetName(ElementId)." },
            { "revision", "CheckIn = put back in the vault. CheckOut = take out of the vault. CheckIn is required before a life-cycle change." },
            { "family", "IsFamily() to test, GetCodes() for the codes, GetGenericDocument() for the generic document." },
            { "inclusion", "Call CreatePositioning() BEFORE CreateInclusion(). Use GetInclusionChildOccurrence to navigate." },
            { "modification", "ALWAYS use topsolid_modify_script instead of topsolid_execute_script for modifications." },
            { "designation", "Designation = IPdm.SetDescription (not SetName). Reference = IPdm.SetPartNumber. Name = IPdm.SetName." },
            { "description", "Description in the API = Designation in TopSolid. Use IPdm.SetDescription to change it." },
            { "drafting", "Drafting: create a .TopDrf, add views (main + auxiliary) through projection sets. Batch drafting generates one drawing per BOM line." },
            { "bom", "A BOM is a technical view of an assembly. It can be filtered (sheet metal, profiles, purchased parts...). Batch drafting generates one drawing per line." },
            { "unfolding", "Unfolding / flat pattern: for sheet metal. Folded part -> unfolded -> DXF export for laser cutting." },
            { "traduction", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "traduire", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "translat", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "localiz", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "localis", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "xliff", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "topdic", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "diction", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." },
            { "langue", "Project translation / localization workflow (prefer recipes 'export_project_xliff' and 'import_project_xliff'): 1) Export: TopSolidHost.Pdm.ExportLocalizations(new List<PdmObjectId> { projectId }, new[] { new CultureInfo(\"fr\") }, outputDir, \".xliff\", false) — passing a project PdmObjectId recursively exports all its child folders, documents, parameters, and properties into a single .xliff file (if the project name contains a dot '.', TopSolid truncates the filename on disk at the last dot before '.<culture>.xliff'). 2) Never use IPdm.ExportDictionaries (throws FaultException: Sequence contains no matching element) and never rename designations/names directly with SetName. 3) Import: TopSolidHost.Pdm.ImportLocalizations(outputDir, \".xliff\", errors) or TopSolidHost.Pdm.ImportDictionnaries(new List<PdmObjectId> { projectId }, new[] { xliffFilePath }, errors). 4) If a project contains invalid family instances (referencing an older major revision of a family), TopSolid opens a modal 'Repair family' dialog during ExportLocalizations; if the family is in Design state instead of Validated/Released, validate/repair the family in TopSolid UI ('Library Design > Export dictionary') first." }
        };

        private static readonly string[] Tier1Interfaces = { "ITopSolidHost", "IDocuments", "IPdm", "IParameters", "IElements", "ISketches2D", "IShapes", "IOperations" };

        private static readonly string[] OrderedCategories = {
            "Navigation / Queries",
            "Value reads",
            "Search",
            "Creation",
            "Value writes",
            "Export",
            "Deletion",
            "Other"
        };

        public ApiHelpTool(Func<TypeGraph> graphProvider)
        {
            _graphProvider = graphProvider;
        }

        public void Register(McpToolRegistry registry)
        {
            registry.RegisterTool(new McpToolDescriptor
            {
                Name = "topsolid_api_help",
                Description = "Search the TopSolid Automation API reference. " +
                    "Use it BEFORE topsolid_execute_script to get the exact signatures. " +
                    "Accepts an interface name (IPdm), a keyword (sketch), or a filter (IDocuments.Export).",
                InputSchema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Interface (e.g. IParameters), keyword (e.g. sketch), or Interface.Prefix (e.g. IDocuments.Get)"
                        }
                    },
                    ["required"] = new JArray { "query" }
                }
            }, Execute);
        }

        public string Execute(JObject arguments)
        {
            return Truncate(ExecuteCore(arguments));
        }

        private string ExecuteCore(JObject arguments)
        {
            string query = arguments["query"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(query))
                return "Error: the 'query' argument is required.";

            var graph = _graphProvider();
            if (graph == null)
                return "Error: API graph not loaded.";

            // --- 0. Interface.Prefix support ---
            if (query.Contains(".") && !query.StartsWith("."))
            {
                var parts = query.Split('.');
                var interfaceName = parts[0].Trim();
                var prefix = parts[1].Trim();

                var filteredEdges = graph.GetEdges()
                    .Where(e => string.Equals(e.Interface, interfaceName, StringComparison.OrdinalIgnoreCase) && 
                                e.MethodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filteredEdges.Count > 0)
                    return FormatInterface(interfaceName, filteredEdges, true);
            }

            // --- 1. Exact interface mode ---
            var interfaceEdges = graph.GetEdges()
                .Where(e => string.Equals(e.Interface, query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (interfaceEdges.Count > 0)
            {
                return FormatInterface(query, interfaceEdges);
            }

            // --- 1b. Exact method-name mode (e.g. "GetConstituents") ---
            // Every interface declaring the method, without the noise of the camel-case split.
            if (query.IndexOf(' ') < 0)
            {
                var methodEdges = graph.GetEdges()
                    .Where(e => string.Equals(e.MethodName, query, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => Tier1Rank(e.Interface))
                    .ThenBy(e => e.Interface)
                    .ToList();

                if (methodEdges.Count > 0)
                    return FormatKeywordResults(query, methodEdges);
            }

            // --- 2. Keyword search mode ---
            // Accents are dropped so that "déplacer" meets the synonym "deplacer" and the
            // accent-free semantic hints of the graph.
            string plainQuery = RemoveDiacritics(query);
            var expandedKeywords = new List<string>();
            var rawTokens = plainQuery.Split(new[] { ' ', ',', '_', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !StopWords.Contains(t))
                .ToArray();
            if (rawTokens.Length == 0)
                rawTokens = plainQuery.Split(new[] { ' ', ',', '_', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries);

            // Case-insensitive full match in Synonyms (for multi-word terms like "mise en plan")
            if (Synonyms.TryGetValue(plainQuery, out var querySyns))
            {
                expandedKeywords.AddRange(querySyns);
            }
            else
            {
                // CamelCase split each token (ex: "BackReferences" → "Back", "References")
                var splitTokens = new List<string>();
                foreach (var token in rawTokens)
                {
                    var camelParts = SplitCamelCase(token);
                    if (camelParts.Length > 0)
                        splitTokens.AddRange(camelParts);
                    else
                        splitTokens.Add(token);
                }

                // Expand synonyms (ex: "bom" → "IBoms", "rename" → "Set" + "Name")
                foreach (var token in splitTokens)
                {
                    if (Synonyms.TryGetValue(token, out var syns))
                        expandedKeywords.AddRange(syns);
                    else
                        expandedKeywords.Add(token);
                }
            }

            // Deduplicate case-insensitive
            string[] keywords = expandedKeywords
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Strict mode: every keyword must match. The pool is unbounded so that scoring sees
            // every candidate (a cap here used to drop IPdm methods behind generic "Get" hits).
            var searchResults = graph.SearchByKeywords(keywords, int.MaxValue);

            var sortedResults = searchResults
                .Select(e => new { Edge = e, Score = CalculateScore(e, query, keywords) })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Edge.MethodName)
                .Select(x => x.Edge)
                .ToList();

            // Natural-language queries rarely match on every word, and a full match can be the
            // wrong method ("déplacer dossier" -> MoveProjectFolder, not MoveSeveral): complete
            // the list with the methods ranked by the words they do match.
            if (sortedResults.Count < MaxKeywordResults)
            {
                var strict = new HashSet<GraphEdge>(sortedResults);
                sortedResults.AddRange(RankedSearch(graph, keywords, rawTokens, query).Where(e => !strict.Contains(e)));
            }

            // A query word that is a method name ("GetConstituents folder", "SetMaterial part")
            // names the method the caller is after: list it first.
            var namedMethods = graph.GetEdges()
                .Where(e => e.MethodName != null && rawTokens.Any(t => string.Equals(t, e.MethodName, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(e => Tier1Rank(e.Interface))
                .ThenBy(e => e.Interface)
                .ToList();
            if (namedMethods.Count > 0)
            {
                var named = new HashSet<GraphEdge>(namedMethods);
                sortedResults = namedMethods.Concat(sortedResults.Where(e => !named.Contains(e))).ToList();
            }

            if (sortedResults.Count == 0)
            {
                return "No result for '" + query + "'. Suggestions:\n" +
                    "- Try an exact interface name: IParameters, IPdm, IDocuments, IShapes\n" +
                    "- Try an English keyword: sketch, export, family, assembly\n" +
                    "- Try Interface.Prefix: IDocuments.Export, IPdm.Search";
            }

            return FormatKeywordResults(query, sortedResults);
        }

        /// <summary>
        /// Ranks edges by the search words they match, each word weighted by its rarity in the
        /// graph: "Move" (a handful of methods) outweighs "Document" (hundreds). A word matches a
        /// method-name part, the interface, the semantic hint or the description.
        /// </summary>
        private List<GraphEdge> RankedSearch(TypeGraph graph, string[] keywords, string[] rawTokens, string query)
        {
            var edges = graph.GetEdges();
            var terms = keywords
                .Concat(rawTokens.Where(t => t.Length >= 3))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (terms.Length == 0) return new List<GraphEdge>();

            var matchesByTerm = new Dictionary<string, HashSet<GraphEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in terms)
                matchesByTerm[term] = new HashSet<GraphEdge>(edges.Where(e => EdgeMatches(e, term)));

            double total = Math.Max(1, edges.Count);
            var weights = matchesByTerm.ToDictionary(
                kv => kv.Key,
                kv => Math.Log(total / (1 + kv.Value.Count)),
                StringComparer.OrdinalIgnoreCase);

            return edges
                .Select(e => new
                {
                    Edge = e,
                    Weight = terms.Where(t => matchesByTerm[t].Contains(e)).Sum(t => weights[t])
                })
                .Where(x => x.Weight > 0)
                .OrderByDescending(x => x.Weight)
                .ThenByDescending(x => CalculateScore(x.Edge, query, keywords))
                .ThenBy(x => x.Edge.MethodName)
                .Select(x => x.Edge)
                .ToList();
        }

        /// <summary>Position in <see cref="Tier1Interfaces"/>; other interfaces come after.</summary>
        private static int Tier1Rank(string interfaceName)
        {
            int ix = Array.FindIndex(Tier1Interfaces, t => string.Equals(t, interfaceName, StringComparison.OrdinalIgnoreCase));
            return ix < 0 ? int.MaxValue : ix;
        }

        private static bool EdgeMatches(GraphEdge e, string term)
        {
            if (e.MethodName != null && SplitCamelCase(e.MethodName).Any(w => string.Equals(w, term, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (e.Interface != null && string.Equals(e.Interface, term, StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.SemanticHint != null && RemoveDiacritics(e.SemanticHint).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return e.Description != null && term.Length >= 4 &&
                e.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private int CalculateScore(GraphEdge edge, string query, string[] keywords)
        {
            int score = 0;
            // Exact method-name match
            if (string.Equals(edge.MethodName, query, StringComparison.OrdinalIgnoreCase)) score += 10;
            // Interface match
            if (edge.Interface != null && edge.Interface.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 5;
            // Description match
            if (edge.Description != null && edge.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 2;
            // Match SemanticHint
            if (edge.SemanticHint != null && keywords.Any(k => edge.SemanticHint.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)) score += 2;
            // Tier 1
            if (edge.Interface != null && Tier1Interfaces.Any(t => string.Equals(t, edge.Interface, StringComparison.OrdinalIgnoreCase))) score += 3;
            
            return score;
        }

        private string FormatInterface(string name, List<GraphEdge> edges, bool isFiltered = false)
        {
            // One edge per parameter type in the graph: count and show each method once.
            edges = DistinctMethods(edges).ToList();
            int total = edges.Count;
            int limit = 50;
            var displayEdges = edges.OrderBy(e => e.MethodName).Take(limit).ToList();

            var groups = displayEdges.GroupBy(e => GetCategory(e.MethodName))
                                     .OrderBy(g => Array.IndexOf(OrderedCategories, g.Key));

            var lines = new List<string>();
            string header = isFiltered ? $"=== {name} (filtered) - {total} method(s) ===" : $"=== {name} - {total} method(s) ===";
            lines.Add(header + "\n");

            bool withRemarks = displayEdges.Count <= FullRemarksMaxMethods;
            foreach (var group in groups)
            {
                lines.Add($"--- {group.Key} ({group.Count()} methods) ---");
                foreach (var edge in group)
                {
                    lines.Add(FormatEdge(edge, withRemarks));
                }
                lines.Add("");
            }

            if (total > limit)
            {
                lines.Add($"... and {total - limit} more method(s). Use api_help(\"{name}.Prefix\") to filter.");
            }

            foreach (string tip in GetUsageTips(name))
            {
                lines.Add("Tip: " + tip);
            }

            return string.Join("\n", lines);
        }

        private string FormatKeywordResults(string query, List<GraphEdge> rankedEdges)
        {
            // The graph holds one edge per parameter type: show each method once, best first.
            var edges = DistinctMethods(rankedEdges)
                .Take(MaxKeywordResults)
                .ToList();

            var lines = new List<string>();
            lines.Add($"{edges.Count} result(s) for \"{query}\":\n");

            // Interfaces in order of their best-ranked method.
            var groups = edges.GroupBy(e => e.Interface ?? "Other");

            bool withRemarks = edges.Count <= FullRemarksMaxMethods;
            foreach (var group in groups)
            {
                lines.Add($"{group.Key} ({group.Count()} method(s)):");
                foreach (var edge in group)
                {
                    lines.Add("  " + FormatEdge(edge, withRemarks));
                }
                lines.Add("");
            }

            // Append the contextual usage tips that match the query.
            foreach (string tip in GetUsageTips(query))
            {
                lines.Add("Tip: " + tip);
            }

            return string.Join("\n", lines);
        }

        /// <summary>First edge of each method (interface, name, signature), in input order.</summary>
        private static IEnumerable<GraphEdge> DistinctMethods(IEnumerable<GraphEdge> edges)
        {
            return edges
                .GroupBy(e => (e.Interface ?? "") + "|" + e.MethodName + "|" + e.MethodSignature)
                .Select(g => g.First());
        }

        private IEnumerable<string> GetUsageTips(string query)
        {
            string plainQuery = RemoveDiacritics(query);
            return UsageTips
                .Where(kvp => plainQuery.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(kvp => kvp.Value)
                .Distinct()
                .Take(MaxUsageTips);
        }

        private string GetCategory(string methodName)
        {
            if (methodName.EndsWith("Value") && methodName.StartsWith("Get")) return "Value reads";
            if (methodName.EndsWith("Value") && methodName.StartsWith("Set")) return "Value writes";
            if (methodName.StartsWith("Get")) return "Navigation / Queries";
            if (methodName.StartsWith("Create")) return "Creation";
            if (methodName.StartsWith("Search")) return "Search";
            if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove")) return "Deletion";
            if (methodName.StartsWith("Export")) return "Export";
            return "Other";
        }

        private string FormatEdge(GraphEdge edge, bool withRemarks = false)
        {
            string signature = !string.IsNullOrEmpty(edge.MethodSignature)
                ? edge.MethodSignature
                : $"{edge.Interface ?? "Unknown"}.{edge.MethodName}(...)";

            string line = string.IsNullOrEmpty(edge.Description)
                ? signature
                : string.Format("{0} — {1}", signature, edge.Description);

            string tags = RemarkTags(edge);
            if (tags.Length > 0) line += " " + tags;

            if (withRemarks && !string.IsNullOrWhiteSpace(edge.Remarks))
                line += "\n      Remarks: " + edge.Remarks.Trim();

            return line;
        }

        // Official remark sentences (API reference), as counted in graph.json: 63 methods "must not be
        // called between StartModification and EndModification", 376 need StartModification AND
        // EnsureIsDirty, 42 need StartModification only. "may be called before" is optional: no tag.
        private static readonly Regex OutsideTransactionRemark = new Regex(
            @"must not be called between\s+StartModification", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InsideWithEnsureIsDirtyRemark = new Regex(
            @"StartModification\s*\([^)]*\)\s*and\s+EnsureIsDirty\s*\([^)]*\)\s*must be called", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InsideTransactionRemark = new Regex(
            @"StartModification\s*\([^)]*\)\s*must be called before|must be called after the\s+StartModification", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Full remarks are printed under each method when a result lists at most this many.</summary>
        private const int FullRemarksMaxMethods = 3;

        /// <summary>
        /// Short tags from the official remarks: transaction rule, obsolescence, and the version for
        /// methods recent enough (7.20+) to be missing from an older TopSolid.
        /// </summary>
        private static string RemarkTags(GraphEdge edge)
        {
            var tags = new List<string>();
            string remarks = edge.Remarks ?? "";

            if (OutsideTransactionRemark.IsMatch(remarks))
                tags.Add("[outside StartModification]");
            else if (InsideWithEnsureIsDirtyRemark.IsMatch(remarks))
                tags.Add("[inside StartModification + EnsureIsDirty]");
            else if (InsideTransactionRemark.IsMatch(remarks))
                tags.Add("[inside StartModification]");

            // The signature may already carry [ObsoleteAttribute("...")]: do not repeat the message.
            bool signatureSaysObsolete = (edge.MethodSignature ?? "").IndexOf("Obsolete", StringComparison.Ordinal) >= 0;
            if (!string.IsNullOrWhiteSpace(edge.ObsoleteMessage) && !signatureSaysObsolete)
                tags.Add("[OBSOLETE: " + edge.ObsoleteMessage.Trim() + "]");
            else if (edge.Deprecated || !string.IsNullOrWhiteSpace(edge.ObsoleteMessage))
                tags.Add("[OBSOLETE]");

            string since = RecentVersion(edge.Since);
            if (since != null) tags.Add("[since " + since + "]");

            return string.Join(" ", tags);
        }

        /// <summary>"7.21.100" gives "7.21"; versions before 7.20 give null.</summary>
        private static string RecentVersion(string since)
        {
            if (string.IsNullOrWhiteSpace(since)) return null;
            Match m = Regex.Match(since, @"^\s*v?(\d+)\.(\d+)");
            if (!m.Success) return null;

            int major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int minor = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (major < 7 || (major == 7 && minor < 20)) return null;
            return major + "." + minor;
        }

        /// <summary>
        /// Caps the output length and appends an explicit marker when text was cut.
        /// </summary>
        private static string Truncate(string output)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputChars)
                return output;

            return output.Substring(0, MaxOutputChars) + "\n... [output truncated - refine your query]";
        }
    }
}

