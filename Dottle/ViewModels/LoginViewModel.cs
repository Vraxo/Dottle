using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace Dottle.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string? _password;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isLoggingIn;

    public event EventHandler<string>? LoginSuccessful;
    public event EventHandler? LoginFailed;

    public LoginViewModel()
    {
        // Validation logic is handled within the Login command execution.
    }

    private bool CanLogin()
    {
        return !string.IsNullOrEmpty(Password) && !IsLoggingIn;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login()
    {
        IsLoggingIn = true;
        ErrorMessage = null;
        await Task.Delay(150);

        // Simplified check: In a real app, attempt decryption here.
        if (!string.IsNullOrEmpty(Password))
        {
            LoginSuccessful?.Invoke(this, Password);
        }
        else
        {
            ErrorMessage = "Invalid password.";
            LoginFailed?.Invoke(this, EventArgs.Empty);
        }

        IsLoggingIn = false;
    }
}