namespace Geopilot.Api;

[TestClass]
public class StringExtensionsTest
{
    [TestMethod]
    [DataRow("SQUIRRELGENESIS", "SQUIRRELGENESIS")]
    [DataRow("JUNIORARK.xyz", "JUNIORARK.xyz")]
    [DataRow("PEEVEDBEAM-ANT.MESS.abc", "PEEVEDBEAM-ANT.MESS.abc")]
    [DataRow("WEIRD WATER.example", "WEIRD WATER.example")]
    [DataRow("AUTOFIRE123.doc", "AUTOFIRE123.doc")]
    [DataRow("SUNNY(1).doc", "SUNNY(1).doc")]
    [DataRow("ODD_MONKEY.doc", "ODD_MONKEY.doc")]
    [DataRow("SILLY,MONKEY.docx", "SILLY,MONKEY.docx")]
    [DataRow("CamelCase.bat", "CamelCase.bat")]
    [DataRow("SLICKER-CHIPMUNK.bat", "SLICKER-CHIPMUNK.bat")]
    public void SanitizeFileNameForValidFileNames(string expected, string fileName)
        => Assert.AreEqual(expected, fileName.SanitizeFileName());

    [TestMethod]
    [DataRow("CHIPMUNKWALK", "  CHIPMUNKWALK  ")]
    [DataRow("SLEEPYBOUNCE", "SLEEPYBOUNCE\n")]
    [DataRow("PLOWARK", "PLOWARK\r")]
    [DataRow("JUNIORGLEE", "JUNIORGLEE\t")]
    [DataRow("SILLYWATER", "SILLYWATER\r\n")]
    [DataRow("LATENTROUTE34", "LATENTROUTE?34")]
    [DataRow("TRAWLSOUFFLE", "/TRAWLSOUFFLE*")]
    [DataRow("VIOLENTIRON", "><VIOLENTIRON\"|")]
    [DataRow("YELLOWBAGEL", "YELLOWBAGEL://")]
    [DataRow("ZANYWATER", "ZANYWATER$")]
    [DataRow("SLICKERCANDID", "..\\SLICKERCANDID")]
    [DataRow("DIREFOOT", "./DIREFOOT:")]
    [DataRow("FIREFOOT", ".../...//FIREFOOT\\")]
    public void SanitizeFileNameForInvalidFileNames(string expected, string fileName)
        => Assert.AreEqual(expected, fileName.SanitizeFileName());

    [TestMethod]
    public void SanitizeFileNameForInvalid()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => string.Empty.SanitizeFileName());
        Assert.ThrowsExactly<ArgumentNullException>(() => "   ".SanitizeFileName());
        Assert.ThrowsExactly<ArgumentNullException>(() => (null as string).SanitizeFileName());
    }
}
