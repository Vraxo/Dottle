using Avalonia.Controls;
using Dottle.ViewModels; // Assuming ViewModels namespace

namespace Dottle.Views;

// Simple base class if needed, though often not strictly necessary
// if setting DataContext directly in constructors or App setup.
public abstract class ViewBase<TViewModel> : UserControl where TViewModel : ViewModelBase
{
    public TViewModel? ViewModel => DataContext as TViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Can add common logic here if needed when DataContext changes
    }
}