using Velopack;

namespace MYG.Electrical.App;

/// <summary>
/// Giriş noktası. Velopack kurulum, kaldırma ve güncelleme sonrası kancalarını WPF başlamadan çalıştırır
/// (bu durumlarda uygulama hiç açılmadan çıkar).
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
