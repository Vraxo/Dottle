using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Dottle.Services;
using Dottle.ViewModels;
using Dottle.Views;
using System; // Required for EventArgs

namespace Dottle;

public class App : Application
{
    private readonly SettingsService _settingsService;
    private readonly EncryptionService _encryptionService;
    private readonly JournalService _journalService;

    public App()
    {
        _settingsService = new SettingsService();
        _encryptionService = new EncryptionService();
        _journalService = new JournalService(_encryptionService, _settingsService);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());

        RequestedThemeVariant = ThemeVariant.Dark;

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
                // Pass SettingsService along with JournalService and password
                var mainViewModel = new MainViewModel(_journalService, _settingsService, password);
                var mainView = new MainView
                {
                    DataContext = mainViewModel
                };

                // Important: Set OwnerWindow reference on the VM *after* the view is part of the window
                mainWindow.Content = mainView; // Set content first
                mainViewModel.OwnerWindow = mainWindow; // Then set owner

                mainWindow.Title = "Dottle Journal";
                mainWindow.Width = 900;
                mainWindow.Height = 650;
                mainWindow.MinWidth = 600;
                mainWindow.MinHeight = 400;
                mainWindow.CanResize = true;
                mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen; // Recenter after resize
            };

            loginViewModel.LoginFailed += (sender, args) =>
            {
                // Optional feedback handled in LoginView/ViewModel
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}