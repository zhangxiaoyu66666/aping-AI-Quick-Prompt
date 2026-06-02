using Microsoft.UI.Xaml;

namespace PromptInputMethod.App;

public partial class App : Application
{
    private CompactPromptWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new CompactPromptWindow();
        _window.Activate();
    }
}
