using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Geopilot.Api.Controllers;

/// <summary>
/// An action that reads the multipart parts itself must keep MVC from reading the form first, because the
/// value provider factories consume the request body before the action runs. A test that invokes the action
/// directly cannot see this: it bypasses the value providers entirely, which is why the rule is pinned here.
/// </summary>
[TestClass]
public class DisableFormValueModelBindingAttributeTest
{
    [TestMethod]
    public void RemovesTheFactoriesThatWouldReadTheForm()
    {
        var context = CreateContext(new List<IValueProviderFactory>
        {
            new QueryStringValueProviderFactory(),
            new FormValueProviderFactory(),
            new FormFileValueProviderFactory(),
            new JQueryFormValueProviderFactory(),
            new RouteValueProviderFactory(),
        });

        new DisableFormValueModelBindingAttribute().OnResourceExecuting(context);

        CollectionAssert.AreEquivalent(
            new[] { typeof(QueryStringValueProviderFactory), typeof(RouteValueProviderFactory) },
            context.ValueProviderFactories.Select(f => f.GetType()).ToArray(),
            "Only the factories that read the request body may be removed.");
    }

    [TestMethod]
    public void TheInlineSubmissionDisablesFormValueBinding()
    {
        var action = typeof(SubmissionController).GetMethod(nameof(SubmissionController.CreateWithFiles));

        Assert.IsNotNull(action);
        Assert.IsNotNull(
            action.GetCustomAttribute<DisableFormValueModelBindingAttribute>(),
            "The action reads the multipart parts itself, so MVC must not read the form before it. Without this the request body is already consumed and the endpoint answers 500.");
    }

    private static ResourceExecutingContext CreateContext(IList<IValueProviderFactory> factories)
        => new(
            new Microsoft.AspNetCore.Mvc.ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            factories);
}
