namespace GeoCop.Api
{
    /// <summary>
    /// Provides read/write access to files in a predefined folder.
    /// </summary>
    public interface IFileProvider
    {
        /// <summary>
        /// Creates a file with a random name and the given <paramref name="extension"/>.
        /// </summary>
        /// <param name="extension">The file extension for the provided file.</param>
        /// <returns>A <see cref="FileHandle"/> with the file name and a read/write stream to the file specified.</returns>
        FileHandle CreateFileWithRandomName(string extension);

        /// <summary>
        /// Opens the specified file for reading.
        /// </summary>
        /// <param name="file">The file to be opened for reading.</param>
        /// <returns>A read-only <see cref="Stream"/> on the specified file.</returns>
        /// <exception cref="InvalidOperationException">If the file provider is not yet initialized.</exception>
        Stream Open(string file);

        /// <summary>
        /// Determines whether the specified <paramref name="file"/> exists.
        /// </summary>
        /// <param name="file">The file to check.</param>
        /// <returns><c>true</c> if the caller has the required permissions and path contains the name of
        /// an existing file; otherwise, <c>false</c>.</returns>
        bool Exists(string file);

        /// <summary>
        /// Enumerates the current home directory.
        /// </summary>
        /// <returns>Returns the contents of the home directory.</returns>
        /// <exception cref="InvalidOperationException">If the file provider is not yet initialized.</exception>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Initializes this file provider. Creates and sets the home directory
        /// to the folder with the <paramref name="id"/> specified.
        /// </summary>
        /// <param name="id">The specified folder id to be created.</param>
        void Initialize(Guid id);
    }
}
