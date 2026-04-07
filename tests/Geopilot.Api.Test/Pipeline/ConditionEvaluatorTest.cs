using Geopilot.Api.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class ConditionEvaluatorTest
{
    private readonly ConditionEvaluator conditionEvaluator = new ConditionEvaluator(Mock.Of<ILogger<ConditionEvaluatorTest>>());

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

    [TestMethod(DisplayName = "Length of a string array returns correct count")]
    public async Task LengthOfArrayParameter()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new string[] { "a", "b", "c" } },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 3", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length of a list returns correct count")]
    public async Task LengthOfListParameter()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new List<string> { "x", "y" } },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 2", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length of an empty array returns zero")]
    public async Task LengthOfEmptyArray()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", Array.Empty<string>() },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 0", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length of an empty list returns zero")]
    public async Task LengthOfEmptyList()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new List<int>() },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 0", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length of a single element array returns one")]
    public async Task LengthOfSingleElementArray()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new int[] { 42 } },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 1", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length with comparison operators")]
    public async Task LengthWithComparisonOperators()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new string[] { "a", "b", "c" } },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) > 0 and Length([step1.result1]) < 5", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }

    [TestMethod(DisplayName = "Length of null parameter throws")]
    public async Task LengthOfNullParameterThrows()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", null },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 0", expressionParameters));
    }

    [TestMethod(DisplayName = "Length of non-collection parameter throws")]
    public async Task LengthOfNonCollectionParameterThrows()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", 123 },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 0", expressionParameters));
    }

    [TestMethod(DisplayName = "Length with no arguments throws")]
    public async Task LengthWithNoArgumentsThrows()
    {
        var expressionParameters = new Dictionary<string, object?>();
        await Assert.ThrowsAsync<ArgumentException>(() => conditionEvaluator.EvaluateConditionAsync("Length() == 0", expressionParameters));
    }

    [TestMethod(DisplayName = "Length combined with other conditions")]
    public async Task LengthCombinedWithOtherConditions()
    {
        var expressionParameters = new Dictionary<string, object?>()
        {
            { "step1.result1", new string[] { "a", "b", "c" } },
            { "step2.result1", "foo" },
        };
        var result = await conditionEvaluator.EvaluateConditionAsync("Length([step1.result1]) == 3 and [step2.result1] == 'foo'", expressionParameters).ConfigureAwait(false);
        Assert.IsTrue(result);
    }
}
