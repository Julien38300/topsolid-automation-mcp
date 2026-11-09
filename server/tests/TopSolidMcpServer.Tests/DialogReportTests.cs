using System;
using System.Collections.Generic;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class DialogReportTests
    {
        private static readonly IntPtr MainHandle = new IntPtr(100);

        private static WindowInfo Main(bool enabled)
        {
            return new WindowInfo { Handle = MainHandle, ProcessId = 7, Title = "TopSolid 7.20", ClassName = "HwndWrapper[TopSolid]", IsVisible = true, IsEnabled = enabled };
        }

        private static WindowInfo Owned(string title, bool ownerEnabled, string cls = "HwndWrapper[TopSolid]")
        {
            return new WindowInfo { Handle = new IntPtr(200), ProcessId = 7, Title = title, ClassName = cls, IsVisible = true, IsEnabled = true, Owner = MainHandle, OwnerEnabled = ownerEnabled };
        }

        [Test]
        public void ModalDialog_IsReportedWithTextAndButtons()
        {
            var dialog = Owned("TopSolid", ownerEnabled: false, cls: DialogReport.Win32DialogClass);
            dialog.Texts.Add("Aucune entité à exporter.");
            dialog.Buttons.Add("&OK");
            var windows = new List<WindowInfo> { Main(enabled: false), dialog };

            var dialogs = DialogReport.SelectDialogs(windows);
            string report = DialogReport.Format(dialogs, windows, 1);

            Assert.That(dialogs, Has.Count.EqualTo(1));
            Assert.That(report, Does.Contain("[MODAL"));
            Assert.That(report, Does.Contain("Aucune entité à exporter."));
            Assert.That(report, Does.Contain("Buttons: [OK]"));
        }

        [Test]
        public void WpfButtonCaption_ExposedAsText_IsListedOnlyAsButton()
        {
            var dialog = Owned("Réparer la famille", ownerEnabled: false);
            dialog.Texts.Add("La famille doit être réparée.");
            dialog.Texts.Add("Réparer");
            dialog.Buttons.Add("Réparer");
            var windows = new List<WindowInfo> { Main(enabled: false), dialog };

            string report = DialogReport.Format(DialogReport.SelectDialogs(windows), windows, 1);

            Assert.That(report, Does.Contain("  La famille doit être réparée."));
            Assert.That(report, Does.Not.Contain("  Réparer\r\n").And.Not.Contain("  Réparer\n"));
            Assert.That(report, Does.Contain("Buttons: [Réparer]"));
        }

        [Test]
        public void FloatingToolPane_WithEnabledOwner_IsNotADialog()
        {
            var windows = new List<WindowInfo> { Main(enabled: true), Owned("Propriétés", ownerEnabled: true) };

            var dialogs = DialogReport.SelectDialogs(windows);

            Assert.That(dialogs, Is.Empty);
            Assert.That(DialogReport.Format(dialogs, windows, 1), Does.StartWith("No dialog box is open"));
        }

        [Test]
        public void UnownedWindow_WhileMainWindowIsDisabled_IsModal()
        {
            var popup = new WindowInfo { Handle = new IntPtr(300), ProcessId = 7, Title = "Réparer la famille", ClassName = "HwndWrapper[TopSolid]", IsVisible = true, IsEnabled = true };
            var windows = new List<WindowInfo> { Main(enabled: false), popup };

            var dialogs = DialogReport.SelectDialogs(windows);

            Assert.That(dialogs, Has.Count.EqualTo(1));
            Assert.That(dialogs[0].Title, Is.EqualTo("Réparer la famille"));
        }

        [Test]
        public void DisabledMainWindow_WithoutReadableDialog_SaysSo()
        {
            var windows = new List<WindowInfo> { Main(enabled: false) };

            string report = DialogReport.Format(DialogReport.SelectDialogs(windows), windows, 1);

            Assert.That(report, Does.Contain("main window is disabled"));
        }

        [Test]
        public void NoVisibleWindow_PointsAtAnotherWindowsSession()
        {
            var hidden = Main(enabled: true);
            hidden.IsVisible = false;

            string report = DialogReport.Format(new List<WindowInfo>(), new List<WindowInfo> { hidden }, 1);

            Assert.That(report, Does.Contain("another Windows session"));
        }

        [Test]
        public void NoProcess_SaysTopSolidIsNotRunning()
        {
            Assert.That(DialogReport.Format(new List<WindowInfo>(), new List<WindowInfo>(), 0), Does.Contain("No TopSolid process"));
        }

        [Test]
        public void Tooltip_IsIgnored()
        {
            var tip = Owned("tip", ownerEnabled: false, cls: "tooltips_class32");

            Assert.That(DialogReport.SelectDialogs(new List<WindowInfo> { Main(enabled: false), tip }), Is.Empty);
        }
    }
}
