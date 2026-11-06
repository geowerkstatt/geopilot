using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Binds the enums of geopilot from query and route values by the camelCase names the API document gives them, as the
/// JSON does. It has to go ahead of the binder of the STAC library, which claims every enum but takes only the exact
/// member name; the enums of the library keep that binder.
/// </summary>
internal sealed class GeopilotEnumModelBinderProvider : IModelBinderProvider
{
    private const string BaseNamespace = nameof(Geopilot);
    private readonly EnumTypeModelBinderProvider enumTypeModelBinderProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotEnumModelBinderProvider"/> class.
    /// </summary>
    /// <param name="options">The MVC options the built-in enum binder reads its settings from.</param>
    public GeopilotEnumModelBinderProvider(MvcOptions options)
    {
        enumTypeModelBinderProvider = new EnumTypeModelBinderProvider(options);
    }

    /// <inheritdoc/>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The built-in enum binder converts through the type converter of the enum, which ignores case.
        var type = context.Metadata.UnderlyingOrModelType;
        return type.Namespace?.StartsWith(BaseNamespace + ".", StringComparison.Ordinal) == true
            ? enumTypeModelBinderProvider.GetBinder(context)
            : null;
    }
}
