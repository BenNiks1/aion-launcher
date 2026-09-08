using System.Diagnostics;

namespace AionLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception e)
        {
            // Better a readable box than a silent exit: most players have no console to look at.
            MessageBox.Show(
                e.ToString(),
                "Лаунчер не смог запуститься",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Debug.WriteLine(e);
        }
    }
}
