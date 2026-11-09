using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Decides what a script does to TopSolid, from its code with literals and comments already
    /// stripped. TopSolid refuses PDM and document-level calls (Pdm.DeleteSeveral, Pdm.SetDescription,
    /// Pdm.Save, Documents.Open...) between Application.StartModification and EndModification with
    /// "EndModification must be called before calling this method", so a script made only of such
    /// calls must run without the automatic transaction.
    /// </summary>
    public static class TransactionRules
    {
        // IPdm methods whose API reference (7.21) says "must not be called between StartModification
        // and EndModification", plus SetName, measured refused the same way on TopSolid 7.20 (2026-11-09).
        private static readonly string[] PdmMembers =
        {
            "AddReferencedProjects", "CheckIn", "CheckInSeveral", "CheckOut", "CopySeveral", "CopySeveral2",
            "CopySeveralWithOptions", "CreateDocument", "CreateDocumentWithTemplate", "CreateFolder",
            "CreateProject", "CreateProjectFolder", "CreateProjectWithTemplate", "DeleteProjectFolder",
            "DeleteSeveral", "EnsureIsDirty", "ExportExecutablePackage", "ExportMinorRevisionFile",
            "ExportPackage", "ExportViewerPackage", "GetLatestRevision", "ImportFile",
            "ImportPackageAsDistinctCopy", "ImportPackageAsReplication", "IncludePackageAsDistinctCopy",
            "IncludePackageAsReplication", "MoveProject", "MoveProjectFolder", "MoveSeveral", "PurgeRevisions",
            "PurgeSeveral", "ReimportFile", "Restore", "RestoreLastSave", "Save", "SaveSeveral", "SetAuthor",
            "SetComment", "SetComplementaryPartNumber", "SetDescription", "SetLifeCycleMainState",
            "SetMajorRevisionLifeCycleMainState", "SetManufacturer", "SetManufacturerPartNumber", "SetName",
            "SetPartNumber", "SetProjectFolderName", "SetProjectMainDocument", "SetRealUserProperty",
            "SetRevisionTexts", "SetTextUserProperty", "UndoCheckOut"
        };

        // IDocuments methods with the same remark in the API reference (7.21).
        private static readonly string[] DocumentsMembers =
        {
            "Close", "CloseAll", "Drop", "Export", "ExportMachineToGlTF", "ExportWithOptions", "Import",
            "ImportWithOptions", "Open", "Save"
        };

        private static readonly Regex OutsideOnlyCall = new Regex(
            @"\bTopSolidHost\s*\.\s*(?:" +
            @"(?<i>Pdm)\s*\.\s*(?<m>" + string.Join("|", PdmMembers) + @")" +
            @"|(?<i>Documents)\s*\.\s*(?<m>" + string.Join("|", DocumentsMembers) + @")" +
            @"|(?<i>Tools)\s*\.\s*(?<m>CreateDerivedDocument|CreateMirrorDocument)" +
            @"|(?<i>Families)\s*\.\s*(?<m>CreateDerivedDocument)" +
            @")\s*\(",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // A call on a TopSolid host (TopSolidHost.Pdm. / TopSolidDesignHost.Assemblies. ...) whose name
        // changes something. BCL calls such as dict.Remove(key) or System.IO.Directory.CreateDirectory(path)
        // are not anchored on a host and do not count.
        private const string HostReceiver =
            @"\b(?:TopSolidHost|TopSolidDesignHost|TopSolidDraftingHost)\b(?:\s*\.\s*[A-Za-z_]\w*)*\s*\.\s*";

        private static readonly Regex[] MutatingCalls =
        {
            new Regex(HostReceiver + @"Set\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Create\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Delete\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Add\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Modify\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"EnsureIsDirty\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Rename\w*\s*\(", RegexOptions.Compiled),
            new Regex(HostReceiver + @"Remove\w*\s*\(", RegexOptions.Compiled)
        };

        /// <summary>
        /// Calls that TopSolid refuses inside a transaction, as "Pdm.DeleteSeveral", each listed once
        /// in order of first appearance.
        /// </summary>
        public static List<string> FindOutsideOnlyCalls(string sanitizedCode)
        {
            var calls = new List<string>();
            if (string.IsNullOrEmpty(sanitizedCode)) return calls;

            foreach (Match m in OutsideOnlyCall.Matches(sanitizedCode))
            {
                string call = m.Groups["i"].Value + "." + m.Groups["m"].Value;
                if (!calls.Contains(call)) calls.Add(call);
            }
            return calls;
        }

        /// <summary>
        /// True when the code changes the model: a mutating call on a TopSolid host that is not one
        /// of the PDM/document calls TopSolid refuses inside a transaction.
        /// </summary>
        public static bool ModifiesModel(string sanitizedCode)
        {
            if (string.IsNullOrEmpty(sanitizedCode)) return false;

            string masked = OutsideOnlyCall.Replace(sanitizedCode, m => new string(' ', m.Length));
            return MutatingCalls.Any(r => r.IsMatch(masked));
        }

        /// <summary>
        /// Error for a script that needs the automatic transaction (it changes the model) and also
        /// makes calls that TopSolid refuses inside it.
        /// </summary>
        public static string MixedTransactionError(IList<string> outsideOnlyCalls)
        {
            string list = string.Join(", ", outsideOnlyCalls);
            string message =
                "Error: this script changes the model, so it runs inside an automatic transaction, but it also calls " +
                list + ", which TopSolid refuses between StartModification and EndModification " +
                "(\"EndModification must be called before calling this method\"). Split it into two calls: one for the " +
                "PDM/document operations, one for the model changes. Or manage the transaction yourself: " +
                "TopSolidHost.Application.StartModification(\"name\", false); <model changes> " +
                "TopSolidHost.Application.EndModification(true, true); then make these calls after EndModification.";
            if (outsideOnlyCalls.Contains("Pdm.Save"))
                message += " Pdm.Save is not needed in an automatic transaction: the wrapper saves the edited document after EndModification.";
            return message;
        }
    }
}
