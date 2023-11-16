using Asp.Versioning;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GeoCop.Api.Controllers
{
    /// <summary>
    /// Controller for uploading files.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly ILogger<UploadController> logger;
        private readonly IValidationService validationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UploadController"/> class.
        /// </summary>
        public UploadController(ILogger<UploadController> logger, IValidationService validationService)
        {
            this.logger = logger;
            this.validationService = validationService;
        }

        /// <summary>
        /// Schedules a new job for the given <paramref name="file"/>.
        /// </summary>
        /// <param name="version">The API version.</param>
        /// <param name="file">The file to validate.</param>
        /// <remarks>
        /// ## Usage
        ///
        /// ### CURL
        ///
        /// ```bash
        /// curl -i -X POST -H "Content-Type: multipart/form-data" \
        ///   -F 'file=@example.xtf' https://example.com/api/v1/upload
        /// ```
        ///
        /// ### JavaScript
        ///
        /// ```javascript
        /// import { createReadStream } from 'fs';
        /// import FormData from 'form-data';
        /// import fetch from 'node-fetch';
        ///
        /// var form = new FormData();
        /// form.append('file', createReadStream('example.xtf'));
        /// const response = await fetch('https://example.com/api/v1/upload', {
        ///   method: 'POST',
        ///   body: form,
        /// });
        /// ```
        ///
        /// ### Python
        ///
        /// ```python
        /// import requests
        /// response = requests.post('https://example.com/api/v1/upload', files={'file':open('example.xtf')}).json()
        /// ```
        /// </remarks>
        /// <returns>Information for a newly created validation job.</returns>
        [HttpPost]
        [SwaggerResponse(StatusCodes.Status201Created, "The validation job was successfully created and is now scheduled for execution.", typeof(ValidationJobStatus), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ProblemDetails), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "The file is too large. Max allowed request body size is 200 MB.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1629:DocumentationTextMustEndWithAPeriod", Justification = "Not applicable for code examples.")]
        public async Task<IActionResult> UploadAsync(ApiVersion version, IFormFile file)
        {
            if (file == null) return Problem($"Form data <{nameof(file)}> cannot be empty.", statusCode: StatusCodes.Status400BadRequest);

            var (validationJob, fileHandle) = validationService.CreateValidationJob(file.FileName);
            using (fileHandle)
            {
                logger.LogInformation("Start uploading <{FormFile}> as <{File}>, file size: {FileSize}", file.FileName, fileHandle.FileName, file.Length);

                await file.CopyToAsync(fileHandle.Stream).ConfigureAwait(false);
                logger.LogInformation("Successfully received file: {File}", fileHandle.FileName);
            }

            var status = await validationService.StartValidationJobAsync(validationJob);
            logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", validationJob.Id);

            var location = new Uri(
                string.Format(CultureInfo.InvariantCulture, "/api/v{0}/status/{1}", version.MajorVersion, validationJob.Id),
                UriKind.Relative);

            return Created(location, status);
        }
    }
}
