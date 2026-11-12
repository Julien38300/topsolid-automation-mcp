using NUnit.Framework;
using TopSolidMcp.UpdateCheck;

namespace TopSolidMcp.UpdateCheck.Tests
{
    /// <summary>
    /// When the tray shows the "new release" popup. The automatic check runs at start and every few
    /// hours: the popup must appear once per new release, not at every check.
    /// </summary>
    [TestFixture]
    public class UpdateNotificationTests
    {
        [Test]
        public void NewerRelease_NeverShown_Notifies()
        {
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.18", "v1.8.19", null), Is.True);
        }

        [Test]
        public void SameOrOlderRelease_DoesNotNotify()
        {
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.19", "v1.8.19", null), Is.False);
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.19", "v1.8.18", null), Is.False);
        }

        [Test]
        public void AlreadyShownForThatRelease_DoesNotNotifyAgain()
        {
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.18", "v1.8.19", "1.8.19"), Is.False);
        }

        [Test]
        public void AnEvenNewerRelease_NotifiesAgain()
        {
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.18", "v1.8.20", "1.8.19"), Is.True);
        }

        [Test]
        public void MissingRemoteVersion_DoesNotNotify()
        {
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.18", null, null), Is.False);
            Assert.That(UpdateCheckLogic.ShouldNotify("1.8.18", "  ", null), Is.False);
        }
    }
}
