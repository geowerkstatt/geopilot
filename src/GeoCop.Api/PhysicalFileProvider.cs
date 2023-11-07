namespace GeoCop.Api
{
    /// <summary>
    /// Provides read/write access to files in a predefined folder.
    /// </summary>
    public class PhysicalFileProvider : IFileProvider
    {
        private readonly IConfiguration configuration;
        private readonly string rootDirectoryEnvironmentKey;

        private DirectoryInfo? homeDirectory;

        private DirectoryInfo HomeDirectory => homeDirectory ?? throw new InvalidOperationException("The file provider needs to be initialized first.");

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalFileProvider"/> at the given root directory path.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="rootDirectoryEnvironmentKey">The name of the environment variable containing the root directory path.</param>
        /// <exception cref="ArgumentNullException">If <see cref="configuration"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">If <see cref="rootDirectoryEnvironmentKey"/> is <c>null</c>.</exception>
        public PhysicalFileProvider(IConfiguration configuration, string rootDirectoryEnvironmentKey)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.rootDirectoryEnvironmentKey = rootDirectoryEnvironmentKey ?? throw new ArgumentNullException(nameof(rootDirectoryEnvironmentKey));
        }

        private Stream CreateFile(string file)
        {
            return File.Create(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public FileHandle CreateFileWithRandomName(string extension)
        {
            var fileName = Path.ChangeExtension(Path.GetRandomFileName(), extension);
            var stream = CreateFile(fileName);

            return new FileHandle(fileName, stream);
        }

        /// <inheritdoc/>
        public Stream Open(string file)
        {
            return File.OpenRead(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public bool Exists(string file)
        {
            return File.Exists(Path.Combine(HomeDirectory.FullName, file));
        }

        /// <inheritdoc/>
        public virtual IEnumerable<string> GetFiles()
        {
            return HomeDirectory.GetFiles().Select(x => x.Name);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentException">If <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
        public void Initialize(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("The specified id is not valid.", nameof(id));
            var rootDirectory = configuration.GetValue<string>(rootDirectoryEnvironmentKey)
                ?? throw new InvalidOperationException($"Missing root directory, the value can be configured as \"{rootDirectoryEnvironmentKey}\"");

            homeDirectory = new DirectoryInfo(rootDirectory).CreateSubdirectory(id.ToString());
        }
    }
}
