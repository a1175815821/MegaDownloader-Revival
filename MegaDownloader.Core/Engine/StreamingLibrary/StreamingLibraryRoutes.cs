namespace MegaDownloader
{
    /// <summary>
    /// Routes served by the streaming library HTTP module. They live in the engine so
    /// StreamingHelper can build player URLs without referencing the embedded web server.
    /// </summary>
    public static class StreamingLibraryRoutes
    {
        public const string PaginaMain = "/library";
        public const string PaginaManagement = "/manage";
    }
}
