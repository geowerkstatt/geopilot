namespace Geopilot.Api.Controllers;

/// <summary>
/// Names the type that describes the multipart form an action reads from the request stream itself. MVC binds
/// nothing for such an action, so the published API would show no request body at all; the operation filter
/// <see cref="MultipartRequestBodyOperationFilter"/> describes it from this type instead.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MultipartRequestBodyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartRequestBodyAttribute"/> class.
    /// </summary>
    /// <param name="formType">The type whose properties are the fields and files of the form.</param>
    public MultipartRequestBodyAttribute(Type formType)
    {
        FormType = formType;
    }

    /// <summary>
    /// The type whose properties are the fields and files of the form.
    /// </summary>
    public Type FormType { get; }
}
