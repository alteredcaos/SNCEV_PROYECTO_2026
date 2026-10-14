
using SNCEV.MobileApp.Services;

namespace SNCEV.MobileApp.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;

    public LoginPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnLoginClicked(
        object sender,
        EventArgs e)
    {
        var usuario = TxtUsuario.Text?.Trim();
        var password = TxtPassword.Text;

        if (string.IsNullOrWhiteSpace(usuario) ||
            string.IsNullOrWhiteSpace(password))
        {
            LblMensaje.Text =
                "Introduce usuario y contraseña.";
            return;
        }

        BtnLogin.IsEnabled = false;
        Indicador.IsVisible = true;
        Indicador.IsRunning = true;
        LblMensaje.Text = "";

        try
        {
            bool correcto = await _authService.LoginAsync(
                usuario, password);

            if (correcto)
            {
                Application.Current!.Windows[0].Page =
                    new NavigationPage(new MainPage());
            }
            else
            {
                LblMensaje.Text =
                    "Credenciales incorrectas.";
            }
        }
        catch (Exception)
        {
            LblMensaje.Text =
                "No se pudo conectar con SNCEV.Api.";
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            Indicador.IsRunning = false;
            Indicador.IsVisible = false;
        }
    }
}
