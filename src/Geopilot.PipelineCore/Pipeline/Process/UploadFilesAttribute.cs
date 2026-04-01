namespace Geopilot.PipelineCore.Pipeline.Process;

/// <summary>
/// Indicates that a parameter is intended to receive uploaded <see cref="IPipelineFile"/> in a method invocation.
/// Only use it for parameters of type <see cref="IEnumerable{IPipelineFile}"/> to specify that the parameter should accept file uploads as part of the pipeline process.
/// Can be used on mehtods that are marked with <see cref="PipelineProcessRunAttribute"/> to specify that the parameter should be treated as a file upload,
/// allowing for appropriate handling and processing of the uploaded files within the pipeline process.
/// </summary>
/// <remarks>Apply this attribute to a method parameter to specify that it should accept file uploads, such as
/// those submitted through a web form. This attribute is typically used in web application frameworks to facilitate
/// file handling and validation for incoming requests.</remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class UploadFilesAttribute : Attribute
{
}
