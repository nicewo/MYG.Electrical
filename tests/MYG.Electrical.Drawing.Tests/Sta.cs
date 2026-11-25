using System.Runtime.ExceptionServices;

namespace MYG.Electrical.Drawing.Tests;

/// <summary>WPF nesneleri STA iş parçacığı gerektirir; test gövdesini STA'da çalıştırır.</summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
    }
}
