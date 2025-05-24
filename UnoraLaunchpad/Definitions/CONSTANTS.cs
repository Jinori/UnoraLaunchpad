namespace UnoraLaunchpad.Definitions;

public static class CONSTANTS
{
    #if DEBUG
    public const string BASE_API_URL = "http://localhost:5001/api/files/";
    #else
    public const string BASE_API_URL = "http://unora.freeddns.org:5001/api/files/";
    #endif
    public const string GET_LAUNCHER_VERSION_RESOURCE = "launcherversion";
    public const string GET_FILE_DETAILS_RESOURCE = "details";
    public const string GET_FILE_RESOURCE = "get/";
    public const string GET_GAME_UPDATES_RESOURCE = "gameUpdates";
    public const string UNORA_FOLDER_NAME = "Unora";
}