using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dottle.ViewModels;

public partial class ChangePasswordDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmChangeCommand))]
    private string? _oldPassword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmChangeCommand))]
    private string? _newPassword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmChangeCommand))]
    private string? _confirmNewPassword;

    [ObservableProperty]
    private string? _errorMessage;

    public delegate Task<bool> ChangePasswordDelegate(string oldPassword, string newPassword);

    private readonly ChangePasswordDelegate _changePasswordAction;

    public ChangePasswordDialogViewModel(ChangePasswordDelegate changePasswordAction)
    {
        _changePasswordAction = changePasswordAction;
    }

    private bool CanConfirmChange()
    {
        return !string.IsNullOrEmpty(OldPassword) &&
               !string.IsNullOrEmpty(NewPassword) &&
               NewPassword == ConfirmNewPassword;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmChange))]
    private async Task ConfirmChange()
    {
        ErrorMessage = null;

        if (string.IsNullOrEmpty(NewPassword) || NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New passwords do not match or are empty.";
            return; // Prevent closing dialog
        }

        if (NewPassword == OldPassword)
        {
            ErrorMessage = "New password cannot be the same as the old password.";
            return; // Prevent closing dialog
        }

        // The actual password validation and re-encryption is handled
        // by the delegate passed in from MainViewModel.
        // This command primarily validates input fields locally.
        // The dialog will be closed externally if this command completes without setting ErrorMessage.
        await Task.CompletedTask; // Placeholder if no async needed here, but keeps signature consistent
    }
}