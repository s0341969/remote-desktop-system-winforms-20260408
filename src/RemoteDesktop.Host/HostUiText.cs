using System.Drawing;
using System.Windows.Forms;

namespace RemoteDesktop.Host;

internal static class HostUiText
{
    public static string Bi(string traditionalChinese, string english)
    {
        return $"{traditionalChinese}{Environment.NewLine}{english}";
    }

    public static string Window(string traditionalChinese, string english)
    {
        return $"{traditionalChinese} / {english}";
    }

    public static void ApplyButton(Button button, string traditionalChinese, string english, int minHeight = 44)
    {
        button.Text = Bi(traditionalChinese, english);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Height = Math.Max(button.Height, minHeight);
    }
}
