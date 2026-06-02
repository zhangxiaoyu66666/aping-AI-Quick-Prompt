namespace PromptInputMethod.App.Services;

public sealed class PrivacyDataService
{
    private readonly AppDatabaseService _database = new();

    public int ClearHistory()
    {
        var deleted = 0;
        deleted += _database.DeleteState("ocr.scheduler") ? 1 : 0;

        foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "prompt-input-method-ocr-*.png"))
        {
            deleted += TryDelete(file);
        }

        return deleted;
    }

    public int ClearFavorites()
    {
        var deleted = 0;
        deleted += _database.ClearRecords(AppDatabaseService.KindFavorite, updateSearchIndex: false);
        return deleted;
    }

    private static int TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            File.Delete(path);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}
