using Avalonia;
using Avalonia.Reactive; // Although not explicitly using ReactiveUI features, it's often included by default templates
using System;
using System.Reflection.PortableExecutable;

namespace Dottle;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
    .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    // Commented out UseReactiveUI() as it's not strictly needed for this approach
    // .UseReactiveUI();
}