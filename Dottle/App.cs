using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;          // Required for RequestedThemeVariant, Application.Styles
using Avalonia.Themes.Fluent;    // Required for FluentTheme
using Dottle.ViewModels;
using Dottle.Views;
using Dottle.Services;
// using System; // Check if needed - likely not directly

namespace Dottle;

public class App : Application
{
    private readonly EncryptionService _encryptionService;
    private readonly JournalService _journalService;

    public App()
    {
        _encryptionService = new EncryptionService();
        _journalService = new JournalService(_encryptionService);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load the theme explicitly in C#
        Styles.Add(new FluentTheme()); // Add FluentTheme instance

        // Set the desired theme variant AFTER adding the theme
        RequestedThemeVariant = ThemeVariant;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginViewModel = new LoginViewModel();
            var loginView = new LoginView
            {
                DataContext = loginViewModel
            };

            var mainWindow = new Window
            {
                Title = "Dottle - Login",
                Content = loginView,
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                CanResize = false,
                SystemDecorations = SystemDecorations.Full
            };

            loginViewModel.LoginSuccessful += (sender, password) =>
            {
                var mainViewModel = new MainViewModel(_journalService, password);
                var mainView = new MainView
                {
                    DataContext = mainViewModel
                };

                mainWindow.Content = mainView;
                mainWindow.Title = "Dottle Journal";
                mainWindow.Width = 900;
                mainWindow.Height = 650;
                mainWindow.MinWidth = 600;
                mainWindow.MinHeight = 400;
                mainWindow.CanResize = true;
                mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            };

            loginViewModel.LoginFailed += (sender, args) =>
            {
                // Optional feedback
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}