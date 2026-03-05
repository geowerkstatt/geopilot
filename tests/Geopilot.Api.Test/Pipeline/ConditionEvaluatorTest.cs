using Geopilot.Api.Pipeline;

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
        var expressionParameters = new Dictionary<string, object?>();
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a simple non-boolean condition with no parameters")]
    [DataRow("Sin(0)")]
    [DataRow("Sqrt(4)")]
    [DataRow("123 / 2")]
    public async Task SimpleNonBooleanConditionWithNoParameters(string expression)
    {
        var expressionParameters = new Dictionary<string, object?>();
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, expressionParameters).ConfigureAwait(false);
        Assert.IsFalse(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a syntactical invalid condition")]
    [DataRow("Hello World.", "Error parsing the expression.")]
    [DataRow("UnknownFunction(123)", "Function not found. Name: UnknownFunction")]
    [DataRow("[step.output] == bar", "Parameter step.output not defined.")]
    public async Task SyntacticalInvalidCondition(string expression, string exceptedExceptionMessage)
    {
        var expressionParameters = new Dictionary<string, object?>();
        var exception = await Assert.ThrowsAsync<Exception>(() => conditionEvaluator.EvaluateConditionAsync(expression, expressionParameters));
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
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", "hello" },
            { "step1.result2", "world" },
            { "step1.result3", null },
            { "step1.result4", 123 },
            { "step2.result1", "foo" },
            { "step2.result2", "bar" },
        };
        var conditionResult = await conditionEvaluator.EvaluateConditionAsync(expression, expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(conditionResult);
    }

    [TestMethod(DisplayName = "Evaluate a boolean condition with invalid parameters references")]
    [DataRow("[step1.Step2Result1] == 'foo'", "Parameter step1.Step2Result1 not defined.")]
    [DataRow("[step1.somerandomresult] == 'foo'", "Parameter step1.somerandomresult not defined.")]
    public async Task BooleanConditionWithInvalidParameterReference(string expression, string exceptedExceptionMessage)
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.Step1Result1", "hello" },
            { "step1.Step1Result2", "world" },
            { "step2.Step2Result1", "foo" },
            { "step2.Step2Result2", "bar" },
        };
        var exception = await Assert.ThrowsAsync<Exception>(() => conditionEvaluator.EvaluateConditionAsync(expression, expressionParameters));
        Assert.AreEqual(exception.Message, exceptedExceptionMessage);
    }
}
