import json
import os
import re

def enrich_graph():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    data_dir = os.path.join(script_dir, "..", "data")
    
    graph_path = os.path.join(data_dir, "graph.json")
    api_index_path = os.path.join(data_dir, "api-index.json")

    print(f"Loading graph from {graph_path}...")
    with open(graph_path, 'r', encoding='utf-8') as f:
        graph_data = json.load(f)
        
    print(f"Loading API index from {api_index_path}...")
    with open(api_index_path, 'r', encoding='utf-8') as f:
        api_index = json.load(f)

    # Build lookup table from API index
    lookup = {}
    interfaces_data = api_index.get("interfaces", {})
    total_index_methods = 0
    for iface_name, iface_info in interfaces_data.items():
        for method in iface_info.get("methods", []):
            method_name = method.get("name")
            key = f"{iface_name}.{method_name}"
            lookup[key] = {
                "interface": iface_name,
                "description": method.get("description"),
                "since": method.get("since"),
                "signature": method.get("signature")
            }
            total_index_methods += 1

    print(f"Loaded {total_index_methods} methods from API index across {len(interfaces_data)} interfaces.")

    # Process graph edges
    edges = graph_data.get("_edges", [])
    if not edges:
        edges = graph_data.get("Edges", []) # Depends on serialization
        
    matched = 0
    unmatched = 0
    
    # Track which methods form the lookup were actually used
    used_lookup_keys = set()
    
    for edge in edges:
        method_name = edge.get("MethodName", "")
        iface_name = edge.get("Interface")
        
        if not iface_name:
            # Extract interface from MethodSignature
            # e.g. "TopSolid.Kernel.Automating.IParameters.GetRealValue(...) -> System.Double"
            method_sig = edge.get("MethodSignature", "")
            
            # Simple extraction logic: find the part before .MethodName(
            match = re.search(r'\.([A-Z]\w+)\.' + re.escape(method_name) + r'\(', method_sig)
            
            if match:
                iface_name = match.group(1)
            else:
                # Fallback if the signature format is different
                parts = method_sig.split('(')[0].split('.')
                if len(parts) >= 2:
                    iface_name = parts[-2]
                
        if iface_name and method_name:
            key = f"{iface_name}.{method_name}"
            
            # Save interface to edge (even if not in documentation)
            edge["Interface"] = iface_name
            
            if key in lookup:
                info = lookup[key]
                # Enrichment
                if not edge.get("Description"):
                    edge["Description"] = info["description"]
                if not edge.get("Since"):
                    edge["Since"] = info["since"]
                
                # If signature is already cleaned, don't overwrite with identical content
                # only overwrite if it looks like a reflexed signature (contains -> or dots)
                if "." in edge.get("MethodSignature", "") or "->" in edge.get("MethodSignature", ""):
                    if info.get("signature"):
                        edge["MethodSignature"] = info["signature"]
                
                matched += 1
                used_lookup_keys.add(key)
            else:
                unmatched += 1
        else:
            unmatched += 1

    print("\n--- Statistics ---")
    print(f"Total graph edges processed: {len(edges)}")
    print(f"Edges enriched (matched): {matched}")
    print(f"Edges not enriched (unmatched): {unmatched}")
    print(f"Methods in index without corresponding edge: {total_index_methods - len(used_lookup_keys)}")
    
    # Phase 2: Inject ALL missing methods from api-index into graph
    # M-55: Extended from 5 targeted methods to all 64+ missing methods
    print("\n--- Phase 2: Injecting missing methods (all api-index) ---")

    # Build interface namespace map from api-index
    IFACE_NAMESPACE = {}
    for iface_name, iface_info in interfaces_data.items():
        ns = iface_info.get("namespace", "")
        IFACE_NAMESPACE[iface_name] = f"{ns}.{iface_name}" if ns else iface_name

    # Comprehensive type mapping: short name -> full CLR name
    TYPE_MAPPING = {
        # Primitives
        "void": "System.Void",
        "int": "System.Int32",
        "long": "System.Int64",
        "string": "System.String",
        "bool": "System.Boolean",
        "double": "System.Double",
        "float": "System.Single",
        "byte": "System.Byte",
        # .NET types
        "Bitmap": "System.Drawing.Bitmap",
        "Color": "System.Drawing.Color",
        # TopSolid Kernel types
        "PdmObjectId": "TopSolid.Kernel.Automating.PdmObjectId",
        "PdmMajorRevisionId": "TopSolid.Kernel.Automating.PdmMajorRevisionId",
        "PdmMinorRevisionId": "TopSolid.Kernel.Automating.PdmMinorRevisionId",
        "PdmProjectFolderId": "TopSolid.Kernel.Automating.PdmProjectFolderId",
        "PdmLifeCycleMainState": "TopSolid.Kernel.Automating.PdmLifeCycleMainState",
        "ElementId": "TopSolid.Kernel.Automating.ElementId",
        "ElementItemId": "TopSolid.Kernel.Automating.ElementItemId",
        "DocumentId": "TopSolid.Kernel.Automating.DocumentId",
        "PropertyDefinition": "TopSolid.Kernel.Automating.PropertyDefinition",
        "PropertyType": "TopSolid.Kernel.Automating.PropertyType",
        "Real": "TopSolid.Kernel.Automating.Real",
        "SmartText": "TopSolid.Kernel.Automating.SmartText",
        # TopSolid Drafting types
        "CellType": "TopSolid.Cad.Drafting.Automating.CellType",
        "CellValueType": "TopSolid.Cad.Drafting.Automating.CellValueType",
        "TableType": "TopSolid.Cad.Drafting.Automating.TableType",
        # TopSolid Design types
        "BendFeature": "TopSolid.Cad.Design.Automating.BendFeature",
        "ConstraintDriverData": "TopSolid.Cad.Design.Automating.ConstraintDriverData",
    }

    def get_full_type(type_name):
        """Resolve short type name to full CLR name."""
        if "List<" in type_name or "SmartList<" in type_name:
            return "System.Collections.Generic.List"
        # Check explicit mapping first
        if type_name in TYPE_MAPPING:
            return TYPE_MAPPING[type_name]
        # Check interface namespace map (for interfaces as parameter types)
        if type_name in IFACE_NAMESPACE:
            return IFACE_NAMESPACE[type_name]
        # Check existing graph nodes by short name
        for node_name in graph_data["_nodes"]:
            if node_name.endswith("." + type_name):
                return node_name
        # Fallback: return as-is
        return type_name

    PRIMITIVE_TYPES = {"int", "string", "bool", "double", "float", "long", "byte",
                       "System.Int32", "System.String", "System.Boolean", "System.Double",
                       "System.Single", "System.Int64", "System.Byte", "System.Void",
                       "void"}

    def is_primitive(type_name):
        return type_name in PRIMITIVE_TYPES

    injected_count = 0
    skipped_count = 0
    parse_errors = []

    for iface_name, iface_info in interfaces_data.items():
        for method in iface_info.get("methods", []):
            method_name = method.get("name")
            key = f"{iface_name}.{method_name}"

            # Skip if already present in graph (populated during Phase 1)
            if key in used_lookup_keys:
                skipped_count += 1
                continue

            # Parse signature: "ReturnType MethodName( Type1 Param1, Type2 Param2 )"
            # Also handle [ObsoleteAttribute(...)] prefix on deprecated methods
            sig = method.get("signature", "")
            clean_sig = re.sub(r"^\[.*?\]\s*", "", sig)
            match = re.match(r"^([\w<>]+)\s+(\w+)\s*\((.*)\)$", clean_sig)
            if not match:
                parse_errors.append(f"{key}: '{sig}'")
                continue

            ret_type_short = match.group(1)
            params_str = match.group(3)

            ret_type_full = get_full_type(ret_type_short)

            param_types_short = []
            if params_str.strip():
                for p in params_str.split(','):
                    p = p.strip()
                    if p:
                        p_parts = [part for part in p.split(' ') if part and part not in ('out', 'ref')]
                        if p_parts:
                            param_types_short.append(p_parts[0])

            # Create edges: declaring interface + each parameter type -> return type
            sources_short = [iface_name] + param_types_short
            unique_sources = list(set(sources_short))

            for src_short in unique_sources:
                src_full = get_full_type(src_short)

                new_edge = {
                    "Source": { "TypeName": src_full, "IsPrimitive": is_primitive(src_short) },
                    "Target": { "TypeName": ret_type_full, "IsPrimitive": is_primitive(ret_type_short) },
                    "MethodName": method_name,
                    "MethodSignature": sig,
                    "Weight": 10 if is_primitive(src_short) else 1,
                    "IsStatic": False,
                    "Interface": iface_name,
                    "Description": method.get("description"),
                    "Since": method.get("since"),
                    "SemanticHint": None
                }
                edges.append(new_edge)

                # Ensure nodes exist
                for t_full, is_prim in [(src_full, is_primitive(src_short)), (ret_type_full, is_primitive(ret_type_short))]:
                    if t_full not in graph_data["_nodes"]:
                        graph_data["_nodes"][t_full] = {
                            "TypeName": t_full,
                            "IsPrimitive": is_prim
                        }

            injected_count += 1
            used_lookup_keys.add(key)

    print(f"Injected {injected_count} methods ({skipped_count} already present, {len(parse_errors)} parse errors).")
    if parse_errors:
        for err in parse_errors:
            print(f"  Parse error: {err}")

    # Sub-task B: Manual injection of SearchMajorRevisionBackReferences
    print("Injecting SearchMajorRevisionBackReferences edge manually...")
    new_edge = {
        "Source": { "TypeName": "TopSolid.Kernel.Automating.PdmObjectId", "IsPrimitive": False },
        "Target": { "TypeName": "System.Collections.Generic.List", "IsPrimitive": False },
        "MethodName": "SearchMajorRevisionBackReferences",
        "MethodSignature": "SmartList<PdmObjectId> SearchMajorRevisionBackReferences(PdmObjectId projectId, PdmObjectId majorRevisionId)",
        "Weight": 2,
        "IsStatic": False,
        "Interface": "IPdm",
        "Description": "Recherche les back-references (cas d'emploi) d'une revision majeure dans un projet",
        "Since": "7.17",
        "SemanticHint": "where-used, cas d'emploi, back-references, qui utilise ce document"
    }
    edges.append(new_edge)

    # Phase 3: Extract examples from REDACTED-USER .cs files
    print("\n--- Phase 3: Extracting examples from .cs files ---")

    EXAMPLES_DIRS = [
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples REDACTED-USER",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples RoB",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\SelfLearning-Exercises-master",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\TopSolidKernelAutomationExamples-main",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\ExportMAPMEPdepuisNomenclature",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\DraftSwitch",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Extraction CSV Liste de documents TopSolid",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\TopSolid Material Creator",
        r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Traitement par lot",
    ]

    # Mapping from TopSolidHost.Property to Interface name
    HOST_MAPPING = {
        "Pdm": "IPdm",
        "Documents": "IDocuments",
        "Elements": "IElements",
        "Parameters": "IParameters",
        "Families": "IFamilies",
        "Geometries3D": "IGeometries3D",
        "Geometries2D": "IGeometries2D",
        "Sketches2D": "ISketches2D",
        "Entities": "IEntities",
        "Application": "IApplication",
        "User": "IUser",
        "Shapes": "IShapes",
        "Operations": "IOperations",
        "Units": "IUnits",
        "Layers": "ILayers",
        "Materials": "IMaterials",
        "Textures": "ITextures",
        "Options": "IOptions",
    }
    DESIGN_HOST_MAPPING = {
        "Representations": "IRepresentations",
        "Assemblies": "IAssemblies",
        "Parts": "IParts",
        "Coatings": "ICoatings",
        "Finishings": "IFinishings",
        "Materials": "IDesignMaterials",
        "Substitutions": "ISubstitutions",
        "Processes": "IProcesses",
        "MultiLayers": "IMultiLayer",
        "Tools": "ITools",
        "Boms": "IBoms",
        "Unfoldings": "IUnfoldings",
        "Visualization3D": "IVisualization3D",
        "Sketches3D": "ISketches3D",
        "Simulations": "ISimulations",
        "Collisions": "ICollisions",
    }
    DRAFTING_HOST_MAPPING = {
        "Draftings": "IDraftings",
        "Tables": "ITables",
    }

    SKIP_DIRS = {'bin', 'obj', 'packages', '.vs', 'TestResults'}

    def find_cs_files(root_dir):
        """Recursively find all .cs files, skipping build/designer/generated files."""
        cs_files = []
        for dirpath, dirnames, filenames in os.walk(root_dir):
            # Prune build directories in-place (prevents os.walk from descending)
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if fn.endswith(".cs") and not fn.endswith(".Designer.cs") and fn != "AssemblyInfo.cs":
                    cs_files.append(os.path.join(dirpath, fn))
        return cs_files

    def extract_methods_from_cs(filepath):
        """Extract C# method blocks from a file. Returns list of (method_name, line_start, lines)."""
        with open(filepath, 'r', encoding='utf-8', errors='replace') as f:
            all_lines = f.readlines()

        methods = []
        # Match method signatures: access_modifier [static] return_type MethodName(
        method_pattern = re.compile(
            r'^\s*(?:public|private|protected|internal)\s+'
            r'(?:static\s+)?'
            r'(?:override\s+)?'
            r'(?:async\s+)?'
            r'[\w<>\[\],\s]+?\s+'  # return type
            r'(\w+)\s*\('  # method name
        )

        i = 0
        while i < len(all_lines):
            m = method_pattern.match(all_lines[i])
            if m:
                method_name = m.group(1)
                # Skip constructors and simple getters
                if method_name in ('InitializeComponent', 'Dispose'):
                    i += 1
                    continue

                # Find the opening brace
                brace_start = i
                while brace_start < len(all_lines) and '{' not in all_lines[brace_start]:
                    brace_start += 1
                if brace_start >= len(all_lines):
                    i += 1
                    continue

                # Track braces to find method end
                depth = 0
                method_lines = []
                for j in range(brace_start, len(all_lines)):
                    line = all_lines[j]
                    depth += line.count('{') - line.count('}')
                    method_lines.append(line)
                    if depth <= 0:
                        break

                methods.append((method_name, i, method_lines))
                i = brace_start + len(method_lines)
            else:
                i += 1

        return methods

    def extract_api_calls_with_context(method_name, method_lines, filename, context_radius=5):
        """Find TopSolidHost/TopSolidDesignHost API calls and extract context snippets."""
        snippets = {}  # key: "Interface.Method" -> list of snippet strings

        api_pattern = re.compile(r'(TopSolidHost|TopSolidDesignHost|TopSolidDraftingHost)\.(\w+)\.(\w+)\s*\(')

        for idx, line in enumerate(method_lines):
            for match in api_pattern.finditer(line):
                host_type = match.group(1)
                prop = match.group(2)
                api_method = match.group(3)

                # Skip property getters like get_Version
                if api_method.startswith("get_") or api_method.startswith("set_"):
                    continue

                # Map to interface name
                if host_type == "TopSolidHost":
                    iface = HOST_MAPPING.get(prop)
                elif host_type == "TopSolidDraftingHost":
                    iface = DRAFTING_HOST_MAPPING.get(prop)
                else:
                    iface = DESIGN_HOST_MAPPING.get(prop)

                if not iface:
                    continue

                key = f"{iface}.{api_method}"

                # Extract context window
                start = max(0, idx - context_radius)
                end = min(len(method_lines), idx + context_radius + 1)
                snippet_lines = method_lines[start:end]

                # Clean up: strip common leading whitespace
                stripped = [l.rstrip('\n\r') for l in snippet_lines]
                if stripped:
                    min_indent = min((len(l) - len(l.lstrip()) for l in stripped if l.strip()), default=0)
                    stripped = [l[min_indent:] if len(l) >= min_indent else l for l in stripped]

                short_filename = os.path.basename(filename)
                header = f"// {short_filename} — {method_name}()"
                snippet = header + "\n" + "\n".join(stripped)

                if key not in snippets:
                    snippets[key] = []
                snippets[key].append(snippet)

        return snippets

    # Build the examples index from ALL example directories
    examples_index = {}  # "Interface.Method" -> [snippet1, snippet2, ...]

    for examples_dir in EXAMPLES_DIRS:
        dir_label = os.path.basename(examples_dir)
        if not os.path.isdir(examples_dir):
            print(f"WARNING: Examples directory not found: {examples_dir}")
            continue

        cs_files = find_cs_files(examples_dir)
        print(f"[{dir_label}] Found {len(cs_files)} .cs files.")

        dir_methods_count = 0
        for cs_file in cs_files:
            methods = extract_methods_from_cs(cs_file)
            for method_name, line_start, method_lines in methods:
                snippets = extract_api_calls_with_context(method_name, method_lines, cs_file)
                for key, snippet_list in snippets.items():
                    if key not in examples_index:
                        examples_index[key] = []
                    examples_index[key].extend(snippet_list)
                    dir_methods_count += len(snippet_list)

        print(f"[{dir_label}] Extracted {dir_methods_count} snippets.")

    print(f"Total: examples for {len(examples_index)} unique API methods.")

    # Manual injection: IBoms examples from VB.NET source (Affectation Reference Auto)
    # These use the same TopSolid API but in VB syntax — converted to C# snippets
    VB_BOM_EXAMPLES = {
        "IBoms.IsBom": [
            "// Accueil.vb — Affectation Reference Auto (VB->C#)\n"
            "// Check if document is a BOM\n"
            "if (TopSolidDesignHost.Boms.IsBom(docId)) {\n"
            "    // proceed with BOM operations\n"
            "} else {\n"
            "    // Not a BOM document\n"
            "}"
        ],
        "IBoms.GetColumnCount": [
            "// Accueil.vb — BOM column enumeration (VB->C#)\n"
            "int columnCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n"
            "for (int i = 0; i < columnCount; i++) {\n"
            "    var propDef = TopSolidDesignHost.Boms.GetColumnPropertyDefinition(docId, i);\n"
            "    // propDef.Name, propDef.Domain\n"
            "}"
        ],
        "IBoms.GetColumnPropertyDefinition": [
            "// Accueil.vb — Get property definitions for BOM columns (VB->C#)\n"
            "var propDefs = new List<PropertyDefinition>();\n"
            "int colCount = TopSolidDesignHost.Boms.GetColumnCount(docId);\n"
            "for (int i = 0; i < colCount; i++)\n"
            "    propDefs.Add(TopSolidDesignHost.Boms.GetColumnPropertyDefinition(docId, i));"
        ],
        "IBoms.GetRootRow": [
            "// Accueil.vb — Traverse BOM tree recursively (VB->C#)\n"
            "int rootRow = TopSolidDesignHost.Boms.GetRootRow(docId);\n"
            "var children = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rootRow);\n"
            "// Iterate children, check for sub-children (assemblies vs parts)"
        ],
        "IBoms.GetRowChildrenRows": [
            "// Accueil.vb — Recursive BOM traversal (VB->C#)\n"
            "var childRows = TopSolidDesignHost.Boms.GetRowChildrenRows(docId, rowNumber);\n"
            "if (childRows.Count > 0) {\n"
            "    // This row is an assembly, recurse into children\n"
            "    foreach (int childRow in childRows) { /* process */ }\n"
            "} else {\n"
            "    // This row is a leaf part\n"
            "}"
        ],
        "IBoms.GetRowContents": [
            "// Accueil.vb — Read BOM row properties and texts (VB->C#)\n"
            "List<Property> props; List<string> texts;\n"
            "TopSolidDesignHost.Boms.GetRowContents(docId, rowNumber, out props, out texts);\n"
            "// texts contains the cell values as strings\n"
            "// Compare texts across rows to group identical parts"
        ],
        "IBoms.GetRowCountedEntities": [
            "// Accueil.vb — Get entities counted in a BOM row (VB->C#)\n"
            "var entities = TopSolidDesignHost.Boms.GetRowCountedEntities(docId, rowNumber);\n"
            "// Each entity is an ElementId representing a counted part/assembly\n"
            "// Used for assigning BOM indices: SetNodeBomIndex(entity, \"P001\")"
        ],
    }

    for key, snippets in VB_BOM_EXAMPLES.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_BOM_EXAMPLES)} IBoms methods from VB.NET source.")

    # Manual injection: Advanced Parameters & PDM from VB.NET (Macro Traitement proprietes SW)
    VB_PARAMS_EXAMPLES = {
        "IParameters.GetDescriptionParameter": [
            "// Accueil.vb — Macro Traitement proprietes SW (VB->C#)\n"
            "// Direct access to the Description system parameter\n"
            "ElementId descId = TopSolidHost.Parameters.GetDescriptionParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextValue(descId, newDescription);"
        ],
        "IParameters.GetNameParameter": [
            "// Accueil.vb — Get the Name system parameter directly (VB->C#)\n"
            "ElementId nameId = TopSolidHost.Parameters.GetNameParameter(docId);\n"
            "string currentName = TopSolidHost.Parameters.GetTextValue(nameId);"
        ],
        "IParameters.GetPartNumberParameter": [
            "// Accueil.vb — Get PartNumber (Reference) parameter directly (VB->C#)\n"
            "ElementId pnId = TopSolidHost.Parameters.GetPartNumberParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextValue(pnId, newPartNumber);"
        ],
        "IParameters.GetManufacturerParameter": [
            "// Accueil.vb — Get Manufacturer parameter directly (VB->C#)\n"
            "ElementId mfrId = TopSolidHost.Parameters.GetManufacturerParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextValue(mfrId, \"Bosch\");"
        ],
        "IParameters.GetManufacturerPartNumberParameter": [
            "// Accueil.vb — Get ManufacturerPartNumber parameter directly (VB->C#)\n"
            "ElementId mfrPnId = TopSolidHost.Parameters.GetManufacturerPartNumberParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextValue(mfrPnId, fabricantRef);"
        ],
        "IParameters.SetTextParameterizedValue": [
            "// Accueil.vb — Set a parameterized text value with formula (VB->C#)\n"
            "// Makes the Name parameter always equal to PartNumber\n"
            "ElementId nameId = TopSolidHost.Parameters.GetNameParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextParameterizedValue(nameId, \"[$PartNumber]\");",
            "// Accueil.vb — Compound formula: Ref + Revision - Designation (VB->C#)\n"
            "ElementId nameId = TopSolidHost.Parameters.GetNameParameter(docId);\n"
            "TopSolidHost.Parameters.SetTextParameterizedValue(nameId, \"[$PartNumber][$MajorRevision]-[$Description]\");\n"
            "// Available tokens: [$PartNumber], [$Description], [$MajorRevision], [$Manufacturer], etc."
        ],
        "IParameters.CreateUserPropertyParameter": [
            "// Accueil.vb — Create user property parameter from .TopPrd doc (VB->C#)\n"
            "// userPropDocId is the DocumentId of the .TopPrd property definition\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateUserPropertyParameter(docId, userPropDocId);\n"
            "// Then read its enumeration values\n"
            "DocumentId enumDef = TopSolidHost.Parameters.GetUserEnumerationDefinition(newParam);"
        ],
        "IParameters.GetUserEnumerationDefinition": [
            "// Accueil.vb — Get enumeration definition for user property (VB->C#)\n"
            "DocumentId enumDef = TopSolidHost.Parameters.GetUserEnumerationDefinition(paramId);\n"
            "List<int> values; List<string> texts;\n"
            "TopSolidHost.Parameters.GetUserEnumerationValues(enumDef, out values, out texts);"
        ],
        "IParameters.GetUserEnumerationValues": [
            "// Accueil.vb — Read possible values of user enumeration (VB->C#)\n"
            "List<int> values; List<string> texts;\n"
            "TopSolidHost.Parameters.GetUserEnumerationValues(enumDef, out values, out texts);\n"
            "// texts = {\"Usinage\", \"Tolerie\", \"Standard\"}, values = {0, 1, 2}"
        ],
        "IParameters.SetUserEnumerationValue": [
            "// Accueil.vb — Set user enumeration value by index (VB->C#)\n"
            "TopSolidHost.Parameters.SetUserEnumerationValue(paramId, values[i]);\n"
            "// values[i] is the int index matching the desired text"
        ],
        "IPdm.GetReferencedProjects": [
            "// Accueil.vb — Get referenced projects for user property search (VB->C#)\n"
            "PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);\n"
            "var refProjects = TopSolidHost.Pdm.GetReferencedProjects(projId);\n"
            "// Search for .TopPrd documents in referenced projects"
        ],
        "IPdm.SearchDocumentByName": [
            "// Accueil.vb — Search document by name in a project (VB->C#)\n"
            "var found = TopSolidHost.Pdm.SearchDocumentByName(projectId, \"Type Composant\");\n"
            "// Returns List<PdmObjectId>, filter by GetType for .TopPrd"
        ],
        "IParts.IsDerivedPart": [
            "// Accueil.vb — Check if part is a derivation (VB->C#)\n"
            "bool isDerived = TopSolidDesignHost.Parts.IsDerivedPart(docId);\n"
            "// Derived parts have special constraints on parameter modification"
        ],
    }

    for key, snippets in VB_PARAMS_EXAMPLES.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_PARAMS_EXAMPLES)} IParameters/IPdm/IParts methods from VB.NET source.")

    # Manual injection: Import workflow from TopSolidWorks Bridge VB.NET
    VB_IMPORT_EXAMPLES = {
        "IDocuments.Import": [
            "// Accueil.vb — TopSolidWorks Bridge: Import Parasolid file (VB->C#)\n"
            "PdmObjectId projId = TopSolidHost.Pdm.GetProject(pdmId);\n"
            "var importLog = new List<string>();\n"
            "var badDocs = new List<DocumentId>();\n"
            "// importerIndex 0 = first available importer\n"
            "var created = TopSolidHost.Documents.Import(0, filePath, projId, importLog, badDocs);\n"
            "// created = list of DocumentIds for imported documents\n"
            "DocumentId importedDoc = created[0];"
        ],
    }

    for key, snippets in VB_IMPORT_EXAMPLES.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_IMPORT_EXAMPLES)} IDocuments.Import from VB.NET source.")

    # Manual injection: Recursive PDM traversal + material search (Referenced Materials getting)
    VB_PDM_RECURSIVE = {
        "IPdm.GetConstituents": [
            "// Accueil.vb — Recursive PDM folder traversal (VB->C#)\n"
            "// Get all documents recursively from referenced projects\n"
            "var allDocs = new List<PdmObjectId>();\n"
            "var refProjects = TopSolidHost.Pdm.GetReferencedProjects(projId);\n"
            "foreach (var refProj in refProjects) {\n"
            "    List<PdmObjectId> folders, docs;\n"
            "    TopSolidHost.Pdm.GetConstituents(refProj, out folders, out docs);\n"
            "    allDocs.AddRange(docs);\n"
            "    // Recurse into sub-folders\n"
            "    while (folders.Count > 0) {\n"
            "        var nextFolders = new List<PdmObjectId>();\n"
            "        foreach (var f in folders) {\n"
            "            List<PdmObjectId> subFolders, subDocs;\n"
            "            TopSolidHost.Pdm.GetConstituents(f, out subFolders, out subDocs);\n"
            "            allDocs.AddRange(subDocs);\n"
            "            nextFolders.AddRange(subFolders);\n"
            "        }\n"
            "        folders = nextFolders;\n"
            "    }\n"
            "}"
        ],
        "IPdm.GetType": [
            "// Accueil.vb — Filter documents by type extension (VB->C#)\n"
            "// TopSolid document types:\n"
            "//   .TopPrt = Part, .TopAsm = Assembly, .TopDft = Drafting\n"
            "//   .TopPrd = User Property, .TopMat = Material\n"
            "string docType;\n"
            "TopSolidHost.Pdm.GetType(pdmObjId, out docType);\n"
            "if (docType == \".TopMat\") {\n"
            "    // This is a material document\n"
            "    string matName = TopSolidHost.Pdm.GetName(pdmObjId);\n"
            "}"
        ],
    }

    for key, snippets in VB_PDM_RECURSIVE.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_PDM_RECURSIVE)} IPdm recursive traversal from VB.NET source.")

    # Manual injection: SmartText parameter creation (Concateneur de parametres)
    VB_SMART_TEXT = {
        "IParameters.CreateSmartTextParameter": [
            "// Form1.vb — Concateneur de parametres (VB->C#)\n"
            "// Create a SmartText parameter with formula concatenation\n"
            "// formula example: ParamA & \" - \" & ParamB\n"
            "var stValue = new SmartText(SmartTextType.Formula, \"\", ElementId.Empty, ItemLabel.Empty, formula);\n"
            "TopSolidHost.Application.StartModification(\"Creation parametre\", true);\n"
            "TopSolidHost.Documents.EnsureIsDirty(ref docId);\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateSmartTextParameter(docId, stValue);\n"
            "TopSolidHost.Elements.SetName(newParam, \"MonParametre\");\n"
            "TopSolidHost.Application.EndModification(true, true);"
        ],
        "IElements.SetName": [
            "// Form1.vb — Rename a created element (VB->C#)\n"
            "// After creating a parameter, give it a friendly name\n"
            "TopSolidHost.Elements.SetName(paramElementId, \"Nom du parametre\");"
        ],
    }

    for key, snippets in VB_SMART_TEXT.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_SMART_TEXT)} SmartText/SetName from VB.NET source.")

    # Manual injection: Parameter copy/create (Copie Parametres)
    VB_PARAM_CREATE = {
        "IParameters.CreateRealParameter": [
            "// Accueil.vb — Copie Parametres: create real param (VB->C#)\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateRealParameter(docId, unitType, value);\n"
            "TopSolidHost.Elements.SetName(newParam, \"Longueur - Copie\");\n"
            "TopSolidHost.Elements.SetDescription(newParam, description);"
        ],
        "IParameters.CreateIntegerParameter": [
            "// Accueil.vb — Create integer parameter (VB->C#)\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateIntegerParameter(docId, intValue);\n"
            "TopSolidHost.Elements.SetName(newParam, \"Quantite\");"
        ],
        "IParameters.CreateBooleanParameter": [
            "// Accueil.vb — Create boolean parameter (VB->C#)\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateBooleanParameter(docId, boolValue);\n"
            "TopSolidHost.Elements.SetName(newParam, \"Actif\");"
        ],
        "IParameters.CreateTextParameter": [
            "// Accueil.vb — Create text parameter (VB->C#)\n"
            "ElementId newParam = TopSolidHost.Parameters.CreateTextParameter(docId, textValue);\n"
            "TopSolidHost.Elements.SetName(newParam, \"Commentaire\");"
        ],
        "IParameters.GetRealUnit": [
            "// Accueil.vb — Get unit type and symbol of a real parameter (VB->C#)\n"
            "UnitType unitType; string unitSymbol;\n"
            "TopSolidHost.Parameters.GetRealUnit(paramId, out unitType, out unitSymbol);\n"
            "// unitType = UnitType.Length, unitSymbol = \"mm\""
        ],
        "IElements.SearchByName": [
            "// Accueil.vb — Search element by name in document (VB->C#)\n"
            "ElementId found = TopSolidHost.Elements.SearchByName(docId, \"Longueur - Copie\");\n"
            "if (!found.IsEmpty) { /* element exists */ }"
        ],
        "IElements.GetDescription": [
            "// Accueil.vb — Get element description (VB->C#)\n"
            "string desc = TopSolidHost.Elements.GetDescription(paramId);"
        ],
        "IElements.SetDescription": [
            "// Accueil.vb — Set element description (VB->C#)\n"
            "TopSolidHost.Elements.SetDescription(paramId, \"Description du parametre\");"
        ],
    }

    for key, snippets in VB_PARAM_CREATE.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_PARAM_CREATE)} Parameter create/unit/search from VB.NET source.")

    # Manual injection: Material + Texture creation (TopSolid Material Creator)
    VB_MATERIAL = {
        "IPdm.CreateDocument": [
            "// Form1.vb — TopSolid Material Creator (VB->C#)\n"
            "// Create a material document in the current project\n"
            "PdmObjectId matPdmId = TopSolidHost.Pdm.CreateDocument(projectId, \".TopMat\", true);\n"
            "DocumentId matDocId = TopSolidHost.Documents.GetDocument(matPdmId);\n"
            "// Create texture as child of material\n"
            "PdmObjectId texPdmId = TopSolidHost.Pdm.CreateDocument(matPdmId, \".TopTex\", true);\n"
            "DocumentId texDocId = TopSolidHost.Documents.GetDocument(texPdmId);\n"
            "// Document types: .TopMat=Material, .TopTex=Texture, .TopPrt=Part,\n"
            "//   .TopAsm=Assembly, .TopDft=Drafting, .TopPrd=UserProperty"
        ],
        "IDocuments.SetName": [
            "// Form1.vb — Rename a document (not element) (VB->C#)\n"
            "TopSolidHost.Application.StartModification(\"Rename\", false);\n"
            "TopSolidHost.Documents.EnsureIsDirty(ref docId);\n"
            "TopSolidHost.Documents.SetName(docId, \"Acier S235\");\n"
            "TopSolidHost.Application.EndModification(true, true);"
        ],
        "ITextures.SetCategory": [
            "// Form1.vb — Set texture category type (VB->C#)\n"
            "// Categories: Albedo, Roughness, Metalness, Normal, Opacity, AmbientOcclusion\n"
            "TopSolidHost.Textures.SetCategory(texDocId, TextureCategoryType.Albedo);"
        ],
        "ITextures.SetPicture": [
            "// Form1.vb — Assign image file to texture (VB->C#)\n"
            "TopSolidHost.Textures.SetPicture(texDocId, @\"C:\\Textures\\Steel_Color.png\");"
        ],
        "ITextures.SetTextureScale": [
            "// Form1.vb — Set texture physical scale in meters (VB->C#)\n"
            "TopSolidHost.Textures.SetTextureScale(texDocId, 0.1); // 100mm width"
        ],
    }

    for key, snippets in VB_MATERIAL.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_MATERIAL)} Material/Texture creation from VB.NET source.")

    # Manual injection: Batch PDM modification + GetProperties (Retraitement lib RAL)
    VB_BATCH_PDM = {
        "IDocuments.GetProperties": [
            "// Form1.vb — Retraitement lib RAL (VB->C#)\n"
            "// Get list of property strings from a document\n"
            "var props = TopSolidHost.Documents.GetProperties(docId);\n"
            "// Returns List<string> of document properties"
        ],
    }

    for key, snippets in VB_BATCH_PDM.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_BATCH_PDM)} Batch PDM from VB.NET source.")

    # Manual injection: Preview extraction + PDM navigation (Previews Extractor)
    VB_PREVIEW = {
        "IPdm.GetOwner": [
            "// Form1.vb — Previews Extractor (VB->C#)\n"
            "// Get the parent folder of a document in PDM tree\n"
            "PdmObjectId parentFolder = TopSolidHost.Pdm.GetOwner(docPdmId);\n"
            "string folderName = TopSolidHost.Pdm.GetName(parentFolder);"
        ],
        "IPdm.GetMinorRevisionPreviewBitmap": [
            "// Form1.vb — Extract document preview as Bitmap (VB->C#)\n"
            "PdmMinorRevisionId minorRev = TopSolidHost.Documents.GetPdmMinorRevision(docId);\n"
            "Bitmap preview = TopSolidHost.Pdm.GetMinorRevisionPreviewBitmap(minorRev);\n"
            "preview.Save(path, System.Drawing.Imaging.ImageFormat.Png);"
        ],
    }

    for key, snippets in VB_PREVIEW.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_PREVIEW)} Preview/GetOwner from VB.NET source.")

    # Manual injection: Bridge V2 + CSV Extractor (advanced PDM & import/export)
    VB_BRIDGE_CSV = {
        "IPdm.ImportFile": [
            "// Accueil.vb — TopSolidWorks Bridge V2 (VB->C#)\n"
            "// Import a file directly into a project (shortcut, no format selection)\n"
            "PdmObjectId imported = TopSolidHost.Pdm.ImportFile(filePath, projectId, docName);\n"
            "DocumentId importedDocId = TopSolidHost.Documents.GetDocument(imported);"
        ],
        "IPdm.CheckIn": [
            "// Accueil.vb — CheckIn (mise au coffre) programmatique (VB->C#)\n"
            "TopSolidHost.Pdm.CheckIn(pdmId, true); // true = keep local copy"
        ],
        "IPdm.IsDirty": [
            "// Accueil.vb — Check if document has unsaved changes (VB->C#)\n"
            "bool dirty = TopSolidHost.Pdm.IsDirty(pdmId);"
        ],
        "IDocuments.ImportWithOptions": [
            "// Accueil.vb — Import with explicit options (VB->C#)\n"
            "var options = TopSolidHost.Application.GetImporterOptions(importerIndex);\n"
            "var log = new List<string>(); var bad = new List<DocumentId>();\n"
            "var created = TopSolidHost.Documents.ImportWithOptions(importerIndex, options, path, projId, log, bad);"
        ],
        "IPdm.MoveSeveral": [
            "// Accueil.vb — Move multiple PDM objects to a destination (VB->C#)\n"
            "var toMove = new List<PdmObjectId> { obj1, obj2, obj3 };\n"
            "TopSolidHost.Pdm.MoveSeveral(toMove, destinationFolderId);"
        ],
        "IPdm.SetDescription": [
            "// Accueil.vb — Set description directly via PDM (VB->C#)\n"
            "TopSolidHost.Pdm.SetDescription(pdmId, \"Bride de fixation\");"
        ],
        "IPdm.SetPartNumber": [
            "// Accueil.vb — Set part number directly via PDM (VB->C#)\n"
            "TopSolidHost.Pdm.SetPartNumber(pdmId, \"REF-2024-001\");"
        ],
        "IPdm.SetManufacturer": [
            "// Accueil.vb — Set manufacturer directly via PDM (VB->C#)\n"
            "TopSolidHost.Pdm.SetManufacturer(pdmId, \"Bosch\");"
        ],
        "IPdm.SetManufacturerPartNumber": [
            "// Accueil.vb — Set manufacturer part number via PDM (VB->C#)\n"
            "TopSolidHost.Pdm.SetManufacturerPartNumber(pdmId, \"MFR-REF-001\");"
        ],
        "IPdm.GetDescription": [
            "// Start.vb — CSV Extractor: read description via PDM (VB->C#)\n"
            "string desc = TopSolidHost.Pdm.GetDescription(pdmId);"
        ],
        "IPdm.GetPartNumber": [
            "// Start.vb — CSV Extractor: read part number via PDM (VB->C#)\n"
            "string pn = TopSolidHost.Pdm.GetPartNumber(pdmId);"
        ],
        "IPdm.GetManufacturer": [
            "// Start.vb — CSV Extractor: read manufacturer via PDM (VB->C#)\n"
            "string mfr = TopSolidHost.Pdm.GetManufacturer(pdmId);"
        ],
        "IPdm.GetManufacturerPartNumber": [
            "// Start.vb — CSV Extractor: read manufacturer part number (VB->C#)\n"
            "string mfrPn = TopSolidHost.Pdm.GetManufacturerPartNumber(pdmId);"
        ],
    }

    for key, snippets in VB_BRIDGE_CSV.items():
        if key not in examples_index:
            examples_index[key] = []
        examples_index[key].extend(snippets)
    print(f"Manual injection: {len(VB_BRIDGE_CSV)} Bridge V2 + CSV Extractor from VB.NET source.")

    # Inject into edges
    edges_with_examples = 0
    total_examples_added = 0
    for edge in edges:
        iface = edge.get("Interface", "")
        method = edge.get("MethodName", "")
        if not iface or not method:
            continue

        key = f"{iface}.{method}"
        if key in examples_index:
            # Take up to 3 shortest/most focused snippets
            all_snippets = examples_index[key]
            # Deduplicate by content
            unique_snippets = list(dict.fromkeys(all_snippets))
            # Sort by length (shortest = most focused)
            unique_snippets.sort(key=len)
            selected = unique_snippets[:3]

            edge["Examples"] = selected
            edges_with_examples += 1
            total_examples_added += len(selected)

    print(f"Enriched {edges_with_examples} edges with examples ({total_examples_added} total snippets).")

    # Phase 4: Semantic tuning — data-driven weight adjustments and hints
    print("\n--- Phase 4: Semantic tuning ---")

    # Axe 1: Promote methods with real examples (weight → 2)
    # Axe 2: Add guidance hints for the LLM agent
    # Axe 3: Penalize niche methods (weight → 20-30)

    SEMANTIC_RULES = {
        # === AXE 1+2: Core methods (promote + hint) ===

        # PDM Navigation
        "IPdm.GetCurrentProject": {"weight": 1, "hint": "Point d'entree: retourne le projet courant."},
        "IPdm.GetConstituents": {"weight": 1, "hint": "Liste dossiers et documents d'un conteneur PDM (projet ou dossier)."},
        "IPdm.SearchDocumentByName": {"weight": 1, "hint": "Recherche par nom (CONTAINS, pas exact match). Retourne List<PdmObjectId>."},
        "IPdm.SearchObjectsWithProperties": {"weight": 2, "hint": "Recherche avancee par extension et proprietes dans un projet."},
        "IPdm.GetName": {"weight": 1, "hint": "Nom d'un objet PDM (document ou dossier)."},
        "IPdm.GetType": {"weight": 2, "hint": "Retourne PdmObjectType + extension (.TopPrt, .TopAsm, .TopFam...)."},
        "IPdm.GetSelectedPdmObjectIds": {"weight": 2, "hint": "Retourne la selection courante dans l'arbre PDM."},
        "IPdm.CreateDocument": {"weight": 2, "hint": "Cree un document dans un projet. Extension obligatoire (.TopPrt, .TopFam...)."},
        "IPdm.SetName": {"weight": 2, "hint": "NOM du document (colonne Nom dans TopSolid). Pas la designation. renommer, nom"},
        "IPdm.GetName": {"weight": 1, "hint": "Lit le NOM d'un objet PDM. Pas la designation. nom"},
        "IPdm.GetDescription": {"weight": 2, "hint": "Lit la DESIGNATION du document (colonne Designation dans TopSolid). designation, description"},
        "IPdm.SetDescription": {"weight": 2, "hint": "Modifie la DESIGNATION du document (colonne Designation dans TopSolid). designation, description"},
        "IPdm.GetPartNumber": {"weight": 2, "hint": "Lit la REFERENCE du document (colonne Reference dans TopSolid). reference, part number, numero de piece"},
        "IPdm.SetPartNumber": {"weight": 2, "hint": "Modifie la REFERENCE du document (colonne Reference dans TopSolid). reference, part number"},
        "IPdm.GetManufacturer": {"weight": 2, "hint": "Lit le FABRICANT du document. fabricant, manufacturer, fournisseur"},
        "IPdm.SetManufacturer": {"weight": 2, "hint": "Modifie le FABRICANT du document. fabricant, manufacturer, fournisseur"},
        "IPdm.GetManufacturerPartNumber": {"weight": 2, "hint": "Lit la REFERENCE FABRICANT du document. reference fabricant, manufacturer part number"},
        "IPdm.SetManufacturerPartNumber": {"weight": 2, "hint": "Modifie la REFERENCE FABRICANT. reference fabricant, manufacturer part number"},
        "IPdm.Save": {"weight": 2, "hint": "Sauvegarde un document. OBLIGATOIRE apres EndModification. sauvegarder, enregistrer"},
        "IPdm.CheckIn": {"weight": 2, "hint": "MISE AU COFFRE du document (archive la revision). mise au coffre, check-in"},
        "IPdm.CheckOut": {"weight": 2, "hint": "SORTI DE COFFRE du document (reserve pour modification). sorti de coffre, check-out"},
        "IPdm.ExportPackage": {"weight": 3, "hint": "Exporte un package TopSolid (.TopPkg)."},
        "IPdm.ExportViewerPackage": {"weight": 3, "hint": "Exporte un package viewer (.TopPkgViw)."},
        "IPdm.CreateDocument": {"weight": 2, "hint": "Cree un document dans un projet. Extension: .TopPrt=piece, .TopAsm=assemblage, .TopFam=famille. creer piece, nouveau document"},
        "IPdm.CreateProjectFolder": {"weight": 2, "hint": "Cree un dossier dans un projet. creer dossier"},

        # Documents
        "IDocuments.get_EditedDocument": {"weight": 1, "hint": "Point d'entree: retourne le DocumentId du document en cours d'edition."},
        "IDocuments.GetDocument": {"weight": 1, "hint": "Convertit PdmObjectId → DocumentId."},
        "IDocuments.GetName": {"weight": 1, "hint": "Nom du document (pas le nom PDM)."},
        "IDocuments.GetTypeFullName": {"weight": 2, "hint": "Type complet du document (ex: ...PartDocument, ...AssemblyDocument)."},
        "IDocuments.GetPdmObject": {"weight": 2, "hint": "Convertit DocumentId → PdmObjectId (inverse de GetDocument)."},
        "IDocuments.EnsureIsDirty": {"weight": 1, "hint": "Passer le document EN MODIFICATION. OBLIGATOIRE avant toute ecriture. ATTENTION: docId CHANGE (ref). Chercher les elements APRES. en modification"},
        "IDocuments.Export": {"weight": 2, "hint": "Exporte un document. Trouver l'index exporteur via Application.ExporterCount + GetExporterFileType."},
        "IDocuments.ExportWithOptions": {"weight": 2, "hint": "Exporte avec options (KeyValue list). Ex: REPRESENTATION_ID pour FBX."},
        "IDocuments.Import": {"weight": 2, "hint": "Importe un fichier dans un projet. Retourne List<DocumentId> des documents importes."},
        "IDocuments.ImportWithOptions": {"weight": 2, "hint": "Importe avec options template (TEMPLATE_EXTENSION_1, TEMPLATE_ID_1...)."},
        "IDocuments.CanExport": {"weight": 3, "hint": "Verifie si un format d'export est supporte pour ce document."},
        "IDocuments.Open": {"weight": 3, "hint": "Ouvre un document (ref DocumentId). Necessaire avant d'editer un document ferme."},
        "IDocuments.Close": {"weight": 3, "hint": "Ferme un document."},
        "IDocuments.Rebuild": {"weight": 3, "hint": "Reconstruit le document (recalcul complet)."},
        "IDocuments.Refresh": {"weight": 3, "hint": "Rafraichit l'affichage du document."},

        # Elements
        "IElements.GetElements": {"weight": 1, "hint": "Liste tous les elements d'un document (parametres, shapes, operations...)."},
        "IElements.GetConstituents": {"weight": 1, "hint": "Liste les sous-elements d'un element (ex: publishings d'une fonction)."},
        "IElements.GetName": {"weight": 1, "hint": "Nom interne d'un element."},
        "IElements.GetFriendlyName": {"weight": 1, "hint": "Nom affiche d'un element (plus lisible que GetName)."},
        "IElements.GetTypeFullName": {"weight": 1, "hint": "Type complet d'un element (pour filtrage)."},
        "IElements.GetProperties": {"weight": 2, "hint": "Retourne toutes les proprietes d'un element."},
        "IElements.SetName": {"weight": 2, "hint": "Renomme un element."},
        "IElements.HasSystemName": {"weight": 3, "hint": "True si c'est un element systeme (non modifiable)."},
        "IElements.GetParent": {"weight": 2, "hint": "Retourne l'operation parente d'un element."},
        "IElements.GetOwner": {"weight": 2, "hint": "Retourne le proprietaire d'un element."},
        "IElements.Delete": {"weight": 3, "hint": "Supprime un element (operation, parametre...)."},
        "IElements.GetColor": {"weight": 2, "hint": "Lit la COULEUR d'attribut d'un element (clic droit → attribut → couleur). couleur, color, attribut"},
        "IElements.SetColor": {"weight": 2, "hint": "Change la COULEUR d'attribut d'un element. Methode principale pour colorer une forme. couleur, color, attribut"},
        "IElements.HasColor": {"weight": 2, "hint": "Verifie si l'element a une couleur d'attribut."},
        "IElements.IsColorModifiable": {"weight": 3, "hint": "Verifie si la couleur de l'element est modifiable."},
        "IElements.GetTransparency": {"weight": 2, "hint": "Lit la TRANSPARENCE d'un element (0=opaque, 1=transparent). transparence, opacite"},
        "IElements.SetTransparency": {"weight": 2, "hint": "Change la TRANSPARENCE d'un element. transparence, opacite"},
        "IElements.HasTransparency": {"weight": 2, "hint": "Verifie si l'element a une transparence."},
        "IElements.IsVisible": {"weight": 2, "hint": "Verifie si l'element est visible. visibilite, cacher, montrer"},

        # Layers
        "ILayers.GetLayers": {"weight": 2, "hint": "Liste les calques du document. calque, layer"},
        "ILayers.SetLayer": {"weight": 2, "hint": "Affecte un element a un calque. calque, layer"},
        "ILayers.GetLayer": {"weight": 2, "hint": "Retourne le calque d'un element. calque, layer"},
        "ILayers.CreateLayer": {"weight": 2, "hint": "Cree un nouveau calque. calque, layer"},

        # Parameters
        "IParameters.GetParameters": {"weight": 1, "hint": "Liste tous les parametres d'un document. CHERCHER APRES EnsureIsDirty."},
        "IParameters.GetParameterType": {"weight": 2, "hint": "Retourne ParameterType (Real, Integer, Boolean, Text, DateTime, Enumeration, Code...)."},
        "IParameters.GetRealValue": {"weight": 2, "hint": "Valeur reelle en SI (metres, radians). 50mm = 0.05."},
        "IParameters.SetRealValue": {"weight": 2, "hint": "Definit une valeur reelle en SI."},
        "IParameters.GetTextValue": {"weight": 2, "hint": "Valeur texte d'un parametre."},
        "IParameters.SetTextValue": {"weight": 2, "hint": "Definit la valeur texte."},
        "IParameters.GetIntegerValue": {"weight": 2},
        "IParameters.SetIntegerValue": {"weight": 2},
        "IParameters.GetBooleanValue": {"weight": 2},
        "IParameters.SetBooleanValue": {"weight": 2},
        "IParameters.GetEnumerationText": {"weight": 2},
        "IParameters.SetEnumerationValue": {"weight": 2},
        "IParameters.GetCodeValue": {"weight": 2},
        "IParameters.CreateRealParameter": {"weight": 2, "hint": "Cree un parametre reel. UnitType obligatoire (Length, Angle, Mass...)."},
        "IParameters.IsTextParameterized": {"weight": 3, "hint": "True si le texte est parametrise (formule)."},
        "IParameters.GetDescriptionParameter": {"weight": 2, "hint": "Retourne l'ElementId du parametre DESIGNATION du document. designation, description"},
        "IParameters.GetPartNumberParameter": {"weight": 2, "hint": "Retourne l'ElementId du parametre REFERENCE du document. reference, part number"},
        "IParameters.GetManufacturerParameter": {"weight": 2, "hint": "Retourne l'ElementId du parametre FABRICANT du document. fabricant, manufacturer"},
        "IParameters.GetManufacturerPartNumberParameter": {"weight": 2, "hint": "Retourne l'ElementId du parametre REFERENCE FABRICANT. reference fabricant"},
        "IParameters.GetComplementaryPartNumberParameter": {"weight": 2, "hint": "Retourne l'ElementId du parametre REFERENCE COMPLEMENTAIRE. reference complementaire"},

        # User Properties (proprietes utilisateur — filtrage nomenclatures, type production, etc.)
        "IParameters.SearchUserPropertyParameter": {"weight": 2, "hint": "Cherche un parametre de propriete utilisateur dans un document. propriete utilisateur, custom, filtre nomenclature"},
        "IParameters.CreateUserPropertyParameter": {"weight": 2, "hint": "Cree un parametre de propriete utilisateur. propriete utilisateur, custom"},
        "IParameters.GetUserPropertyDefinition": {"weight": 2, "hint": "Retourne la definition (DocumentId) d'une propriete utilisateur. propriete utilisateur"},
        "IPdm.GetTextUserProperty": {"weight": 2, "hint": "Lit la valeur texte d'une propriete utilisateur PDM. propriete utilisateur, custom"},
        "IPdm.SetTextUserProperty": {"weight": 2, "hint": "Ecrit la valeur texte d'une propriete utilisateur PDM. propriete utilisateur, custom"},
        "IPdm.GetRealUserProperty": {"weight": 2, "hint": "Lit la valeur reelle d'une propriete utilisateur PDM. propriete utilisateur, custom"},
        "IPdm.SetRealUserProperty": {"weight": 2, "hint": "Ecrit la valeur reelle d'une propriete utilisateur PDM. propriete utilisateur, custom"},

        # Application
        "IApplication.StartModification": {"weight": 1, "hint": "OBLIGATOIRE: demarre une transaction de modification. Toujours avec EndModification."},
        "IApplication.EndModification": {"weight": 1, "hint": "Termine la transaction. (true,true)=valider, (false,false)=annuler."},
        "IApplication.ExporterCount": {"weight": 3, "hint": "Nombre d'exporteurs disponibles. Boucler de 0 a ExporterCount-1."},
        "IApplication.GetExporterFileType": {"weight": 3, "hint": "Retourne le type et les extensions d'un exporteur par index."},
        "IApplication.ImporterCount": {"weight": 3},
        "IApplication.GetImporterFileType": {"weight": 3},
        "IApplication.GetExporterOptions": {"weight": 3, "hint": "Retourne les options par defaut d'un exporteur (List<KeyValue>)."},
        "IApplication.GetImporterOptions": {"weight": 3},

        # Families
        "IFamilies.IsFamily": {"weight": 2, "hint": "True si le document est une famille."},
        "IFamilies.IsExplicit": {"weight": 2, "hint": "True si famille explicite (instances nommees)."},
        "IFamilies.GetExplicitInstances": {"weight": 2, "hint": "Retourne codes + PdmObjectId des instances explicites."},
        "IFamilies.GetCodes": {"weight": 2, "hint": "Liste les codes de la famille."},
        "IFamilies.GetGenericDocument": {"weight": 2, "hint": "Retourne le document generique de la famille."},
        "IFamilies.SetAsExplicit": {"weight": 3, "hint": "Convertit un document en famille explicite."},
        "IFamilies.AddExplicitInstance": {"weight": 3, "hint": "Ajoute une instance explicite (code + DocumentId)."},
        "IFamilies.AddCatalogColumn": {"weight": 3, "hint": "Ajoute une colonne au catalogue (prend ElementId du parametre)."},
        "IFamilies.GetCatalogColumnParameters": {"weight": 3, "hint": "Liste les parametres colonnes du catalogue."},
        "IFamilies.GetDriverCondition": {"weight": 3, "hint": "Condition sur un pilote (SmartBoolean ou null)."},

        # Assemblies
        "IAssemblies.IsAssembly": {"weight": 2, "hint": "True si le document est un assemblage."},
        "IAssemblies.IsInclusion": {"weight": 2, "hint": "True si l'element est une operation d'inclusion."},
        "IAssemblies.CreateInclusion": {"weight": 2, "hint": "Cree une inclusion. Pilotes = (driverNames, driverValues SmartObject). Transform3D.Identity pour pas de placement."},
        "IAssemblies.CreateInclusion2": {"weight": 2, "hint": "Version etendue de CreateInclusion avec drivers de design (SmartDesignObject)."},
        "IAssemblies.GetInclusionCodeAndDrivers": {"weight": 2, "hint": "Lit le code et les pilotes d'une inclusion de famille."},
        "IAssemblies.SetInclusionCodeAndDrivers": {"weight": 3, "hint": "Modifie le code et les pilotes d'une inclusion."},
        "IAssemblies.GetInclusionDefinitionDocument": {"weight": 2, "hint": "Retourne le document de definition d'une inclusion."},
        "IAssemblies.GetOccurrenceDefinition": {"weight": 2, "hint": "Retourne le document de definition d'une occurrence."},
        "IAssemblies.GetParts": {"weight": 2, "hint": "Liste les pieces/assemblages du dossier Parts."},
        "IAssemblies.CreateFrameOnFrameConstraint": {"weight": 3, "hint": "Contrainte repere-sur-repere. SmartFrame3D source et destination."},
        "IAssemblies.SetCollisionsManagement": {"weight": 3, "hint": "Active/desactive la detection de collisions."},

        # Geometries 3D
        "IGeometries3D.GetAbsoluteFrame": {"weight": 2, "hint": "Retourne le repere absolu du document."},
        "IGeometries3D.GetAbsoluteOriginPoint": {"weight": 2, "hint": "Point d'origine absolu du document."},
        "IGeometries3D.GetAbsoluteXYPlane": {"weight": 2, "hint": "Plan XY absolu."},
        "IGeometries3D.GetAbsoluteXZPlane": {"weight": 2},
        "IGeometries3D.GetAbsoluteYZPlane": {"weight": 2},
        "IGeometries3D.GetAbsoluteXAxis": {"weight": 2},
        "IGeometries3D.GetAbsoluteYAxis": {"weight": 2},
        "IGeometries3D.GetAbsoluteZAxis": {"weight": 2},
        "IGeometries3D.GetPoints": {"weight": 2, "hint": "Liste les points 3D du document."},
        "IGeometries3D.GetFrames": {"weight": 2, "hint": "Liste les reperes 3D du document."},
        "IGeometries3D.GetPlanes": {"weight": 2, "hint": "Liste les plans 3D du document."},
        "IGeometries3D.GetAxes": {"weight": 2},
        "IGeometries3D.CreatePoint": {"weight": 2, "hint": "Cree un point 3D. Coordonnees en metres SI."},
        "IGeometries3D.CreateFrame": {"weight": 2, "hint": "Cree un repere par SmartFrame3D."},
        "IGeometries3D.CreateFrameByPointAndTwoDirections": {"weight": 2, "hint": "Cree un repere par point + 2 directions. isSecondDirectionOY=true si 2e dir = axe Y."},
        "IGeometries3D.CreateFrameWithOffset": {"weight": 2, "hint": "Cree un repere decale. Offset en metres SI."},
        "IGeometries3D.GetPointGeometry": {"weight": 2, "hint": "Retourne Point3D d'un element point."},
        "IGeometries3D.GetFrameGeometry": {"weight": 2, "hint": "Retourne Frame3D d'un element repere."},
        "IGeometries3D.GetPlaneGeometry": {"weight": 2, "hint": "Retourne Plane3D d'un element plan."},

        # Sketches
        "ISketches2D.GetSketches": {"weight": 2, "hint": "Liste les esquisses du document."},
        "ISketches2D.CreateSketchIn3D": {"weight": 2, "hint": "Cree une esquisse 3D sur un plan."},
        "ISketches2D.StartModification": {"weight": 2, "hint": "Demarre l'edition d'une esquisse. DISTINCT de Application.StartModification."},
        "ISketches2D.EndModification": {"weight": 2, "hint": "Termine l'edition de l'esquisse."},
        "ISketches2D.CreateVertex": {"weight": 3, "hint": "Cree un sommet 2D (Point2D en metres SI)."},
        "ISketches2D.CreateLineSegment": {"weight": 3, "hint": "Cree un segment de ligne entre deux sommets."},
        "ISketches2D.CreateProfile": {"weight": 3, "hint": "Cree un profil ferme a partir de segments."},
        "ISketches2D.GetProfiles": {"weight": 3},
        "ISketches2D.GetPlane": {"weight": 3, "hint": "Retourne le plan de definition de l'esquisse (Plane3D)."},

        # Shapes
        "IShapes.GetShapes": {"weight": 2, "hint": "Liste les shapes du document."},
        "IShapes.GetFaces": {"weight": 2, "hint": "Liste les faces d'un shape (ElementItemId)."},
        "IShapes.GetFaceCount": {"weight": 2},
        "IShapes.GetEdgeCount": {"weight": 2},
        "IShapes.GetFaceArea": {"weight": 3, "hint": "Aire d'une face en m2."},
        "IShapes.GetFaceSurfaceType": {"weight": 3, "hint": "Type de surface (Plane, Cylinder, Sphere, Cone, Torus, BSpline...)."},
        "IShapes.GetFaceColor": {"weight": 3},
        "IShapes.CreateExtrudedShape": {"weight": 2, "hint": "Cree une extrusion depuis un profil d'esquisse."},

        # Operations
        "IOperations.GetOperations": {"weight": 2, "hint": "Liste les operations du document (inclusions, esquisses, shapes...)."},
        "IOperations.SetInsertionOperation": {"weight": 3, "hint": "Definit le point d'insertion pour les nouvelles operations."},
        "IOperations.ResetInsertionOperation": {"weight": 3, "hint": "Reinitialise le point d'insertion a la fin."},

        # Entities
        "IEntities.GetFunctions": {"weight": 2, "hint": "Liste les fonctions du document."},
        "IEntities.GetFunctionPublishings": {"weight": 2, "hint": "Liste les publishings d'une fonction."},
        "IEntities.GetFunctionDefinition": {"weight": 3, "hint": "Retourne la definition de la fonction."},
        "IEntities.Transform": {"weight": 2, "hint": "Applique une transformation 3D a un element. Transform3D.CreateTranslation/CreateRotation."},
        "IEntities.ProvideFunction": {"weight": 3, "hint": "Fournit (insere) une fonction depuis un autre document."},

        # User interaction
        "IUser.AskShape": {"weight": 2, "hint": "Demande a l'utilisateur de selectionner une forme 3D."},
        "IUser.AskFace": {"weight": 2, "hint": "Demande a l'utilisateur de selectionner une face."},
        "IUser.AskPoint3D": {"weight": 2, "hint": "Demande a l'utilisateur de selectionner un point 3D."},

        # Representations
        "IRepresentations.GetRepresentations": {"weight": 3, "hint": "Liste les representations du document."},
        "IRepresentations.GetRepresentationConstituents": {"weight": 3},
        "IRepresentations.AddRepresentationConstituent": {"weight": 3},

        # === AXE 3: Penalize niche methods ===

        # Visualization (camera, preview) — rarement utile pour l'automatisation
        "IVisualization3D.*": {"weight": 20},

        # Mechanisms (joints, simulations) — tres specialise
        "IMechanisms.*": {"weight": 25},

        # Simulations — tres specialise
        "ISimulations.*": {"weight": 25},

        # Dimensions (cotation avancee) — drafting uniquement
        "IDimensions.*": {"weight": 20},

        # Healing (reparation shapes) — rare
        "IHealing.*": {"weight": 20},

        # Annotations — drafting
        "IAnnotations.*": {"weight": 20},

        # SurfaceFinish — tres specialise
        "ISurfaceFinish.*": {"weight": 25},
    }

    # Apply rules
    hint_before = sum(1 for e in edges if e.get("SemanticHint"))
    weight_changed = 0
    hint_added = 0

    for edge in edges:
        iface = edge.get("Interface", "")
        method = edge.get("MethodName", "")
        if not iface or not method:
            continue

        key = f"{iface}.{method}"
        rule = SEMANTIC_RULES.get(key)

        # Check wildcard rules (Interface.*)
        if not rule:
            wildcard_key = f"{iface}.*"
            rule = SEMANTIC_RULES.get(wildcard_key)

        if not rule:
            continue

        # Apply weight
        if "weight" in rule and edge.get("Weight", 10) == 10:
            # Only change default-weight edges (don't override already-tuned ones)
            edge["Weight"] = rule["weight"]
            weight_changed += 1

        # Apply hint (explicit rules ALWAYS overwrite — they are hand-curated)
        if "hint" in rule:
            edge["SemanticHint"] = rule["hint"]
            hint_added += 1

    hint_after = sum(1 for e in edges if e.get("SemanticHint"))
    print(f"Weight adjusted: {weight_changed} edges")
    print(f"Hints added: {hint_added} (total: {hint_before} -> {hint_after})")

    # Weight distribution after tuning
    from collections import Counter
    weight_dist = Counter(e.get("Weight", 10) for e in edges)
    print("Weight distribution after tuning:")
    for w, c in sorted(weight_dist.items()):
        print(f"  Weight {w}: {c} edges")

    # Phase 5: Auto-generate SemanticHint from Description + MethodName (M-56)
    # For every edge that has a Description but no SemanticHint, derive keywords
    print("\n--- Phase 5: Auto-generate SemanticHint from Description ---")

    # English stop words to strip from descriptions
    STOP_WORDS = {
        "a", "an", "the", "of", "or", "and", "in", "to", "for", "from", "by",
        "is", "are", "was", "were", "be", "been", "being",
        "gets", "sets", "get", "set", "returns", "return",
        "this", "that", "these", "those", "it", "its",
        "has", "have", "had", "having",
        "does", "do", "did", "done",
        "can", "could", "will", "would", "shall", "should", "may", "might",
        "with", "without", "whether", "if", "not", "no", "on", "at", "as",
        "all", "each", "every", "any", "some", "such",
        "new", "given", "specified", "particular", "current",
        "inelementid", "indocumentid", "inpdmobjectid", "inprojectid",
        "inelement", "out", "ref", "null",
    }

    # French translation dict for common CAD/PDM/engineering terms
    # These help the LLM agent match French user queries to English API methods
    FR_TRANSLATIONS = {
        # Geometry / Shapes
        "point": "point", "line": "ligne", "plane": "plan", "axis": "axe",
        "frame": "repere", "circle": "cercle", "arc": "arc",
        "surface": "surface", "face": "face", "edge": "arete",
        "vertex": "sommet, vertex", "curve": "courbe",
        "volume": "volume", "area": "aire, surface",
        "length": "longueur", "angle": "angle", "radius": "rayon",
        "diameter": "diametre", "thickness": "epaisseur",
        "distance": "distance", "offset": "decalage, offset",
        "direction": "direction", "normal": "normale",
        "origin": "origine", "position": "position",
        "translation": "translation, deplacement",
        "rotation": "rotation",
        "transformation": "transformation",
        "geometry": "geometrie",
        "coordinate": "coordonnee",
        "bounding": "englobant",
        "enclosing": "englobant",
        # Shapes / Operations (termes TopSolid valides par Julien 2026-04-09)
        "shape": "forme, shape", "solid": "solide",
        "shell": "coque", "sheet": "tole, feuille",
        "extrusion": "extrusion, extrude",
        "revolution": "revolution, piece tournee",
        "fillet": "conge", "chamfer": "chanfrein",
        "hole": "trou, percage", "drilling": "percage, trou",
        "thread": "filetage", "tap": "taraudage",
        "pocket": "poche", "boss": "bossage",
        "pattern": "motif",
        "mirror": "symetrie, miroir",
        "boolean": "booleen, operation booleenne",
        "union": "union", "subtract": "soustraction",
        "intersection": "intersection",
        "split": "decoupe, separation",
        "trim": "ajuster, rogner",
        "blend": "raccord",
        "sweep": "balayage",
        "loft": "lissage, gabarit",
        "lofted": "lissage, gabarit",
        # Sketch
        "sketch": "esquisse",
        "profile": "profil, section, contour",
        "segment": "segment", "constraint": "contrainte",
        "dimension": "cote, dimension",
        # Assembly
        "assembly": "assemblage", "inclusion": "inclusion",
        "occurrence": "occurrence, instance",
        "component": "composant",
        "positioning": "positionnement",
        "collision": "collision",
        # PDM (termes TopSolid valides par Julien 2026-04-09)
        "project": "projet", "folder": "dossier",
        "document": "document", "file": "fichier",
        "revision": "revision", "version": "version",
        "checkin": "mise au coffre",
        "checkout": "sorti de coffre",
        "lifecycle": "cycle de vie, etat",
        "state": "etat", "status": "statut",
        "name": "nom", "rename": "renommer",
        "delete": "supprimer", "remove": "supprimer",
        "create": "creer", "add": "ajouter",
        "copy": "copier", "move": "deplacer",
        "search": "rechercher, chercher",
        "import": "importer", "export": "exporter",
        "save": "sauvegarder, enregistrer",
        "open": "ouvrir", "close": "fermer",
        "property": "propriete", "properties": "proprietes",
        "owner": "proprietaire, auteur",
        "constituent": "constituant, contenu",
        "reference": "reference, numero de piece, part number",
        "back-reference": "cas d'emploi, where-used",
        # PDM metadata (colonnes TopSolid)
        "description": "designation, description",
        "partnumber": "reference, numero de piece",
        "manufacturer": "fabricant, fournisseur",
        "minor": "revision mineure",
        "major": "revision majeure",
        # Parameters
        "parameter": "parametre", "parameters": "parametres",
        "value": "valeur", "unit": "unite, type d'unite",
        "real": "reel", "integer": "entier",
        "text": "texte",
        "enumeration": "enumeration",
        "formula": "formule",
        "driver": "pilote",
        "code": "code",
        "smart": "formule, parametrise",
        # Family
        "family": "famille", "catalog": "catalogue",
        "instance": "instance", "generic": "generique",
        "explicit": "explicite",
        "column": "colonne",
        # Material / Coating (termes TopSolid)
        "material": "matiere, materiau",
        "coating": "revetement, peinture, traitement",
        "texture": "texture",
        "color": "couleur",
        "finish": "finition",
        "layer": "couche, calque",
        # Drafting / BOM (termes TopSolid valides par Julien 2026-04-09)
        "drawing": "plan, mise en plan, liasse",
        "drafting": "mise en plan, plan, draft, liasse de plans",
        "view": "vue, vue principale, vue auxiliaire",
        "projection": "ensemble de projection, projection",
        "table": "tableau, nomenclature, liste de debit",
        "bom": "nomenclature, fiche, rafale",
        "batch": "rafale",
        "cell": "cellule",
        "row": "ligne, rangee",
        "annotation": "annotation, cotation, cote 3D, cote automatique",
        "tolerance": "tolerance",
        "template": "modele",
        "model": "modele",
        "layout": "disposition, esquisse de disposition",
        # Operations / Entites (termes TopSolid)
        "operation": "operation",
        "function": "fonction",
        "entity": "entite",
        "feature": "feature",
        "publishing": "publication, publishing",
        "working": "courant, etape courante",
        "stage": "etape, etape courante",
        "dirty": "en modification",
        "modification": "modification, en modification",
        "rebuild": "reconstruire, recalculer",
        "refresh": "rafraichir",
        # Simulation / Mechanism
        "simulation": "simulation",
        "mechanism": "mecanisme",
        "joint": "liaison",
        "motion": "mouvement",
        # Parts (termes TopSolid)
        "part": "piece", "stock": "brut",
        "unfolding": "mise a plat, depliage",
        "bend": "pliage, pli",
        "substitution": "substitution",
        "representation": "representation",
        # Misc
        "bitmap": "image, apercu",
        "preview": "apercu, previsualisation",
        "selection": "selection",
        "count": "nombre, quantite",
        "list": "liste, lister",
        "type": "type",
        "index": "index",
        "identifier": "identifiant",
    }

    def generate_hint_from_description(description, method_name):
        """Auto-generate a SemanticHint from Description + MethodName."""
        if not description:
            return None

        # Combine description + CamelCase-split method name
        desc_lower = description.lower()
        method_words = re.findall(r'[A-Z][a-z]+|[a-z]+', method_name)

        # Extract meaningful words from description
        desc_words = re.findall(r'[a-z]+', desc_lower)
        all_words = set(w for w in desc_words + [w.lower() for w in method_words] if len(w) > 2 and w not in STOP_WORDS)

        # Build hint: English keywords + French translations
        hint_parts = set()
        for word in all_words:
            # Add French translation if available
            if word in FR_TRANSLATIONS:
                hint_parts.add(FR_TRANSLATIONS[word])
            # Also check plural → singular
            elif word.endswith('s') and word[:-1] in FR_TRANSLATIONS:
                hint_parts.add(FR_TRANSLATIONS[word[:-1]])

        if not hint_parts:
            return None

        return ", ".join(sorted(hint_parts))

    hint_before_p5 = sum(1 for e in edges if e.get("SemanticHint"))
    hints_generated = 0

    for edge in edges:
        # Skip edges that already have a hint (from Phase 4 manual rules)
        if edge.get("SemanticHint"):
            continue

        desc = edge.get("Description")
        method = edge.get("MethodName", "")
        if not desc:
            continue

        hint = generate_hint_from_description(desc, method)
        if hint:
            edge["SemanticHint"] = hint
            hints_generated += 1

    hint_after_p5 = sum(1 for e in edges if e.get("SemanticHint"))
    print(f"Auto-generated {hints_generated} hints (total: {hint_before_p5} -> {hint_after_p5}, {100*hint_after_p5//len(edges)}%)")

    # Deduplicate edges
    unique_edges = []
    seen_edges = set()
    for e in edges:
        # Create a stable key for the edge
        edge_key = (
            e.get("Source", {}).get("TypeName"),
            e.get("Target", {}).get("TypeName"),
            e.get("MethodName"),
            e.get("MethodSignature")
        )
        if edge_key not in seen_edges:
            unique_edges.append(e)
            seen_edges.add(edge_key)
    
    graph_data["_edges"] = unique_edges
    print(f"Deduplicated edges: {len(edges)} -> {len(unique_edges)}")

    print(f"\nSaving enriched graph to {graph_path}...")
    with open(graph_path, 'w', encoding='utf-8') as f:
        json.dump(graph_data, f, indent=4)
        
    print("Done!")

if __name__ == "__main__":
    enrich_graph()
