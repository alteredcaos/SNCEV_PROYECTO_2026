
using SNCEV.MobileApp.Services;
using SNCEV.MobileApp.Views;

namespace SNCEV.MobileApp;

public partial class App : Application
{
    private readonly AuthService _authService;

    public App(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        return new Window(
            new NavigationPage(
                new LoginPage(_authService)));
    }
}
