namespace UnoraLaunchpad;

/// <summary>
/// Represents the details of a game file, typically obtained from a file manifest.
/// Includes information such as the file's path, hash, and size.
/// </summary>
public sealed class FileDetail // Made class sealed
{
    /// <summary>
    /// Gets or sets the relative path of the file within the game's directory structure.
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// Gets or sets the MD5 hash (or other checksum) of the file, used for integrity checks.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Size { get; set; }
}
