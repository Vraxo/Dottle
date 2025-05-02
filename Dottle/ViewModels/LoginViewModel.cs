using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Security; // Required for SecureString if used, but sticking to string for simplicity here.
using System.Threading.Tasks; // For async command

namespace Dottle.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string? _password;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoggingIn;

    public event EventHandler<string>? LoginSuccessful;
    public event EventHandler? LoginFailed;

    private readonly string _testPassword = "password"; // In a real app, this isn't stored. We'd try decryption.

    public LoginViewModel()
    {
        // In a real scenario, we wouldn't have a predefined password here.
        // The "correct" password is the one that successfully decrypts *any* existing journal file.
        // Or, if no files exist, the first password entered is used to create the first file.
        // For this example, we'll simulate a check.
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
        await Task.Delay(150); // Simulate check

        // !! IMPORTANT !!
        // In a real application, the validation logic is different.
        // You would typically:
        // 1. Get the list of journal files.
        // 2. Try to decrypt the *first* file found using the entered password.
        // 3. If successful, the password is correct. Store it (or better, the derived key).
        // 4. If it fails, the password is wrong.
        // 5. If NO journal files exist yet, accept the entered password as the new password
        //    and use it to encrypt the first journal created.

        // Simplified check for this example:
        if (!string.IsNullOrEmpty(Password)) // Replace with real decryption check attempt
        {
            LoginSuccessful?.Invoke(this, Password); // Pass password to derive key later
        }
        else
        {
            ErrorMessage = "Invalid password.";
            LoginFailed?.Invoke(this, EventArgs.Empty);
        }

        IsLoggingIn = false;
    }
}