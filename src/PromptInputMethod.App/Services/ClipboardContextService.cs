using Windows.ApplicationModel.DataTransfer;
using System.Runtime.InteropServices;

namespace PromptInputMethod.App.Services;

public sealed class ClipboardContextService
{
    public async Task<string?> ReadClipboardTextAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            return null;
        }

        return await content.GetTextAsync();
    }

    public bool TrySetClipboardText(string text)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(text ?? string.Empty);
                Clipboard.SetContent(package);

                try
                {
                    Clipboard.Flush();
                }
                catch (COMException)
                {
                    // Flush only asks Windows to keep the clipboard after this app exits.
                    // SetContent has already updated the live clipboard, so do not crash
                    // when another process/debugger briefly owns the clipboard.
                }

                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }

        return false;
    }

    public void SetClipboardText(string text)
    {
        TrySetClipboardText(text);
    }
}
