using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TopSolidMcpServer.Utils
{
    public enum ChangeArea { Material, Parameter, Operation, Geometry, Consequence, Identity }

    /// <summary>What to do with a change on the target document.</summary>
    public enum PlanAction
    {
        /// <summary>No target given.</summary>
        None,
        /// <summary>The tool can apply it (material, plain parameter, operation state).</summary>
        Auto,
        /// <summary>The target already matches.</summary>
        Already,
        /// <summary>The API cannot apply it: a person has to (operation content, new operation...).</summary>
        Manual,
        /// <summary>Recomputed by TopSolid or specific to each document: never applied.</summary>
        Info
    }

    public sealed class DocumentChange
    {
        public ChangeArea Area;
        /// <summary>added, removed, modified, state, order, invalid.</summary>
        public string Kind = "";
        public string Subject = "";
        public string Before = "";
        public string After = "";

        public PlanAction Action = PlanAction.None;
        public string TargetState = "";
        public string Hint = "";

        // Data the apply script needs.
        public SnapshotParameter ParameterAfter;
        public SnapshotOperation OperationAfter;
        public int TargetOperationOccurrence;
        public List<CylinderGroup> CylindersLost = new List<CylinderGroup>();
        public List<CylinderGroup> CylindersGained = new List<CylinderGroup>();
    }

    /// <summary>
    /// Pure comparison logic of topsolid_compare_documents: the changes between two snapshots, the plan
    /// against a target document, the report and the C# script that applies the automatic part.
    /// </summary>
    public static class DocumentComparison
    {
        /// <summary>Last segments of system parameters that identify a document: never carried over.</summary>
        private static readonly string[] IdentitySuffixes =
        {
            ".Name", ".Description", ".MajorRevision", ".MinorRevision", ".Author", ".PartNumber",
            ".Reference", ".Manufacturer", ".ManufacturerPartNumber", ".CreationDate", ".ModificationDate",
            ".LastModificationAuthor", ".Comment", ".Complement"
        };

        /// <summary>
        /// Changes from <paramref name="from"/> to <paramref name="to"/>. Two revisions of one document share
        /// their element ids, so <paramref name="sameDocument"/> matches operations by id; two documents are
        /// matched by operation type and name.
        /// </summary>
        public static List<DocumentChange> Diff(DocumentSnapshot from, DocumentSnapshot to, bool sameDocument)
        {
            var changes = new List<DocumentChange>();
            bool materialChanged = from.Material != to.Material;
            if (materialChanged)
                changes.Add(new DocumentChange { Area = ChangeArea.Material, Kind = "modified", Subject = "Material", Before = Or(from.Material), After = Or(to.Material) });

            DiffParameters(from, to, materialChanged, changes);
            DiffProperties(from, to, changes);
            DiffOperations(from, to, sameDocument, changes);
            DiffGeometry(from, to, changes);
            string before = OverallSize(from), after = OverallSize(to);
            if (before != null && after != null && before != after)
                changes.Add(new DocumentChange { Area = ChangeArea.Geometry, Kind = "modified", Subject = OverallSizeSubject, Before = before, After = after, Hint = "enclosing box of the part" });
            return changes;
        }

        private const string OverallSizeSubject = "Overall size";

        /// <summary>Enclosing box "X × Y × Z mm" from the part's system parameters, or null.</summary>
        private static string OverallSize(DocumentSnapshot s)
        {
            var sizes = new List<string>();
            foreach (string axis in new[] { "X", "Y", "Z" })
            {
                var p = s.Parameters.FirstOrDefault(x => x.Name.EndsWith(".EnclosingBox" + axis + "Size", StringComparison.Ordinal));
                double v = p == null ? double.NaN : DocumentSnapshot.Dbl(p.Value);
                if (double.IsNaN(v)) return null;
                sizes.Add(Math.Round(v * 1000.0, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            return string.Join(" × ", sizes) + " mm";
        }

        private static void DiffParameters(DocumentSnapshot from, DocumentSnapshot to, bool materialChanged, List<DocumentChange> changes)
        {
            foreach (var p in to.Parameters)
            {
                var old = from.FindParameter(p.Name);
                if (old != null && old.SameValue(p)) continue;
                var c = new DocumentChange
                {
                    Kind = old == null ? "added" : "modified",
                    Subject = p.Label,
                    Before = old == null ? "" : old.Display(),
                    After = p.Display(),
                    ParameterAfter = p
                };
                if (p.IsSystem)
                {
                    c.Area = IsIdentity(p.Name) ? ChangeArea.Identity : ChangeArea.Consequence;
                    if (c.Area == ChangeArea.Consequence && materialChanged && p.Name.Contains(".Materials.")) c.Hint = "follows the material";
                }
                else if (to.IsDriven(p))
                {
                    var op = to.DrivingOperation(p);
                    if (old == null)
                    {
                        c.Area = ChangeArea.Operation;
                        c.Hint = "created by the operation " + op.Name;
                    }
                    else
                    {
                        c.Area = ChangeArea.Consequence;
                        c.Hint = "computed by " + op.Name;
                    }
                }
                else c.Area = ChangeArea.Parameter;
                changes.Add(c);
            }
            foreach (var old in from.Parameters)
            {
                if (to.FindParameter(old.Name) != null) continue;
                changes.Add(new DocumentChange
                {
                    Area = old.IsSystem ? (IsIdentity(old.Name) ? ChangeArea.Identity : ChangeArea.Consequence) : ChangeArea.Parameter,
                    Kind = "removed",
                    Subject = old.Label,
                    Before = old.Display()
                });
            }
        }

        /// <summary>Document properties that no parameter already mirrors.</summary>
        private static void DiffProperties(DocumentSnapshot from, DocumentSnapshot to, List<DocumentChange> changes)
        {
            var mirrored = new HashSet<string>(to.Parameters.Select(p => p.Name).Concat(from.Parameters.Select(p => p.Name)));
            var before = new Dictionary<string, string>();
            foreach (var kv in from.Properties) before[kv.Key] = kv.Value;
            var seen = new HashSet<string>();
            foreach (var kv in to.Properties)
            {
                seen.Add(kv.Key);
                if (mirrored.Contains(kv.Key)) continue;
                string old;
                bool had = before.TryGetValue(kv.Key, out old);
                if (had && old == kv.Value) continue;
                changes.Add(new DocumentChange
                {
                    Area = IsIdentity(kv.Key) ? ChangeArea.Identity : ChangeArea.Consequence,
                    Kind = had ? "modified" : "added",
                    Subject = ShortName(kv.Key),
                    Before = had ? old : "",
                    After = kv.Value
                });
            }
            foreach (var kv in from.Properties)
                if (!seen.Contains(kv.Key) && !mirrored.Contains(kv.Key))
                    changes.Add(new DocumentChange { Area = IsIdentity(kv.Key) ? ChangeArea.Identity : ChangeArea.Consequence, Kind = "removed", Subject = ShortName(kv.Key), Before = kv.Value });
        }

        private static void DiffOperations(DocumentSnapshot from, DocumentSnapshot to, bool sameDocument, List<DocumentChange> changes)
        {
            var fromOps = from.Operations.Where(o => !o.IsStructural).ToList();
            var toOps = to.Operations.Where(o => !o.IsStructural).ToList();
            var fromKeys = Keys(fromOps, sameDocument);
            var toKeys = Keys(toOps, sameDocument);
            var fromByKey = new Dictionary<string, SnapshotOperation>();
            for (int i = 0; i < fromOps.Count; i++) fromByKey[fromKeys[i]] = fromOps[i];
            var toByKey = new Dictionary<string, SnapshotOperation>();
            for (int i = 0; i < toOps.Count; i++) toByKey[toKeys[i]] = toOps[i];

            for (int i = 0; i < toOps.Count; i++)
            {
                var op = toOps[i];
                SnapshotOperation old;
                if (!fromByKey.TryGetValue(toKeys[i], out old))
                {
                    string previous = "";
                    for (int j = i - 1; j >= 0 && previous.Length == 0; j--)
                        if (toOps[j].OwnerId == op.OwnerId || j == i - 1) previous = toOps[j].Name;
                    changes.Add(new DocumentChange
                    {
                        Area = ChangeArea.Operation,
                        Kind = "added",
                        Subject = op.Name,
                        After = op.ShortType + (previous.Length > 0 ? ", after " + previous : ""),
                        OperationAfter = op
                    });
                    continue;
                }
                if (old.Active != op.Active || old.Frozen != op.Frozen)
                    changes.Add(new DocumentChange { Area = ChangeArea.Operation, Kind = "state", Subject = op.Name, Before = old.StateText, After = op.StateText, OperationAfter = op });
                if (!old.Invalid && op.Invalid)
                    changes.Add(new DocumentChange { Area = ChangeArea.Operation, Kind = "invalid", Subject = op.Name, Before = "valid", After = "INVALID", OperationAfter = op });
                if (sameDocument && old.Name != op.Name)
                    changes.Add(new DocumentChange { Area = ChangeArea.Operation, Kind = "renamed", Subject = op.Name, Before = old.Name, After = op.Name, OperationAfter = op });
            }
            for (int i = 0; i < fromOps.Count; i++)
                if (!toByKey.ContainsKey(fromKeys[i]))
                    changes.Add(new DocumentChange { Area = ChangeArea.Operation, Kind = "removed", Subject = fromOps[i].Name, Before = fromOps[i].ShortType });

            // Order of the operations present on both sides: the ones outside the longest common
            // subsequence are the ones that moved.
            var common = toKeys.Where(fromByKey.ContainsKey).ToList();
            var commonFrom = fromKeys.Where(toByKey.ContainsKey).ToList();
            var kept = new HashSet<string>(LongestCommonSubsequence(commonFrom, common));
            foreach (var k in common)
                if (!kept.Contains(k))
                {
                    var op = toByKey[k];
                    int pos = common.IndexOf(k);
                    changes.Add(new DocumentChange
                    {
                        Area = ChangeArea.Operation,
                        Kind = "order",
                        Subject = op.Name,
                        Before = "position " + (commonFrom.IndexOf(k) + 1),
                        After = "position " + (pos + 1) + (pos > 0 ? ", after " + toByKey[common[pos - 1]].Name : ", first"),
                        OperationAfter = op
                    });
                }
        }

        private static void DiffGeometry(DocumentSnapshot from, DocumentSnapshot to, List<DocumentChange> changes)
        {
            foreach (var shape in to.Shapes)
            {
                var old = from.Shapes.FirstOrDefault(s => s.Name == shape.Name);
                if (old == null)
                {
                    changes.Add(new DocumentChange { Area = ChangeArea.Geometry, Kind = "added", Subject = "Shape " + shape.Name, After = shape.FaceCount + " faces" });
                    continue;
                }
                var removed = Subtract(old.Cylinders, shape.Cylinders);
                var added = Subtract(shape.Cylinders, old.Cylinders);
                var surfaces = new List<string>();
                foreach (var type in old.Surfaces.Keys.Union(shape.Surfaces.Keys).OrderBy(t => t, StringComparer.Ordinal))
                {
                    int a, b;
                    old.Surfaces.TryGetValue(type, out a);
                    shape.Surfaces.TryGetValue(type, out b);
                    if (a != b) surfaces.Add(type + " " + a + " → " + b);
                }
                if (removed.Count == 0 && added.Count == 0 && surfaces.Count == 0 && old.FaceCount == shape.FaceCount) continue;
                var c = new DocumentChange
                {
                    Area = ChangeArea.Geometry,
                    Kind = "modified",
                    Subject = "Shape " + shape.Name,
                    Before = removed.Count > 0 ? string.Join(", ", removed) : old.FaceCount + " faces",
                    After = added.Count > 0 ? string.Join(", ", added) : shape.FaceCount + " faces",
                    Hint = (removed.Count > 0 || added.Count > 0 ? "cylindrical faces changed" : "") +
                           (surfaces.Count > 0 ? (removed.Count > 0 || added.Count > 0 ? "; " : "") + "faces by type: " + string.Join(", ", surfaces) : ""),
                    CylindersLost = removed,
                    CylindersGained = added
                };
                changes.Add(c);
            }
            foreach (var old in from.Shapes)
                if (!to.Shapes.Any(s => s.Name == old.Name))
                    changes.Add(new DocumentChange { Area = ChangeArea.Geometry, Kind = "removed", Subject = "Shape " + old.Name, Before = old.FaceCount + " faces" });
        }

        /// <summary>Fills the action and the target state of every change.</summary>
        public static void Plan(List<DocumentChange> changes, DocumentSnapshot target, DocumentSnapshot source, bool sameDocument)
        {
            var targetOps = target.Operations.Where(o => !o.IsStructural).ToList();
            var targetKeys = Keys(targetOps, false);
            foreach (var c in changes)
            {
                switch (c.Area)
                {
                    case ChangeArea.Material:
                        c.TargetState = Or(target.Material);
                        c.Action = target.Material == c.After ? PlanAction.Already : PlanAction.Auto;
                        break;
                    case ChangeArea.Parameter:
                        PlanParameter(c, target);
                        break;
                    case ChangeArea.Operation:
                        PlanOperation(c, target, targetOps, targetKeys, sameDocument);
                        break;
                    case ChangeArea.Geometry:
                        PlanGeometry(c, target, source);
                        break;
                    default:
                        c.Action = PlanAction.Info;
                        break;
                }
            }
        }

        private static void PlanParameter(DocumentChange c, DocumentSnapshot target)
        {
            if (c.Kind == "removed")
            {
                var still = target.Parameters.FirstOrDefault(p => p.Label == c.Subject);
                c.TargetState = still == null ? "absent" : still.Display();
                c.Action = still == null ? PlanAction.Already : PlanAction.Manual;
                if (still != null) c.Hint = "delete it by hand once nothing uses it";
                return;
            }
            var p0 = c.ParameterAfter;
            var t = target.FindParameter(p0.Name);
            if (t == null)
            {
                c.TargetState = "absent";
                c.Action = IsCreatable(p0.Kind) ? PlanAction.Auto : PlanAction.Manual;
                if (c.Action == PlanAction.Manual) c.Hint = p0.Kind + " parameters cannot be created by the API";
                return;
            }
            c.TargetState = t.Display();
            if (t.SameValue(p0)) c.Action = PlanAction.Already;
            else if (t.Kind != p0.Kind) { c.Action = PlanAction.Manual; c.Hint = "the target parameter is a " + t.Kind; }
            else if (target.IsDriven(t)) { c.Action = PlanAction.Manual; c.Hint = "driven by " + target.DrivingOperation(t).Name + " in the target"; }
            else c.Action = IsCreatable(p0.Kind) ? PlanAction.Auto : PlanAction.Manual;
        }

        private static void PlanOperation(DocumentChange c, DocumentSnapshot target, List<SnapshotOperation> targetOps, List<string> targetKeys, bool sameDocument)
        {
            SnapshotOperation match = null;
            int occurrence = 0;
            if (c.OperationAfter != null)
            {
                string key = TypeNameKey(c.OperationAfter);
                for (int i = 0; i < targetOps.Count; i++)
                    if (TypeNameKey(targetOps[i]) == key) { match = targetOps[i]; occurrence = Occurrence(targetKeys[i]); break; }
            }
            else match = targetOps.FirstOrDefault(o => o.Name == c.Subject);
            c.TargetOperationOccurrence = occurrence;

            switch (c.Kind)
            {
                case "added":
                    c.TargetState = match == null ? "absent" : "present";
                    c.Action = match == null ? PlanAction.Manual : PlanAction.Already;
                    if (match == null) c.Hint = "the API cannot read the content of an operation: create it by hand";
                    break;
                case "removed":
                    c.TargetState = match == null ? "absent" : "present";
                    c.Action = match == null ? PlanAction.Already : PlanAction.Manual;
                    if (match != null) c.Hint = "delete it by hand (dependent operations would go with it)";
                    break;
                case "state":
                    if (match == null) { c.TargetState = "absent"; c.Action = PlanAction.Info; break; }
                    c.TargetState = match.StateText;
                    c.Action = match.Active == c.OperationAfter.Active && match.Frozen == c.OperationAfter.Frozen ? PlanAction.Already : PlanAction.Auto;
                    break;
                case "order":
                    c.TargetState = match == null ? "absent" : "position " + (targetOps.IndexOf(match) + 1);
                    c.Action = match == null ? PlanAction.Info : PlanAction.Manual;
                    if (match != null) c.Hint = "move it by hand in the tree";
                    break;
                case "invalid":
                    c.TargetState = match == null ? "absent" : (match.Invalid ? "INVALID" : "valid");
                    c.Action = PlanAction.Info;
                    break;
                default:
                    c.TargetState = match == null ? "absent" : match.Name;
                    c.Action = match != null && match.Name == c.After ? PlanAction.Already : PlanAction.Manual;
                    break;
            }
        }

        private static void PlanGeometry(DocumentChange c, DocumentSnapshot target, DocumentSnapshot source)
        {
            if (c.Subject == OverallSizeSubject)
            {
                c.TargetState = OverallSize(target) ?? "unknown";
                c.Action = c.TargetState == c.After ? PlanAction.Already : PlanAction.Manual;
                if (c.Action == PlanAction.Manual) c.Hint = "change the dimensions of the operations by hand (the API cannot read them)";
                return;
            }
            string shapeName = c.Subject.StartsWith("Shape ", StringComparison.Ordinal) ? c.Subject.Substring(6) : c.Subject;
            var shape = target.Shapes.FirstOrDefault(s => s.Name == shapeName) ?? target.Shapes.FirstOrDefault();
            if (shape == null) { c.TargetState = "no shape"; c.Action = PlanAction.Manual; return; }
            c.TargetState = shape.Cylinders.Count > 0 ? string.Join(", ", shape.Cylinders) : shape.FaceCount + " faces";
            if (c.Kind == "modified" && (c.CylindersGained.Count > 0 || c.CylindersLost.Count > 0))
            {
                // Done when the target has every cylinder group the source gained and none it lost.
                bool hasGained = c.CylindersGained.All(g => CountOf(shape, g.Key) >= g.Count);
                bool lostGone = c.CylindersLost.All(g => CountOf(shape, g.Key) == 0);
                if (hasGained && lostGone) { c.Action = PlanAction.Already; return; }
            }
            c.Action = PlanAction.Manual;
            var drills = target.Operations.Where(o => o.Type.IndexOf("Drilling", StringComparison.Ordinal) >= 0 && !o.IsStructural).Select(o => o.Name).ToList();
            c.Hint = (c.Hint.Length > 0 ? c.Hint + "; " : "") + "the API cannot change an operation's data" +
                     (drills.Count > 0 && c.Hint.Contains("cylindrical") ? "; candidates in the target: " + string.Join(", ", drills) : "");
        }

        /// <summary>The report returned by the tool.</summary>
        public static string Render(string title, List<DocumentChange> changes, DocumentSnapshot target, string footer)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            int auto = changes.Count(c => c.Action == PlanAction.Auto);
            int manual = changes.Count(c => c.Action == PlanAction.Manual);
            int already = changes.Count(c => c.Action == PlanAction.Already);
            int design = changes.Count(c => c.Area != ChangeArea.Consequence && c.Area != ChangeArea.Identity);
            sb.Append("Summary: " + design + " design change(s)");
            if (target != null) sb.Append(" — " + auto + " automatic, " + manual + " manual, " + already + " already in " + target.Document);
            sb.AppendLine("; " + changes.Count(c => c.Area == ChangeArea.Consequence) + " recomputed value(s).");
            if (design == 0) sb.AppendLine("No design change.");

            Section(sb, "MATERIAL", changes.Where(c => c.Area == ChangeArea.Material), target);
            Section(sb, "PARAMETERS", changes.Where(c => c.Area == ChangeArea.Parameter), target);
            Section(sb, "OPERATIONS", changes.Where(c => c.Area == ChangeArea.Operation), target);
            Section(sb, "GEOMETRY (seen on the faces: says WHAT changed, not in which operation)", changes.Where(c => c.Area == ChangeArea.Geometry), target);

            var consequences = changes.Where(c => c.Area == ChangeArea.Consequence).ToList();
            if (consequences.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("RECOMPUTED BY TOPSOLID (never applied)");
                foreach (var c in consequences.Take(12))
                    sb.AppendLine("  " + c.Subject + ": " + Or(c.Before) + " → " + Or(c.After) + (c.Hint.Length > 0 ? "  (" + c.Hint + ")" : ""));
                if (consequences.Count > 12) sb.AppendLine("  ... and " + (consequences.Count - 12) + " more");
            }
            var identity = changes.Where(c => c.Area == ChangeArea.Identity).ToList();
            if (identity.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("IDENTITY (name, revision...: never applied)");
                foreach (var c in identity)
                    sb.AppendLine("  " + c.Subject + ": " + Or(c.Before) + " → " + Or(c.After));
            }
            if (!string.IsNullOrEmpty(footer))
            {
                sb.AppendLine();
                sb.Append(footer);
            }
            return sb.ToString().TrimEnd() + "\n";
        }

        private static void Section(StringBuilder sb, string title, IEnumerable<DocumentChange> items, DocumentSnapshot target)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(title);
            foreach (var c in list)
            {
                string sign = c.Kind == "added" ? "+ " : c.Kind == "removed" ? "- " : "~ ";
                string what;
                if (c.Kind == "added") what = c.Subject + (c.After.Length > 0 ? " = " + c.After : "");
                else if (c.Kind == "removed") what = c.Subject + (c.Before.Length > 0 ? " (" + c.Before + ")" : "");
                else what = c.Subject + ": " + Or(c.Before) + " → " + Or(c.After) + (c.Kind == "order" || c.Kind == "state" ? "" : "");
                if (c.Area == ChangeArea.Operation && c.Kind == "added") what = c.Subject + " (" + c.After + ")";
                if (c.Kind == "order") what = c.Subject + " moved: " + c.Before + " → " + c.After;
                sb.Append("  " + sign + what);
                if (target != null) sb.Append("   | " + target.Document + ": " + Or(c.TargetState) + "  [" + ActionText(c) + "]");
                sb.AppendLine();
                if (c.Hint.Length > 0) sb.AppendLine("      " + c.Hint);
            }
        }

        private static string ActionText(DocumentChange c)
        {
            switch (c.Action)
            {
                case PlanAction.Auto:
                    if (c.Area == ChangeArea.Parameter) return c.TargetState == "absent" ? "AUTO: create" : "AUTO: set";
                    return "AUTO";
                case PlanAction.Already: return "already done";
                case PlanAction.Manual: return "MANUAL";
                case PlanAction.Info: return "info";
                default: return "";
            }
        }

        /// <summary>
        /// C# 5 script that applies every AUTO change to the target, in one transaction: all or nothing.
        /// The material comes from the current revision of the source document.
        /// </summary>
        public static string BuildApplyScript(string sourceDocument, string targetDocument, List<DocumentChange> changes)
        {
            var sb = new StringBuilder();
            sb.AppendLine(DocumentScripts.FindDocumentFunction);
            sb.AppendLine("DocumentId target = __find(" + DocumentScripts.Literal(targetDocument) + ");");
            sb.AppendLine("if (target.IsEmpty) return \"ERROR: target document not found: \" + " + DocumentScripts.Literal(targetDocument) + ";");
            bool needsSource = changes.Any(c => c.Action == PlanAction.Auto && c.Area == ChangeArea.Material);
            if (needsSource)
            {
                sb.AppendLine("DocumentId source = __find(" + DocumentScripts.Literal(sourceDocument) + ");");
                sb.AppendLine("if (source.IsEmpty) return \"ERROR: source document not found: \" + " + DocumentScripts.Literal(sourceDocument) + ";");
                sb.AppendLine("DocumentId material = TopSolidDesignHost.Parts.GetMaterial(source);");
            }
            sb.AppendLine("var log = new System.Text.StringBuilder();");
            sb.AppendLine("Func<string, ElementId> __param = n => { foreach (var p in TopSolidHost.Parameters.GetParameters(target)) if (TopSolidHost.Elements.GetName(p) == n) return p; return ElementId.Empty; };");
            sb.AppendLine("Func<string, string, int, ElementId> __op = (type, name, occ) => { int k = 0; foreach (var o in TopSolidHost.Operations.GetOperations(target)) if (TopSolidHost.Elements.GetTypeFullName(o) == type && TopSolidHost.Elements.GetFriendlyName(o) == name) { if (k == occ) return o; k++; } return ElementId.Empty; };");
            sb.AppendLine("TopSolidHost.Application.StartModification(\"Compare documents: apply\", false);");
            sb.AppendLine("try");
            sb.AppendLine("{");
            sb.AppendLine("    TopSolidHost.Documents.EnsureIsDirty(ref target);");
            foreach (var c in changes.Where(c => c.Action == PlanAction.Auto))
            {
                switch (c.Area)
                {
                    case ChangeArea.Material:
                        sb.AppendLine("    TopSolidDesignHost.Parts.SetMaterial(target, material);");
                        sb.AppendLine("    log.AppendLine(\"OK material: \" + " + DocumentScripts.Literal(c.After) + ");");
                        break;
                    case ChangeArea.Parameter:
                        AppendParameter(sb, c.ParameterAfter);
                        break;
                    case ChangeArea.Operation:
                        var op = c.OperationAfter;
                        sb.AppendLine("    { ElementId o = __op(" + DocumentScripts.Literal(op.Type) + ", " + DocumentScripts.Literal(op.Name) + ", " + c.TargetOperationOccurrence + ");");
                        sb.AppendLine("      if (o.IsEmpty) throw new Exception(\"operation not found: \" + " + DocumentScripts.Literal(op.Name) + ");");
                        sb.AppendLine("      if (TopSolidHost.Operations.IsFrozen(o) && !" + Bool(op.Frozen) + ") TopSolidHost.Operations.Unfreeze(o);");
                        sb.AppendLine("      if (TopSolidHost.Operations.IsActive(o) != " + Bool(op.Active) + ") { if (" + Bool(op.Active) + ") TopSolidHost.Operations.Activate(o); else TopSolidHost.Operations.Deactivate(o); }");
                        sb.AppendLine("      if (!TopSolidHost.Operations.IsFrozen(o) && " + Bool(op.Frozen) + ") TopSolidHost.Operations.Freeze(o);");
                        sb.AppendLine("      log.AppendLine(\"OK operation \" + " + DocumentScripts.Literal(op.Name) + " + \": " + op.StateText + "\"); }");
                        break;
                }
            }
            sb.AppendLine("    TopSolidHost.Application.EndModification(true, true);");
            sb.AppendLine("}");
            sb.AppendLine("catch (Exception ex)");
            sb.AppendLine("{");
            sb.AppendLine("    TopSolidHost.Application.EndModification(false, false);");
            sb.AppendLine("    return \"ERROR, nothing applied: \" + ex.Message;");
            sb.AppendLine("}");
            sb.AppendLine("TopSolidHost.Pdm.Save(TopSolidHost.Documents.GetPdmObject(target), true);");
            sb.AppendLine("return log.ToString();");
            return sb.ToString();
        }

        private static void AppendParameter(StringBuilder sb, SnapshotParameter p)
        {
            string name = DocumentScripts.Literal(p.Name);
            string create, set;
            switch (p.Kind)
            {
                case "Real":
                    string v = "double.Parse(" + DocumentScripts.Literal(p.Value) + ", System.Globalization.CultureInfo.InvariantCulture)";
                    create = "TopSolidHost.Parameters.CreateRealParameter(target, (UnitType)Enum.Parse(typeof(UnitType), " + DocumentScripts.Literal(string.IsNullOrEmpty(p.UnitType) ? "None" : p.UnitType) + "), " + v + ")";
                    set = "TopSolidHost.Parameters.SetRealValue(e, " + v + ")";
                    break;
                case "Integer":
                    create = "TopSolidHost.Parameters.CreateIntegerParameter(target, " + int.Parse(p.Value, System.Globalization.CultureInfo.InvariantCulture) + ")";
                    set = "TopSolidHost.Parameters.SetIntegerValue(e, " + int.Parse(p.Value, System.Globalization.CultureInfo.InvariantCulture) + ")";
                    break;
                case "Boolean":
                    string b = Bool(p.Value == "True");
                    create = "TopSolidHost.Parameters.CreateBooleanParameter(target, " + b + ")";
                    set = "TopSolidHost.Parameters.SetBooleanValue(e, " + b + ")";
                    break;
                default:
                    create = "TopSolidHost.Parameters.CreateTextParameter(target, " + DocumentScripts.Literal(p.Value) + ")";
                    set = "TopSolidHost.Parameters.SetTextValue(e, " + DocumentScripts.Literal(p.Value) + ")";
                    break;
            }
            sb.AppendLine("    { ElementId e = __param(" + name + ");");
            sb.AppendLine("      if (e.IsEmpty) { e = " + create + "; TopSolidHost.Elements.SetName(e, " + name + "); log.AppendLine(\"OK created \" + " + name + " + \" = " + Escape(p.Display()) + "\"); }");
            sb.AppendLine("      else { " + set + "; log.AppendLine(\"OK set \" + " + name + " + \" = " + Escape(p.Display()) + "\"); } }");
        }

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string Bool(bool b)
        {
            return b ? "true" : "false";
        }

        private static bool IsCreatable(string kind)
        {
            return kind == "Real" || kind == "Text" || kind == "Integer" || kind == "Boolean";
        }

        private static bool IsIdentity(string fullName)
        {
            foreach (var s in IdentitySuffixes)
                if (fullName.EndsWith(s, StringComparison.Ordinal) && fullName.StartsWith("$TopSolid.Kernel.", StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ShortName(string fullName)
        {
            int i = fullName.LastIndexOf('.');
            return i < 0 ? fullName : fullName.Substring(i + 1);
        }

        private static string Or(string s)
        {
            return string.IsNullOrEmpty(s) ? "—" : s;
        }

        private static string TypeNameKey(SnapshotOperation o)
        {
            return o.Type + "|" + o.Name;
        }

        /// <summary>Match keys: the id for two revisions, else type + name + occurrence.</summary>
        private static List<string> Keys(List<SnapshotOperation> ops, bool byId)
        {
            var keys = new List<string>();
            var count = new Dictionary<string, int>();
            foreach (var o in ops)
            {
                if (byId) { keys.Add("#" + o.Id); continue; }
                string k = TypeNameKey(o);
                int n;
                count.TryGetValue(k, out n);
                count[k] = n + 1;
                keys.Add(k + "|" + n);
            }
            return keys;
        }

        private static int Occurrence(string key)
        {
            int i = key.LastIndexOf('|');
            int n;
            return i >= 0 && int.TryParse(key.Substring(i + 1), out n) ? n : 0;
        }

        private static List<CylinderGroup> Subtract(List<CylinderGroup> a, List<CylinderGroup> b)
        {
            var result = new List<CylinderGroup>();
            foreach (var g in a)
            {
                var other = b.FirstOrDefault(x => x.Key == g.Key);
                int left = g.Count - (other == null ? 0 : other.Count);
                if (left > 0) result.Add(new CylinderGroup { DiameterMm = g.DiameterMm, LengthMm = g.LengthMm, Count = left });
            }
            return result;
        }

        private static int CountOf(SnapshotShape shape, string key)
        {
            return shape.Cylinders.Where(g => g.Key == key).Sum(g => g.Count);
        }

        private static List<string> LongestCommonSubsequence(List<string> a, List<string> b)
        {
            var len = new int[a.Count + 1, b.Count + 1];
            for (int i = a.Count - 1; i >= 0; i--)
                for (int j = b.Count - 1; j >= 0; j--)
                    len[i, j] = a[i] == b[j] ? len[i + 1, j + 1] + 1 : Math.Max(len[i + 1, j], len[i, j + 1]);
            var result = new List<string>();
            int x = 0, y = 0;
            while (x < a.Count && y < b.Count)
            {
                if (a[x] == b[y]) { result.Add(a[x]); x++; y++; }
                else if (len[x + 1, y] >= len[x, y + 1]) x++;
                else y++;
            }
            return result;
        }
    }
}
