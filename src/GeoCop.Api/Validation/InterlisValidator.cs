using System.Globalization;

namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Validates an INTERLIS <see cref="TransferFile"/> at the given <see cref="IFileProvider.HomeDirectory"/>.
    /// </summary>
    public class InterlisValidator : IValidator
    {
        private readonly ILogger<InterlisValidator> logger;
        private readonly IConfiguration configuration;
        private readonly IFileProvider fileProvider;

        /// <inheritdoc/>
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public string? TransferFile { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InterlisValidator"/> class.
        /// </summary>
        public InterlisValidator(ILogger<InterlisValidator> logger, IConfiguration configuration, IFileProvider fileProvider)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.fileProvider = fileProvider;

            this.fileProvider.Initialize(Id);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string transferFile, CancellationToken cancellationToken)
        {
            if (transferFile == null) throw new ArgumentNullException(nameof(transferFile));
            if (string.IsNullOrWhiteSpace(transferFile)) throw new ArgumentException("Transfer file name cannot be empty.", nameof(transferFile));
            if (!fileProvider.Exists(transferFile)) throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Transfer file with the specified name <{0}> not found in <{1}>.", transferFile, fileProvider.HomeDirectory));

            TransferFile = transferFile;

            // TODO: validate file
            logger.LogInformation("Validating transfer file <{TransferFile}>...", TransferFile);
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
    }
}
