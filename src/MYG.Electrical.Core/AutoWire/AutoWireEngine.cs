using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.AutoWire;

/// <summary>
/// EPLAN benzeri geometrik otomatik kablolama.
/// <para>Kural: iki pin yalnızca aynı yatay/dikey hatta (tolerans dahilinde) bulunuyor, birbirine bakıyor
/// ve <b>karşılıklı olarak</b> birbirlerinin en yakın uygun adayı ise bağlanır (mutual nearest).</para>
/// <para>Pin kimliğine göre eşleştirme yapılmaz; yalnızca konum ve yön kullanılır.</para>
/// <para>Yönlendirme: Direct Routing — iki pin arasında tek düz çizgi.</para>
/// </summary>
public sealed class AutoWireEngine
{
    public double Tolerance { get; }

    public AutoWireEngine(double tolerance = GeometryConstants.ConnectionToleranceMm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        Tolerance = tolerance;
    }

    /// <summary>Sayfadaki cihaz ve junction nesnelerinden pinleri toplar ve bağlantıları hesaplar.</summary>
    public IReadOnlyList<WireConnection> Compute(IEnumerable<DrawingObject> objects) => Compute(CollectPins(objects));

    public static IEnumerable<GlobalPin> CollectPins(IEnumerable<DrawingObject> objects)
    {
        foreach (var obj in objects)
        {
            var pins = obj switch
            {
                DeviceObject d => d.GetGlobalPins(),
                JunctionObject j => j.GetGlobalPins(),
                SinglePinObject s => s.GetGlobalPins(),
                _ => [],
            };
            foreach (var pin in pins)
            {
                yield return pin;
            }
        }
    }

    public IReadOnlyList<WireConnection> Compute(IEnumerable<GlobalPin> pins)
    {
        var all = pins as IList<GlobalPin> ?? pins.ToList();
        var result = new List<WireConnection>();

        // Yatay: Right → Left. Satır anahtarı Y, ilerleme ekseni X.
        Solve(all.Where(p => p.Direction is PinDirection.Right or PinDirection.Left),
            rowKey: p => p.Position.Y,
            axis: p => p.Position.X,
            isSource: p => p.Direction == PinDirection.Right,
            result);

        // Dikey: Down → Up. Sütun anahtarı X, ilerleme ekseni Y (Y aşağı doğru artar).
        Solve(all.Where(p => p.Direction is PinDirection.Down or PinDirection.Up),
            rowKey: p => p.Position.X,
            axis: p => p.Position.Y,
            isSource: p => p.Direction == PinDirection.Down,
            result);

        return result;
    }

    /// <summary>
    /// "Kaynak" pinler (Right/Down) eksende pozitif yöne, "hedef" pinler (Left/Up) negatif yöne bakar.
    /// Pinler önce aynı hat kümelerine ayrılır, sonra her küme içinde karşılıklı en yakın eşleşme aranır.
    /// </summary>
    private void Solve(IEnumerable<GlobalPin> pins, Func<GlobalPin, double> rowKey, Func<GlobalPin, double> axis,
        Func<GlobalPin, bool> isSource, List<WireConnection> result)
    {
        foreach (var row in ClusterRows(pins, rowKey))
        {
            var sources = row.Where(isSource).ToList();
            var targets = row.Where(p => !isSource(p)).ToList();
            if (sources.Count == 0 || targets.Count == 0)
            {
                continue;
            }

            var bestForSource = new Dictionary<PinRef, GlobalPin>();
            foreach (var s in sources)
            {
                if (TryFindNearest(s, targets, rowKey, p => axis(p) - axis(s), out var t))
                {
                    bestForSource[s.Ref] = t;
                }
            }

            foreach (var t in targets)
            {
                if (!TryFindNearest(t, sources, rowKey, p => axis(t) - axis(p), out var s))
                {
                    continue;
                }
                // Karşılıklılık: kaynağın en yakını da bu hedef olmalı.
                if (bestForSource.TryGetValue(s.Ref, out var back) && back.Ref == t.Ref)
                {
                    result.Add(new WireConnection(s.Ref, t.Ref, s.Position, t.Position));
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="from"/> pininin baktığı yönde, aynı hatta bulunan en yakın adayı bulur.
    /// <paramref name="forwardDistance"/> adayın baktığı yöndeki uzaklığı verir (negatifse arkada kalır).
    /// </summary>
    private bool TryFindNearest(GlobalPin from, List<GlobalPin> candidates, Func<GlobalPin, double> rowKey,
        Func<GlobalPin, double> forwardDistance, out GlobalPin nearest)
    {
        nearest = default;
        var found = false;
        var bestDistance = double.MaxValue;
        var fromRow = rowKey(from);

        foreach (var c in candidates)
        {
            if (c.OwnerId == from.OwnerId || Math.Abs(rowKey(c) - fromRow) > Tolerance)
            {
                continue;
            }
            var d = forwardDistance(c);
            if (d < -Tolerance)
            {
                continue; // aday arkada kalıyor: birbirlerine bakmıyorlar
            }
            if (!found || d < bestDistance - double.Epsilon || (Math.Abs(d - bestDistance) <= double.Epsilon && Compare(c, nearest) < 0))
            {
                nearest = c;
                bestDistance = d;
                found = true;
            }
        }
        return found;
    }

    /// <summary>Eşit uzaklıkta deterministik sonuç için sıralama.</summary>
    private static int Compare(GlobalPin a, GlobalPin b)
    {
        var c = a.OwnerId.CompareTo(b.OwnerId);
        return c != 0 ? c : string.CompareOrdinal(a.PinId, b.PinId);
    }

    /// <summary>Hat anahtarına göre sıralayıp tolerans içinde ardışık olanları aynı kümeye toplar.</summary>
    private IEnumerable<List<GlobalPin>> ClusterRows(IEnumerable<GlobalPin> pins, Func<GlobalPin, double> rowKey)
    {
        List<GlobalPin>? current = null;
        var lastKey = double.NaN;
        foreach (var p in pins.OrderBy(rowKey))
        {
            var key = rowKey(p);
            if (current is null || key - lastKey > Tolerance)
            {
                if (current is not null)
                {
                    yield return current;
                }
                current = [];
            }
            current.Add(p);
            lastKey = key;
        }
        if (current is not null)
        {
            yield return current;
        }
    }
}
