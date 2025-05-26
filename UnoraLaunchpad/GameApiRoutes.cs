namespace UnoraLaunchpad;

using Definitions;

public class GameApiRoutes
{
    public string BaseUrl { get; }
    public string Game { get; }

    public GameApiRoutes(string baseUrl, string game)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        Game = game;
    }

    // Game-specific endpoints
    public string GameDetails => $"{BaseUrl}/{Game}/{CONSTANTS.GET_FILE_DETAILS_RESOURCE}";
    public string GameFile(string relativePath) => $"{BaseUrl}/{Game}/{CONSTANTS.GET_FILE_RESOURCE}{relativePath}";
    public string GameUpdates => $"{BaseUrl}/{Game}/{CONSTANTS.GET_GAME_UPDATES_RESOURCE}";

    // Global launcher endpoints (shared for all games)
    public string LauncherVersion => $"{BaseUrl}/{CONSTANTS.GET_LAUNCHER_VERSION_RESOURCE}";
    public string LauncherExe => $"{BaseUrl}/getlauncher";
}

