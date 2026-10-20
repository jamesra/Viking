using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins that a damaged hand-edited <c>KeyName</c> in UserSettings.xml yields <see cref="Keys.None"/>
    /// instead of throwing from the key-down hotkey search, which would break every hotkey.
    /// </summary>
    [TestClass]
    public class HotkeyKeyCodeTests
    {
        [TestMethod]
        [DataRow("C", Keys.C)]
        [DataRow("F5", Keys.F5)]
        [DataRow("Escape", Keys.Escape)]
        public void KnownKeyNamesAreParsed(string name, Keys expected)
        {
            Hotkey hotkey = new() { KeyName = name };
            Assert.AreEqual(expected, hotkey.KeyCode);
        }

        [TestMethod]
        [DataRow("NotAKey")]
        [DataRow("")]
        [DataRow("   ")]
        public void UnreadableKeyNameIsKeysNoneNotAnException(string name)
        {
            Hotkey hotkey = new() { KeyName = name };
            Assert.AreEqual(Keys.None, hotkey.KeyCode);
        }
    }
}
