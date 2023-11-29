using GeoCop.Api.FileAccess;
using System.Diagnostics.CodeAnalysis;

namespace GeoCop.Api.Validation;

/// <summary>
/// Represents a validation job.
/// </summary>
/// <param name="Id">Validation job id.</param>
/// <param name="OriginalFileName">Original name of the uploaded file.</param>
/// <param name="TempFileName">Local name of the uploaded file used by the <see cref="IFileProvider"/>.</param>
[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1313:ParameterNamesMustBeginWithLowerCaseLetter", Justification = "Record class constructor.")]
public record class ValidationJob(Guid Id, string OriginalFileName, string TempFileName);
