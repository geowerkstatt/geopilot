using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class ProcessReflectionTest
{
    [TestMethod]
    public void GetRunMethodsReturnsTheSingleAnnotatedMethod()
    {
        var runMethods = ProcessReflection.GetRunMethods(typeof(SingleRunProcess));

        Assert.HasCount(1, runMethods);
        Assert.AreEqual(nameof(SingleRunProcess.RunAsync), runMethods[0].Name);
    }

    [TestMethod]
    public void GetRunMethodsReturnsEmptyWhenNoMethodIsAnnotated()
    {
        var runMethods = ProcessReflection.GetRunMethods(typeof(NoRunProcess));

        Assert.HasCount(0, runMethods);
    }

    [TestMethod]
    public void GetRunMethodsReturnsAllWhenMultipleAreAnnotated()
    {
        var runMethods = ProcessReflection.GetRunMethods(typeof(MultipleRunProcess));

        Assert.HasCount(2, runMethods);
    }

    [TestMethod]
    public void FindRunMethodReturnsTheMethodWhenExactlyOne()
    {
        var runMethod = ProcessReflection.FindRunMethod(typeof(SingleRunProcess));

        Assert.IsNotNull(runMethod);
    }

    [TestMethod]
    public void FindRunMethodReturnsNullWhenNoMethodIsAnnotated()
    {
        Assert.IsNull(ProcessReflection.FindRunMethod(typeof(NoRunProcess)));
    }

    [TestMethod]
    public void FindRunMethodReturnsNullWhenMultipleAreAnnotated()
    {
        Assert.IsNull(ProcessReflection.FindRunMethod(typeof(MultipleRunProcess)));
    }

    [TestMethod]
    public void ResolveResultTypeReturnsTheTaskResultType()
    {
        Assert.AreEqual(typeof(SampleResult), ProcessReflection.ResolveResultType(typeof(SingleRunProcess)));
    }

    [TestMethod]
    public void ResolveResultTypeReturnsNullWithoutAUniqueRunMethod()
    {
        Assert.IsNull(ProcessReflection.ResolveResultType(typeof(NoRunProcess)));
        Assert.IsNull(ProcessReflection.ResolveResultType(typeof(MultipleRunProcess)));
    }

    [TestMethod]
    public void ResolveResultTypeReturnsNullWhenNotReturningAGenericTask()
    {
        Assert.IsNull(ProcessReflection.ResolveResultType(typeof(NonGenericTaskProcess)));
        Assert.IsNull(ProcessReflection.ResolveResultType(typeof(SynchronousProcess)));
    }

    private sealed class SampleResult
    {
    }

    private sealed class SingleRunProcess
    {
        [PipelineProcessRun]
        public Task<SampleResult> RunAsync() => Task.FromResult(new SampleResult());
    }

    private sealed class NoRunProcess
    {
        public Task<SampleResult> RunAsync() => Task.FromResult(new SampleResult());
    }

    private sealed class MultipleRunProcess
    {
        [PipelineProcessRun]
        public Task<SampleResult> RunAsync() => Task.FromResult(new SampleResult());

        [PipelineProcessRun]
        public Task<SampleResult> RunTwiceAsync() => Task.FromResult(new SampleResult());
    }

    private sealed class NonGenericTaskProcess
    {
        [PipelineProcessRun]
        public Task RunAsync() => Task.CompletedTask;
    }

    private sealed class SynchronousProcess
    {
        [PipelineProcessRun]
        public SampleResult Run() => new SampleResult();
    }
}
