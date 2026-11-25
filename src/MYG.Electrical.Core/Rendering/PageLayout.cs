using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Rendering;

/// <summary>Sayfa çerçevesi ve antet ölçüleri (ISO 5457 / ISO 7200 esinli).</summary>
public static class PageLayout
{
    public const double BindingMargin = 20.0;
    public const double Margin = 10.0;
    public const double TitleBlockHeight = 24.0;
    public const double ColumnCount = 10;
    public const double ColumnStripHeight = 5.0;

    public static Rect2D Frame(Page page) =>
        new(BindingMargin, Margin, page.Width - BindingMargin - Margin, page.Height - 2 * Margin);

    public static Rect2D TitleBlock(Page page)
    {
        var f = Frame(page);
        return new Rect2D(f.Left, f.Bottom - TitleBlockHeight, f.Width, TitleBlockHeight);
    }

    /// <summary>X konumunun bulunduğu kolon (çerçevedeki şeritte 0–9 olarak yazılan).</summary>
    public static int ColumnAt(Page page, double x)
    {
        var f = Frame(page);
        var width = f.Width / ColumnCount;
        return (int)Math.Clamp(Math.Floor((x - f.Left) / width), 0, ColumnCount - 1);
    }

    /// <summary>Çizim yapılabilir alan (çerçeve içi, antet ve kolon şeridi hariç).</summary>
    public static Rect2D DrawingArea(Page page)
    {
        var f = Frame(page);
        return new Rect2D(f.Left, f.Top + ColumnStripHeight, f.Width, f.Height - TitleBlockHeight - ColumnStripHeight);
    }
}
