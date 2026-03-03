using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class ConditionEvaluatorTest
{
    private readonly ConditionEvaluator conditionEvaluator = new ConditionEvaluator();

    [TestMethod(DisplayName = "Evaluate a simple boolean condition with no parameters")]
    [DataRow("1 == 1")]
    [DataRow("1+1 == 2")]
    [DataRow("1 == 1 and (true or false)")]
    [DataRow("true || false")]
    [DataRow("true && 1 == 1")]
    public async Task SimpleBooleanConditionWithNoParameters(string expression)
    {
        var stepResults = new Dictionary<string, StepResult>();
        var pipelineContext = new PipelineContext() { StepResults = stepResults };
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, pipelineContext).ConfigureAwait(false);
        Assert.IsTrue(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a simple non-boolean condition with no parameters")]
    [DataRow("Sin(0)")]
    [DataRow("Sqrt(4)")]
    [DataRow("123 / 2")]
    public async Task SimpleNonBooleanConditionWithNoParameters(string expression)
    {
        var stepResults = new Dictionary<string, StepResult>();
        var pipelineContext = new PipelineContext() { StepResults = stepResults };
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, pipelineContext).ConfigureAwait(false);
        Assert.IsFalse(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a syntactical invalid condition")]
    [DataRow("Hello World.", "Error parsing the expression.")]
    [DataRow("UnknownFunction(123)", "Function not found. Name: UnknownFunction")]
    [DataRow("[step_output] == bar", "Parameter step_output not defined.")]
    public async Task SyntacticalInvalidCondition(string expression, string exceptedExceptionMessage)
    {
        var stepResults = new Dictionary<string, StepResult>();
        var pipelineContext = new PipelineContext() { StepResults = stepResults };
        var exception = await Assert.ThrowsAsync<Exception>(() => conditionEvaluator.EvaluateConditionAsync(expression, pipelineContext));
        Assert.AreEqual(exception.Message, exceptedExceptionMessage);
    }

    [TestMethod(DisplayName = "Evaluate a boolean condition with parameters that evaluates to true")]
    [DataRow("[step1.result1] == 'hello'")]
    [DataRow("[step1.result1] != null")]
    [DataRow("[step1.result3] == null")]
    [DataRow("[step1.result4] < 124 and [step1.result4] > 122")]
    [DataRow("[step1.result1] == 'hello' and [step2.result1] == 'foo'")]
    [DataRow("[step1.result1] == 'world' or [step2.result1] == 'foo'")]
    public async Task ValidBooleanConditionWithParameters(string expression)
    {
        var step1_result1 = new StepOutput()
        {
            Data = "hello",
            Action = new HashSet<OutputAction>(),
        };
        var step1_result2 = new StepOutput()
        {
            Data = "world",
            Action = new HashSet<OutputAction>(),
        };
        var step1_result3 = new StepOutput()
        {
            Data = null,
            Action = new HashSet<OutputAction>(),
        };
        var step1_result4 = new StepOutput()
        {
            Data = 123,
            Action = new HashSet<OutputAction>(),
        };
        var step2_result1 = new StepOutput()
        {
            Data = "foo",
            Action = new HashSet<OutputAction>(),
        };
        var step2_result2 = new StepOutput()
        {
            Data = "bar",
            Action = new HashSet<OutputAction>(),
        };
        var step1Outputs = new Dictionary<string, StepOutput>()
        {
            { "result1", step1_result1 },
            { "result2", step1_result2 },
            { "result3", step1_result3 },
            { "result4", step1_result4 },
        };
        var step2Outputs = new Dictionary<string, StepOutput>()
        {
            { "result1", step2_result1 },
            { "result2", step2_result2 },
        };
        var step1Results = new StepResult() { Outputs = step1Outputs };
        var step2Results = new StepResult() { Outputs = step2Outputs };
        var stepResults = new Dictionary<string, StepResult>()
        {
            { "step1", step1Results },
            { "step2", step2Results },
        };
        var pipelineContext = new PipelineContext() { StepResults = stepResults };
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, pipelineContext).ConfigureAwait(false);
        Assert.IsTrue(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a boolean condition with invalid parameters references")]
    [DataRow("[step1_Step2Result1] == 'foo'", "Parameter step1_Step2Result1 not defined.")]
    [DataRow("[step1_somerandomresult] == 'foo'", "Parameter step1_somerandomresult not defined.")]
    public async Task BooleanConditionWithInvalidParameterReference(string expression, string exceptedExceptionMessage)
    {
        var step1_result1 = new StepOutput()
        {
            Data = "hello",
            Action = new HashSet<OutputAction>(),
        };
        var step1_result2 = new StepOutput()
        {
            Data = "world",
            Action = new HashSet<OutputAction>(),
        };
        var step2_result1 = new StepOutput()
        {
            Data = "foo",
            Action = new HashSet<OutputAction>(),
        };
        var step2_result2 = new StepOutput()
        {
            Data = "bar",
            Action = new HashSet<OutputAction>(),
        };
        var step1Outputs = new Dictionary<string, StepOutput>()
        {
            { "Step1Result1", step1_result1 },
            { "Step1Result2", step1_result2 },
        };
        var step2Outputs = new Dictionary<string, StepOutput>()
        {
            { "Step2Result1", step2_result1 },
            { "Step2Result2", step2_result2 },
        };
        var step1Results = new StepResult() { Outputs = step1Outputs };
        var step2Results = new StepResult() { Outputs = step2Outputs };
        var stepResults = new Dictionary<string, StepResult>()
        {
            { "step1", step1Results },
            { "step2", step2Results },
        };
        var pipelineContext = new PipelineContext() { StepResults = stepResults };
        var exception = await Assert.ThrowsAsync<Exception>(() => conditionEvaluator.EvaluateConditionAsync(expression, pipelineContext));
        Assert.AreEqual(exception.Message, exceptedExceptionMessage);
    }
}
