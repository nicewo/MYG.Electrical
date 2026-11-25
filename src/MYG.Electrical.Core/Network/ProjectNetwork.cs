using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Network;

/// <summary>Bir potansiyel grubunun (anahtarlanan kontaklardan geçerek ulaşılan iletkenler) bilgisi.</summary>
public sealed record PotentialInfo(string Name, PotentialKind Kind, string Voltage);

/// <summary>Sayfadaki konum: sayfa sırası (1 tabanlı) ve kolon (çerçevedeki 0–9 şeridi).</summary>
public readonly record struct PageLocation(int PageNumber, int Column)
{
    public override string ToString() => $"/{PageNumber}.{Column}";
}

/// <summary>
/// Proje genelinde elektriksel bağlantı analizi (union-find).
/// <list type="bullet">
/// <item><b>Net</b>: kablolar, junction kolları, sayfa bağlantıları (aynı ad) ve sembollerin <i>kalıcı</i> iç
/// bağlantılarıyla birleşen pinler. Gerçek iletken; kablo numarası net başına verilir.</item>
/// <item><b>Potansiyel grubu</b>: net + <i>anahtarlanan</i> iç bağlantılar (kontak, kesici). Potansiyel adı
/// ve rengi bu grup boyunca geçer (+24V butondan sonra da +24V'tur).</item>
/// </list>
/// </summary>
public sealed class ProjectNetwork
{
    private readonly Dictionary<PinRef, int> _index = [];
    private readonly List<PinRef> _nodes = [];
    private int[] _net = [];
    private int[] _group = [];
    private readonly Dictionary<int, List<PotentialObject>> _groupSources = [];
    private readonly Dictionary<int, List<PotentialObject>> _netSources = [];

    private ProjectNetwork(ProjectDocument document, IReadOnlyDictionary<Page, IReadOnlyList<WireConnection>> wires)
    {
        Document = document;
        Wires = wires;
    }

    public ProjectDocument Document { get; }

    /// <summary>Sayfa başına hesaplanmış kablolar.</summary>
    public IReadOnlyDictionary<Page, IReadOnlyList<WireConnection>> Wires { get; }

    /// <summary>Konumu bilinen her nesnenin sayfası.</summary>
    public IReadOnlyDictionary<Guid, Page> PageOf => _pageOf;
    private readonly Dictionary<Guid, Page> _pageOf = [];

    public static ProjectNetwork Build(ProjectDocument document, ISymbolCatalog catalog,
        IReadOnlyDictionary<Page, IReadOnlyList<WireConnection>> wires)
    {
        var n = new ProjectNetwork(document, wires);
        n.Analyze(catalog);
        return n;
    }

    /// <summary>Tüm sayfalar için kabloları hesaplayıp ağı kurar.</summary>
    public static ProjectNetwork Build(ProjectDocument document, ISymbolCatalog catalog, AutoWireEngine? engine = null)
    {
        engine ??= new AutoWireEngine();
        var wires = document.Pages.ToDictionary(p => p, p => engine.Compute(p.Objects));
        return Build(document, catalog, wires);
    }

    private int Node(PinRef pin)
    {
        if (!_index.TryGetValue(pin, out var i))
        {
            i = _nodes.Count;
            _index[pin] = i;
            _nodes.Add(pin);
        }
        return i;
    }

    private void Analyze(ISymbolCatalog catalog)
    {
        var permanent = new List<(int, int)>();
        var switched = new List<(int, int)>();
        var links = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var potentials = new List<(int Node, PotentialObject Source)>();

        foreach (var page in Document.Pages)
        {
            foreach (var obj in page.Objects)
            {
                _pageOf[obj.Id] = page;
                switch (obj)
                {
                    case DeviceObject d:
                        foreach (var cp in d.ConnectionPoints)
                        {
                            Node(new PinRef(d.Id, cp.Id));
                        }
                        if (catalog.Find(d.SymbolId) is { } def)
                        {
                            foreach (var c in def.InternalConnections)
                            {
                                var pair = (Node(new PinRef(d.Id, c.A)), Node(new PinRef(d.Id, c.B)));
                                (c.Kind == InternalConnectionKind.Permanent ? permanent : switched).Add(pair);
                            }
                        }
                        break;
                    case JunctionObject j:
                        var arms = j.GetGlobalPins().Select(p => Node(p.Ref)).ToList();
                        for (var i = 1; i < arms.Count; i++)
                        {
                            permanent.Add((arms[0], arms[i]));
                        }
                        break;
                    case PotentialObject pot:
                        potentials.Add((Node(pot.Pin.Ref), pot));
                        break;
                    case PageLinkObject link:
                        var node = Node(link.Pin.Ref);
                        if (!string.IsNullOrWhiteSpace(link.Name))
                        {
                            if (!links.TryGetValue(link.Name.Trim(), out var list))
                            {
                                links[link.Name.Trim()] = list = [];
                            }
                            list.Add(node);
                        }
                        break;
                }
            }
            if (Wires.TryGetValue(page, out var pageWires))
            {
                foreach (var w in pageWires)
                {
                    permanent.Add((Node(w.From), Node(w.To)));
                }
            }
        }
        foreach (var list in links.Values)
        {
            for (var i = 1; i < list.Count; i++)
            {
                permanent.Add((list[0], list[i]));
            }
        }

        var netUf = new UnionFind(_nodes.Count);
        foreach (var (a, b) in permanent)
        {
            netUf.Union(a, b);
        }
        var groupUf = new UnionFind(_nodes.Count);
        foreach (var (a, b) in permanent.Concat(switched))
        {
            groupUf.Union(a, b);
        }
        _net = Enumerable.Range(0, _nodes.Count).Select(netUf.Find).ToArray();
        _group = Enumerable.Range(0, _nodes.Count).Select(groupUf.Find).ToArray();

        foreach (var (node, source) in potentials)
        {
            Add(_groupSources, _group[node], source);
            Add(_netSources, _net[node], source);
        }

        static void Add(Dictionary<int, List<PotentialObject>> map, int key, PotentialObject p)
        {
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = [];
            }
            list.Add(p);
        }
    }

    /// <summary>Pinin net kimliği; bilinmeyen pin için null.</summary>
    public int? NetOf(PinRef pin) => _index.TryGetValue(pin, out var i) ? _net[i] : null;

    public int? GroupOf(PinRef pin) => _index.TryGetValue(pin, out var i) ? _group[i] : null;

    /// <summary>Pinin bağlı olduğu potansiyel (grupta tek bir ad varsa). Çakışmada ilk kaynak döner.</summary>
    public PotentialInfo? PotentialOf(PinRef pin)
    {
        if (GroupOf(pin) is not { } g || !_groupSources.TryGetValue(g, out var sources) || sources.Count == 0)
        {
            return null;
        }
        var s = sources[0];
        return new PotentialInfo(s.Name, s.PotentialKind, s.Voltage);
    }

    public PotentialInfo? PotentialOf(WireConnection wire) => PotentialOf(wire.From) ?? PotentialOf(wire.To);

    /// <summary>Doğrudan bu nete bağlı potansiyel kaynakları (kontak üzerinden gelenler hariç).</summary>
    public IReadOnlyList<PotentialObject> NetSources(int net) =>
        _netSources.TryGetValue(net, out var list) ? list : [];

    /// <summary>Aynı nette bulunan pinler (aynı iletken).</summary>
    public IEnumerable<PinRef> PinsInNet(int net) =>
        _nodes.Where((_, i) => _net[i] == net);

    /// <summary>
    /// Farklı adlı potansiyellerin buluştuğu yerler. <c>Direct</c> = aynı nette (kesin kısa devre);
    /// aksi halde yalnızca bir kontak kapanınca birleşirler (olası kısa devre).
    /// </summary>
    public IEnumerable<(IReadOnlyList<PotentialObject> Sources, bool Direct)> PotentialConflicts()
    {
        var directGroups = new HashSet<int>();
        foreach (var sources in _netSources.Values.Where(s => DistinctNames(s) > 1))
        {
            directGroups.Add(_group[_index[sources[0].Pin.Ref]]);
            yield return (sources, true);
        }
        foreach (var (group, sources) in _groupSources)
        {
            if (!directGroups.Contains(group) && DistinctNames(sources) > 1)
            {
                yield return (sources, false);
            }
        }

        static int DistinctNames(List<PotentialObject> s) =>
            s.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    /// <summary>Sayfa bağlantısının karşı uçları (aynı adlı diğer bağlantıların sayfa/kolon konumu).</summary>
    public IReadOnlyList<PageLocation> LinkTargets(PageLinkObject link)
    {
        if (string.IsNullOrWhiteSpace(link.Name))
        {
            return [];
        }
        return Document.Pages
            .SelectMany(p => p.Objects.OfType<PageLinkObject>().Select(l => (Page: p, Link: l)))
            .Where(x => x.Link.Id != link.Id && string.Equals(x.Link.Name.Trim(), link.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => LocationOf(x.Page, x.Link.X))
            .ToList();
    }

    /// <summary>Nesnenin sayfa.kolon konumu.</summary>
    public PageLocation LocationOf(Page page, double x) =>
        new(Document.Pages.IndexOf(page) + 1, Rendering.PageLayout.ColumnAt(page, x));

    public PageLocation? LocationOf(DrawingObject obj) =>
        _pageOf.TryGetValue(obj.Id, out var page) ? LocationOf(page, obj.Anchor.X) : null;

    private sealed class UnionFind
    {
        private readonly int[] _parent;
        private readonly byte[] _rank;

        public UnionFind(int n)
        {
            _parent = new int[n];
            _rank = new byte[n];
            for (var i = 0; i < n; i++)
            {
                _parent[i] = i;
            }
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]];
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b)
            {
                return;
            }
            if (_rank[a] < _rank[b])
            {
                (a, b) = (b, a);
            }
            _parent[b] = a;
            if (_rank[a] == _rank[b])
            {
                _rank[a]++;
            }
        }
    }
}
