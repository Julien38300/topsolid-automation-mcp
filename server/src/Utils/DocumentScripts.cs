using System.Text;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// C# 5 scripts that topsolid_compare_documents runs inside TopSolid. They only read, except the
    /// apply script built by <see cref="DocumentComparison.BuildApplyScript"/>.
    /// </summary>
    public static class DocumentScripts
    {
        /// <summary>
        /// Declares <c>__find(name)</c>: the document of that exact name in the current project
        /// (SearchDocumentByName matches by "contains"), or the edited document for an empty name.
        /// </summary>
        public const string FindDocumentFunction =
@"Func<string, DocumentId> __find = n =>
{
    if (string.IsNullOrEmpty(n)) return TopSolidHost.Documents.EditedDocument;
    PdmObjectId __proj = TopSolidHost.Pdm.GetCurrentProject();
    foreach (var r in TopSolidHost.Pdm.SearchDocumentByName(__proj, n))
        if (TopSolidHost.Pdm.GetName(r) == n) return TopSolidHost.Documents.GetDocument(r);
    return DocumentId.Empty;
};";

        /// <summary>Most faces read per document, to keep a large part within the script timeout.</summary>
        public const int MaxFaces = 20000;

        /// <summary>C# literal of a string, for code generation.</summary>
        public static string Literal(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>
        /// Reads one revision of a document in the format of <see cref="DocumentSnapshot"/>.
        /// <paramref name="revision"/>: empty = current, "B.0" = that minor revision, "B" = the last minor
        /// revision of major B, "previous" = the revision before the current one.
        /// </summary>
        public static string Snapshot(string document, string revision)
        {
            return FindDocumentFunction + "\n" + SnapshotBody
                .Replace("__DOCUMENT__", Literal(document))
                .Replace("__REVISION__", Literal(revision ?? ""))
                .Replace("__MAXFACES__", MaxFaces.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private const string SnapshotBody =
@"var inv = System.Globalization.CultureInfo.InvariantCulture;
Func<string, string> esc = s => s == null ? """" : s.Replace(""\\"", ""\\\\"").Replace(""\t"", ""\\t"").Replace(""\r"", """").Replace(""\n"", ""\\n"");
string wanted = __REVISION__;
DocumentId doc = __find(__DOCUMENT__);
if (doc.IsEmpty) return ""ERR\t"" + esc(""document not found in the current project: "" + __DOCUMENT__);
PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(doc);
string docName = TopSolidHost.Pdm.GetName(pdm);
var labels = new List<string>();
var minors = new List<PdmMinorRevisionId>();
foreach (var maj in TopSolidHost.Pdm.GetMajorRevisions(pdm))
{
    var mins = TopSolidHost.Pdm.GetMinorRevisions(maj);
    foreach (var min in mins)
    {
        labels.Add(TopSolidHost.Pdm.GetMajorRevisionText(maj) + ""."" + TopSolidHost.Pdm.GetMinorRevisionText(min));
        minors.Add(min);
    }
}
string label = labels.Count > 0 ? labels[labels.Count - 1] : """";
if (wanted.Length > 0)
{
    int found = -1;
    if (wanted == ""previous"") found = labels.Count - 2;
    else
    {
        for (int i = 0; i < labels.Count; i++)
            if (labels[i] == wanted || labels[i].StartsWith(wanted + ""."", StringComparison.Ordinal)) found = i;
    }
    if (found < 0) return ""ERR\t"" + esc(""revision '"" + wanted + ""' not found for "" + docName + "". Revisions: "" + string.Join("", "", labels.ToArray()));
    label = labels[found];
    if (found < labels.Count - 1)
    {
        doc = TopSolidHost.Documents.GetMinorRevisionDocument(minors[found]);
        if (doc.IsEmpty) return ""ERR\t"" + esc(""revision "" + label + "" of "" + docName + "" is not a TopSolid document"");
    }
}
var sb = new System.Text.StringBuilder();
sb.AppendLine(""SNAP\t"" + esc(docName) + ""\t"" + esc(label));
sb.AppendLine(""REVS\t"" + esc(string.Join("","", labels.ToArray())));
try
{
    DocumentId mat = TopSolidDesignHost.Parts.GetMaterial(doc);
    sb.AppendLine(""MAT\t"" + (mat.IsEmpty ? """" : esc(TopSolidHost.Pdm.GetName(TopSolidHost.Documents.GetPdmObject(mat)))));
}
catch (Exception ex) { sb.AppendLine(""ERR\t"" + esc(""material: "" + ex.Message)); }
foreach (string p in TopSolidHost.Documents.GetProperties(doc))
{
    string v;
    try
    {
        string t = TopSolidHost.Documents.GetPropertyType(doc, p).ToString();
        if (t == ""Text"") v = TopSolidHost.Documents.GetPropertyTextValue(doc, p);
        else if (t == ""Real"") v = TopSolidHost.Documents.GetPropertyRealValue(doc, p).ToString(""R"", inv);
        else if (t == ""Integer"") v = TopSolidHost.Documents.GetPropertyIntegerValue(doc, p).ToString(inv);
        else if (t == ""Boolean"") v = TopSolidHost.Documents.GetPropertyBooleanValue(doc, p).ToString();
        else continue;
    }
    catch { continue; }
    sb.AppendLine(""PROP\t"" + esc(p) + ""\t"" + esc(v));
}
foreach (var p in TopSolidHost.Parameters.GetParameters(doc))
{
    string kind = """", value = """", ut = """", us = """";
    try
    {
        var t = TopSolidHost.Parameters.GetParameterType(p);
        kind = t.ToString();
        if (t == ParameterType.Real)
        {
            UnitType u; string sym;
            TopSolidHost.Parameters.GetRealUnit(p, out u, out sym);
            ut = u.ToString(); us = sym;
            value = TopSolidHost.Parameters.GetRealValue(p).ToString(""R"", inv);
        }
        else if (t == ParameterType.Text) value = TopSolidHost.Parameters.GetTextValue(p);
        else if (t == ParameterType.Integer) value = TopSolidHost.Parameters.GetIntegerValue(p).ToString(inv);
        else if (t == ParameterType.Boolean) value = TopSolidHost.Parameters.GetBooleanValue(p).ToString();
        else if (t == ParameterType.Enumeration) value = TopSolidHost.Parameters.GetEnumerationValue(p).ToString(inv);
    }
    catch (Exception ex) { value = ""ERR "" + ex.GetType().Name; }
    sb.AppendLine(""PARAM\t"" + p.Id + ""\t"" + esc(TopSolidHost.Elements.GetName(p)) + ""\t"" + esc(TopSolidHost.Elements.GetFriendlyName(p)) + ""\t"" + kind + ""\t"" + esc(value) + ""\t"" + ut + ""\t"" + esc(us));
}
int index = 0;
foreach (var op in TopSolidHost.Operations.GetOperations(doc))
{
    string children = """";
    try { foreach (var c in TopSolidHost.Operations.GetChildren(op)) children += c.Id + "",""; } catch { }
    ElementId owner = TopSolidHost.Elements.GetOwner(op);
    sb.AppendLine(""OP\t"" + index + ""\t"" + op.Id + ""\t"" + esc(TopSolidHost.Elements.GetTypeFullName(op)) + ""\t"" + esc(TopSolidHost.Elements.GetFriendlyName(op))
        + ""\t"" + (owner.IsEmpty ? -1 : owner.Id) + ""\t"" + (TopSolidHost.Operations.IsActive(op) ? 1 : 0) + ""\t"" + (TopSolidHost.Operations.IsFrozen(op) ? 1 : 0)
        + ""\t"" + (TopSolidHost.Elements.IsInvalid(op) ? 1 : 0) + ""\t"" + children);
    index++;
}
int budget = __MAXFACES__;
foreach (var shape in TopSolidHost.Shapes.GetShapes(doc))
{
    string sname = TopSolidHost.Elements.GetFriendlyName(shape);
    var faces = TopSolidHost.Shapes.GetFaces(shape);
    bool truncated = faces.Count > budget;
    var surfaces = new SortedDictionary<string, int>(StringComparer.Ordinal);
    var cylinders = new SortedDictionary<string, int>(StringComparer.Ordinal);
    int n = 0;
    foreach (var face in faces)
    {
        if (n++ >= budget) break;
        string st = TopSolidHost.Shapes.GetFaceSurfaceType(face).ToString();
        int k; surfaces.TryGetValue(st, out k); surfaces[st] = k + 1;
        if (st == ""Cylinder"")
        {
            string key = Math.Round(TopSolidHost.Shapes.GetFaceCylinderRadius(face) * 2000.0, 4).ToString(""R"", inv) + ""\t"" + Math.Round(TopSolidHost.Shapes.GetFaceCylinderLength(face) * 1000.0, 4).ToString(""R"", inv);
            int m; cylinders.TryGetValue(key, out m); cylinders[key] = m + 1;
        }
    }
    budget = Math.Max(0, budget - faces.Count);
    sb.AppendLine(""SHAPE\t"" + esc(sname) + ""\t"" + faces.Count + ""\t"" + (truncated ? 1 : 0));
    foreach (var kv in surfaces) sb.AppendLine(""SURF\t"" + esc(sname) + ""\t"" + kv.Key + ""\t"" + kv.Value);
    foreach (var kv in cylinders) sb.AppendLine(""CYL\t"" + esc(sname) + ""\t"" + kv.Key + ""\t"" + kv.Value);
}
return sb.ToString();";
    }
}
