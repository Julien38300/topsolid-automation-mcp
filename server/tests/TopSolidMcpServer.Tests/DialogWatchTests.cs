using System;
using System.Collections.Generic;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    /// <summary>
    /// Field report: "when TopSolid shows an error window, it is not reported through the API and the
    /// process stays blocked until someone closes it." The watch reports the dialog at once, closes the
    /// ones that only inform, and leaves every real answer to the user.
    /// </summary>
    [TestFixture]
    public class DialogWatchTests
    {
        private static int _nextHandle = 1000;

        private static WindowInfo Dialog(string title, params string[] buttons)
        {
            var d = new WindowInfo
            {
                Handle = new IntPtr(_nextHandle++), ProcessId = 7, Title = title, ClassName = DialogReport.Win32DialogClass,
                IsVisible = true, IsEnabled = true, Owner = new IntPtr(100), OwnerEnabled = false
            };
            d.Buttons.AddRange(buttons);
            return d;
        }

        [TestCase(null, DialogPolicy.Acknowledge)]
        [TestCase("", DialogPolicy.Acknowledge)]
        [TestCase("ack", DialogPolicy.Acknowledge)]
        [TestCase("Report", DialogPolicy.Report)]
        [TestCase(" off ", DialogPolicy.Off)]
        [TestCase("0", DialogPolicy.Off)]
        [TestCase("whatever", DialogPolicy.Acknowledge)]
        public void Policy_DefaultsToAcknowledge(string raw, DialogPolicy expected)
        {
            Assert.That(DialogWatch.ParsePolicy(raw), Is.EqualTo(expected));
        }

        [TestCase(null, DialogWatch.DefaultGraceMs)]
        [TestCase("500", 500)]
        [TestCase("0", 0)]
        [TestCase("-3", DialogWatch.DefaultGraceMs)]
        [TestCase("abc", DialogWatch.DefaultGraceMs)]
        [TestCase("999999", DialogWatch.DefaultGraceMs)]
        public void GracePeriod_IsBounded(string raw, int expected)
        {
            Assert.That(DialogWatch.ParseGraceMs(raw), Is.EqualTo(expected));
        }

        [TestCase("&Oui", "Oui")]
        [TestCase(" OK ", "OK")]
        [TestCase("R&&D", "R&D")]
        [TestCase(null, "")]
        public void ButtonCaptions_LoseTheirAccelerators(string raw, string expected)
        {
            Assert.That(DialogWatch.NormalizeButton(raw), Is.EqualTo(expected));
        }

        [Test]
        public void MessageWithASingleButton_IsClosedAutomatically()
        {
            var d = Dialog("TopSolid", "&OK");
            d.Texts.Add("Aucune entité à exporter.");
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, 0), Is.EqualTo(DialogDecision.AutoClick));
        }

        [TestCase("OK", DialogDecision.AutoClick)]
        [TestCase("&Fermer", DialogDecision.AutoClick)]
        [TestCase("Supprimer", DialogDecision.Report)]
        [TestCase("Delete", DialogDecision.Report)]
        public void SingleButton_OnlyWhitelistedCaptionsAreAutoClicked(string caption, DialogDecision expected)
        {
            var d = Dialog("TopSolid", caption);
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, 0), Is.EqualTo(expected));
        }

        [Test]
        public void ReportPolicy_NeverClicks()
        {
            Assert.That(DialogWatch.Decide(Dialog("TopSolid", "OK"), DialogPolicy.Report, 0), Is.EqualTo(DialogDecision.Report));
        }

        [Test]
        public void Question_IsLeftToTheUser()
        {
            var d = Dialog("TopSolid", "&Oui", "&Non");
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, 0), Is.EqualTo(DialogDecision.Report));
        }

        [Test]
        public void SingleCancelButton_IsNeverPressedAutomatically()
        {
            // A busy window whose progress bar was not recognized: pressing it would stop TopSolid's work.
            Assert.That(DialogWatch.Decide(Dialog("Import", "Annuler"), DialogPolicy.Acknowledge, 0), Is.EqualTo(DialogDecision.Report));
        }

        [Test]
        public void ProgressWindow_IsIgnored()
        {
            var d = Dialog("Import en cours", "Annuler");
            d.HasProgressBar = true;
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, 0), Is.EqualTo(DialogDecision.Ignore));
        }

        [Test]
        public void ProgressWindowThatAsksSomething_IsReported()
        {
            var d = Dialog("Import", "Remplacer", "Ignorer");
            d.HasProgressBar = true;
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, 0), Is.EqualTo(DialogDecision.Report));
        }

        [Test]
        public void TooManyMessagesInOneScript_AreReported()
        {
            var d = Dialog("TopSolid", "OK");
            Assert.That(DialogWatch.Decide(d, DialogPolicy.Acknowledge, DialogWatch.MaxAutoClicksPerScript), Is.EqualTo(DialogDecision.Report));
        }

        [Test]
        public void Answers_NeedConfirmation_CancelAndSingleOkDoNot()
        {
            var question = Dialog("TopSolid", "&Oui", "&Non", "Annuler");
            Assert.That(DialogWatch.RequiresConfirm(question, "&Oui"), Is.True);
            Assert.That(DialogWatch.RequiresConfirm(question, "&Non"), Is.True, "No is an answer too");
            Assert.That(DialogWatch.RequiresConfirm(question, "Annuler"), Is.False);
            Assert.That(DialogWatch.RequiresConfirm(Dialog("TopSolid", "OK"), "OK"), Is.False);
            Assert.That(DialogWatch.RequiresConfirm(Dialog("TopSolid", "Supprimer"), "Supprimer"), Is.True, "a lone destructive button still needs confirm");
        }

        [Test]
        public void Tracker_WaitsForTheGracePeriod()
        {
            var h = new IntPtr(1);
            var tracker = new DialogTracker(new IntPtr[0], 1500);
            Assert.That(tracker.Update(new[] { h }, 0), Is.Empty);
            Assert.That(tracker.Update(new[] { h }, 1000), Is.Empty);
            Assert.That(tracker.Update(new[] { h }, 1600), Is.EqualTo(new[] { h }));
        }

        [Test]
        public void Tracker_ForgetsAWindowThatClosed()
        {
            var h = new IntPtr(1);
            var tracker = new DialogTracker(new IntPtr[0], 1500);
            tracker.Update(new[] { h }, 0);
            tracker.Update(new IntPtr[0], 1000);
            Assert.That(tracker.Update(new[] { h }, 2000), Is.Empty, "it reopened at 2000 ms: a new grace period starts");
            Assert.That(tracker.Update(new[] { h }, 3600), Is.EqualTo(new[] { h }));
        }

        [Test]
        public void Tracker_SkipsWindowsOpenBeforeTheScriptAndHandledOnes()
        {
            var before = new IntPtr(1);
            var handled = new IntPtr(2);
            var tracker = new DialogTracker(new[] { before }, 0);
            Assert.That(tracker.Update(new[] { before, handled }, 0), Is.EqualTo(new[] { handled }));
            tracker.Skip(handled);
            Assert.That(tracker.Update(new[] { before, handled }, 5000), Is.Empty);
        }

        [Test]
        public void PlanDismiss_RequiresAButton()
        {
            WindowInfo target; string caption;
            string error = DialogWatch.PlanDismiss(new List<WindowInfo> { Dialog("TopSolid", "OK") }, " ", null, false, out target, out caption);
            Assert.That(error, Does.Contain("'button' is required"));
            Assert.That(target, Is.Null);
        }

        [Test]
        public void PlanDismiss_NoDialog()
        {
            WindowInfo target; string caption;
            Assert.That(DialogWatch.PlanDismiss(new List<WindowInfo>(), "OK", null, false, out target, out caption),
                Is.EqualTo("No dialog box is open in TopSolid."));
        }

        [Test]
        public void PlanDismiss_UnknownButton_ListsTheButtons()
        {
            WindowInfo target; string caption;
            string error = DialogWatch.PlanDismiss(new List<WindowInfo> { Dialog("Export", "&OK") }, "Oui", null, true, out target, out caption);
            Assert.That(error, Does.Contain("no open dialog box has a button [Oui]"));
            Assert.That(error, Does.Contain("\"Export\" [OK]"));
        }

        [Test]
        public void PlanDismiss_AnswerWithoutConfirm_IsRefused()
        {
            WindowInfo target; string caption;
            string error = DialogWatch.PlanDismiss(new List<WindowInfo> { Dialog("TopSolid", "&Oui", "&Non") }, "oui", null, false, out target, out caption);
            Assert.That(error, Does.StartWith("Not pressed: [Oui]"));
            Assert.That(error, Does.Contain("confirm=true"));
            Assert.That(target, Is.Null);
            Assert.That(caption, Is.Null);
        }

        [Test]
        public void PlanDismiss_ConfirmedAnswer_PicksTheRawCaption()
        {
            WindowInfo target; string caption;
            var d = Dialog("TopSolid", "&Oui", "&Non");
            Assert.That(DialogWatch.PlanDismiss(new List<WindowInfo> { d }, "Oui", null, true, out target, out caption), Is.Null);
            Assert.That(target, Is.SameAs(d));
            Assert.That(caption, Is.EqualTo("&Oui"));
        }

        [Test]
        public void PlanDismiss_TitleNarrowsTheDialogs()
        {
            WindowInfo target; string caption;
            var first = Dialog("Avertissement", "OK");
            var second = Dialog("Erreur d'export", "OK");
            Assert.That(DialogWatch.PlanDismiss(new List<WindowInfo> { first, second }, "OK", "export", false, out target, out caption), Is.Null);
            Assert.That(target, Is.SameAs(second));
            Assert.That(DialogWatch.PlanDismiss(new List<WindowInfo> { first }, "OK", "export", false, out target, out caption),
                Does.Contain("No open dialog box has \"export\" in its title"));
        }

        [Test]
        public void Summary_KeepsTheTextForTheAgent()
        {
            var d = Dialog("TopSolid", "&OK");
            d.Texts.Add("Aucune entité\r\nà exporter.");
            d.Texts.Add("OK");
            Assert.That(DialogWatch.Summarize(d), Is.EqualTo("\"TopSolid\": Aucune entité à exporter. [OK]"));
        }

        [Test]
        public void SelectModal_LeavesOutFloatingPanes()
        {
            var modal = Dialog("TopSolid", "OK");
            var pane = Dialog("Propriétés");
            pane.OwnerEnabled = true;
            pane.ClassName = "HwndWrapper[TopSolid]";
            var list = DialogReport.SelectModal(new List<WindowInfo> { modal, pane });
            Assert.That(list, Is.EqualTo(new[] { modal }));
        }
    }
}
