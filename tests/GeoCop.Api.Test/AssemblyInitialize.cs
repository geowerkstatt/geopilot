namespace GeoCop.Api.Test;

[TestClass]
public sealed class AssemblyInitialize
{
    public static TestDatabaseFixture DbFixture { get; private set; }

    [AssemblyInitialize]
    public static void TestSetup(TestContext testContext)
    {
        DbFixture = new TestDatabaseFixture();
    }
}
