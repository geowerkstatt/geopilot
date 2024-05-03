namespace Geopilot.Api.Controllers;

[TestClass]
public class Extensions
{
    [TestMethod]
    public void SanitizeFileNameForValidFileNames()
    {
        AssertSanitizeFileName("SQUIRRELGENESIS", "SQUIRRELGENESIS");
        AssertSanitizeFileName("JUNIORARK.xyz", "JUNIORARK.xyz");
        AssertSanitizeFileName("PEEVEDBEAM-ANT.MESS.abc", "PEEVEDBEAM-ANT.MESS.abc");
        AssertSanitizeFileName("WEIRD WATER.example", "WEIRD WATER.example");
        AssertSanitizeFileName("AUTOFIRE123.doc", "AUTOFIRE123.doc");
        AssertSanitizeFileName("SUNNY(1).doc", "SUNNY(1).doc");
        AssertSanitizeFileName("ODD_MONKEY.doc", "ODD_MONKEY.doc");
        AssertSanitizeFileName("SILLY,MONKEY.docx", "SILLY,MONKEY.docx");
        AssertSanitizeFileName("CamelCase.bat", "CamelCase.bat");
        AssertSanitizeFileName("SLICKER-CHIPMUNK.bat", "SLICKER-CHIPMUNK.bat");
    }

    [TestMethod]
    public void SanitizeFileNameForInvalidFileNames()
    {
        AssertSanitizeFileName("CHIPMUNKWALK", "  CHIPMUNKWALK  ");
        AssertSanitizeFileName("SLEEPYBOUNCE", "SLEEPYBOUNCE\n");
        AssertSanitizeFileName("PLOWARK", "PLOWARK\r");
        AssertSanitizeFileName("JUNIORGLEE", "JUNIORGLEE\t");
        AssertSanitizeFileName("SILLYWATER", "SILLYWATER\r\n");
        AssertSanitizeFileName("LATENTROUTE34", "LATENTROUTE?34");
        AssertSanitizeFileName("TRAWLSOUFFLE", "/TRAWLSOUFFLE*");
        AssertSanitizeFileName("VIOLENTIRON", "><VIOLENTIRON\"|");
        AssertSanitizeFileName("YELLOWBAGEL", "YELLOWBAGEL://");
        AssertSanitizeFileName("ZANYWATER", "ZANYWATER$");
        AssertSanitizeFileName("SLICKERCANDID", "..\\SLICKERCANDID");
        AssertSanitizeFileName("DIREFOOT", "./DIREFOOT:");
        AssertSanitizeFileName("FIREFOOT", ".../...//FIREFOOT\\");
    }

    private static void AssertSanitizeFileName(string expected, string fileName)
        => Assert.AreEqual(expected, fileName.SanitizeFileName());
}
