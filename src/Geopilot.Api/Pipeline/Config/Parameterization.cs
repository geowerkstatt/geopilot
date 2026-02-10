namespace Geopilot.Api.Pipeline.Config;

internal class Parameterization : Dictionary<string, string>
{
    public Parameterization()
        : base()
    {
    }

    public Parameterization(Parameterization src)
        : base(src)
    {
    }
}
