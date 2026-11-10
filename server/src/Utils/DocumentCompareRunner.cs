using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// The flow of topsolid_compare_documents, without TopSolid: <c>run</c> executes a C# script in
    /// TopSolid and returns its output (the script executor in the server, a fake in the tests).
    /// </summary>
    public sealed class DocumentCompareRunner
    {
        private readonly Func<string, string> _run;
        private readonly bool _readOnly;

        public DocumentCompareRunner(Func<string, string> run, bool readOnly)
        {
            _run = run;
            _readOnly = readOnly;
        }

        /// <summary>Checks the arguments; null when they are valid.</summary>
        public string Validate(JObject args)
        {
            string mode = Str(args, "mode");
            bool documents = mode == "documents";
            if (mode.Length > 0 && mode != "revisions" && !documents)
                return "Error: mode must be 'revisions' or 'documents'.";
            if (documents && Str(args, "target").Length == 0)
                return "Error: mode 'documents' needs a 'target' document.";
            if (Apply(args) && Str(args, "target").Length == 0)
                return "Error: apply=true needs a 'target' document.";
            if (Apply(args) && Str(args, "to_revision").Length > 0)
                return "Error: apply=true carries over the CURRENT revision: leave to_revision empty.";
            if (Apply(args) && _readOnly)
                return "Error: the server runs in read-only mode: compare without apply, or turn read-only off in the tray.";
            return null;
        }

        public string Run(JObject args)
        {
            args = args ?? new JObject();
            string invalid = Validate(args);
            if (invalid != null) return invalid;

            string document = Str(args, "document");
            string fromRevision = Str(args, "from_revision");
            string toRevision = Str(args, "to_revision");
            string target = Str(args, "target");
            bool documents = Str(args, "mode") == "documents";
            bool apply = Apply(args);

            string error;
            var to = Snapshot(document, documents ? "" : toRevision, out error);
            if (to == null) return error;
            if (target.Length > 0 && target == to.Document)
                return "Error: 'target' is the compared document itself (" + target + (document.Length == 0 ? ", the edited document" : "") +
                    "): name the document that evolved with 'document'.";

            DocumentSnapshot from, targetSnap = null;
            if (documents)
            {
                from = Snapshot(target, "", out error);
                if (from == null) return error;
                targetSnap = from;
            }
            else
            {
                if (fromRevision.Length == 0 && to.Revisions.Count < 2)
                    return "Error: " + to.Document + " has a single revision (" + string.Join(", ", to.Revisions) + "): " +
                        "nothing to compare. Give from_revision, or use mode 'documents' with a target.";
                from = Snapshot(to.Document, fromRevision.Length == 0 ? "previous" : fromRevision, out error);
                if (from == null) return error;
                if (from.Revision == to.Revision)
                    return "Error: both sides are revision " + to.Revision + " of " + to.Document + ".";
                if (target.Length > 0)
                {
                    targetSnap = Snapshot(target, "", out error);
                    if (targetSnap == null) return error;
                }
            }

            var changes = DocumentComparison.Diff(from, to, !documents);
            if (targetSnap != null) DocumentComparison.Plan(changes, targetSnap, to, !documents);

            int auto = changes.Count(c => c.Action == PlanAction.Auto);
            if (!apply)
            {
                string footer = targetSnap == null ? ""
                    : auto > 0 ? "Call again with apply=true to apply the " + auto + " AUTO change(s) to " + targetSnap.Document + " (one transaction, then saved)."
                    : "Nothing can be applied automatically.";
                return DocumentComparison.Render(Title(from, to, targetSnap, documents), changes, targetSnap, footer);
            }
            if (auto == 0)
                return DocumentComparison.Render(Title(from, to, targetSnap, documents), changes, targetSnap, "Nothing to apply automatically: nothing was changed.");

            string result = (_run(DocumentComparison.BuildApplyScript(to.Document, targetSnap.Document, changes)) ?? "").Trim();
            if (!result.StartsWith("OK", StringComparison.Ordinal))
                return "APPLY FAILED on " + targetSnap.Document + ", nothing changed:\n" + result + "\n\n" +
                    DocumentComparison.Render(Title(from, to, targetSnap, documents), changes, targetSnap, "");

            // Read the target again: the report then shows what is really done and what is left.
            var after = Snapshot(targetSnap.Document, "", out error);
            if (after == null) return "APPLIED to " + targetSnap.Document + ":\n" + result + "\n\nCould not read it back: " + error;
            if (documents) from = after;
            var remaining = DocumentComparison.Diff(from, to, !documents);
            DocumentComparison.Plan(remaining, after, to, !documents);
            int left = remaining.Count(c => c.Action == PlanAction.Manual);
            return "APPLIED to " + after.Label + ":\n" + result + "\n\n" +
                DocumentComparison.Render(Title(from, to, after, documents), remaining, after,
                    left > 0 ? left + " change(s) left to do by hand in " + after.Document + " (MANUAL above)." : "Nothing left to do by hand.");
        }

        private static string Title(DocumentSnapshot from, DocumentSnapshot to, DocumentSnapshot target, bool documents)
        {
            if (documents)
                return "COMPARE " + target.Label + " with " + to.Label + " (what to change in " + target.Document + " to match " + to.Document + ")";
            return "COMPARE " + to.Document + ": " + from.Revision + " → " + to.Revision +
                (target != null ? "   target: " + target.Label : "") +
                "\nRevisions of " + to.Document + ": " + string.Join(", ", to.Revisions);
        }

        private DocumentSnapshot Snapshot(string document, string revision, out string error)
        {
            string raw = _run(DocumentScripts.Snapshot(document, revision)) ?? "";
            var snap = DocumentSnapshot.Parse(raw);
            if (snap.Document.Length == 0)
            {
                error = snap.Errors.Count > 0
                    ? "Error: " + snap.Errors[0]
                    : "Error while reading " + (document.Length > 0 ? document : "the edited document") + ": " + Clip(raw);
                return null;
            }
            error = null;
            return snap;
        }

        private static bool Apply(JObject args)
        {
            var t = args["apply"];
            return t != null && t.Type == JTokenType.Boolean && (bool)t;
        }

        private static string Str(JObject args, string name)
        {
            var t = args[name];
            return t == null || t.Type == JTokenType.Null ? "" : t.ToString().Trim();
        }

        private static string Clip(string s)
        {
            s = (s ?? "").Trim();
            return s.Length > 1500 ? s.Substring(0, 1500) + "..." : s;
        }
    }
}
