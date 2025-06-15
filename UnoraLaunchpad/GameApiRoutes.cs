namespace UnoraLaunchpad;

using Definitions; // For CONSTANTS

/// <summary>
/// Defines the API routes for a specific game and the launcher itself.
/// Constructs full URLs based on a base URL and game identifier.
/// </summary>
public class GameApiRoutes
{
    /// <summary>
    /// Gets the base URL for the API.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets the identifier for the game (e.g., "Unora", "Legends").
    /// </summary>
    public string Game { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameApiRoutes"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL for the API. Trailing slashes will be removed.</param>
    /// <param name="game">The identifier for the game.</param>
    public GameApiRoutes(string baseUrl, string game)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        Game = game;
    }

    /// <summary>
    /// Gets the API endpoint for retrieving game file details (manifest).
    /// Format: {BaseUrl}/{Game}/files.json
    /// </summary>
    public string GameDetails => $"{BaseUrl}/{Game}/{CONSTANTS.GET_FILE_DETAILS_RESOURCE}";

    /// <summary>
    /// Gets the API endpoint for downloading a specific game file.
    /// </summary>
    /// <param name="relativePath">The relative path of the game file to download.</param>
    /// <returns>The full URL to the game file.</returns>
    /// Format: {BaseUrl}/{Game}/file/{relativePath}
    public string GameFile(string relativePath) => $"{BaseUrl}/{Game}/{CONSTANTS.GET_FILE_RESOURCE}{relativePath}";

    /// <summary>
    /// Gets the API endpoint for retrieving game-specific updates or news.
    /// Format: {BaseUrl}/{Game}/updates.json
    /// </summary>
    public string GameUpdates => $"{BaseUrl}/{Game}/{CONSTANTS.GET_GAME_UPDATES_RESOURCE}";

    // Global launcher endpoints are defined in CONSTANTS.cs and used directly by services where needed.
    // If they were to be centralized here, they would look like this:
    // public string LauncherVersion => $"{BaseUrl}/{CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE}";
    // public string LauncherExe => $"{BaseUrl}/getlauncher"; // Example, assuming a fixed path
}
