using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace JSimulator.App.Controls;

/// <summary>
/// Ambient background effect: a slowly-rotating, low-poly 3D network of
/// glowing "node" gems joined by faint edges — evocative of a queuing
/// network (customers/servers connected by flow) without depicting one
/// literally. Purely decorative — sits behind page content (see
/// <c>MainWindow.xaml</c>), never intercepts input, and is built once on
/// load since the 3D scene is independent of pixel size.
/// </summary>
public partial class Network3DBackground : UserControl
{
    private const int NodeCount = 24;
    private const double FieldHalfWidth = 8.5;
    private const double FieldHalfHeight = 4.2;
    private const double FieldHalfDepth = 6.0;
    private const double MaxEdgeDistance = 5.2;
    private const int MaxEdgesPerNode = 2;

    private static readonly Color[] Palette =
    {
        (Color)ColorConverter.ConvertFromString("#5B7CFA"), // accent blue
        (Color)ColorConverter.ConvertFromString("#5B7CFA"),
        (Color)ColorConverter.ConvertFromString("#2FCB84"), // success green, used sparingly
        (Color)ColorConverter.ConvertFromString("#6E7891"), // muted slate
    };

    private readonly Random _random = new();
    private bool _built;

    // Shared, frozen geometry — every node/edge reuses these instead of
    // allocating its own mesh, which keeps the triangle-building work and
    // GPU upload cost fixed regardless of how many nodes are on screen.
    private static readonly MeshGeometry3D NodeGeometry = CreateIcosahedron(0.28);
    private static readonly MeshGeometry3D EdgeGeometry = CreateCylinder(0.02, 10);

    public Network3DBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        if (_built) return; // scene geometry doesn't depend on control size — build once
        _built = true;

        var group = new Model3DGroup();
        AddLights(group);

        var nodePositions = new Point3D[NodeCount];
        for (int i = 0; i < NodeCount; i++)
        {
            nodePositions[i] = new Point3D(
                (_random.NextDouble() * 2 - 1) * FieldHalfWidth,
                (_random.NextDouble() * 2 - 1) * FieldHalfHeight,
                (_random.NextDouble() * 2 - 1) * FieldHalfDepth);
        }

        AddEdges(group, nodePositions);
        AddNodes(group, nodePositions);

        var rootVisual = new ModelVisual3D { Content = group };
        var spinTransform = new Transform3DGroup();

        var ySpin = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        var yRotate = new RotateTransform3D(ySpin);
        var xTilt = new AxisAngleRotation3D(new Vector3D(1, 0, 0.15), 0);
        var xRotate = new RotateTransform3D(xTilt);

        spinTransform.Children.Add(yRotate);
        spinTransform.Children.Add(xRotate);
        rootVisual.Transform = spinTransform;

        SceneRoot.Children.Add(rootVisual);

        // Continuous slow spin around Y, plus a gentle back-and-forth tilt —
        // together these read as a lazily drifting 3D object rather than a
        // flat carousel, without ever settling into a repeating "seam".
        var spinAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(90),
            RepeatBehavior = RepeatBehavior.Forever
        };
        ySpin.BeginAnimation(AxisAngleRotation3D.AngleProperty, spinAnimation);

        var tiltAnimation = new DoubleAnimation
        {
            From = -7,
            To = 7,
            Duration = TimeSpan.FromSeconds(22),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        xTilt.BeginAnimation(AxisAngleRotation3D.AngleProperty, tiltAnimation);
    }

    private static void AddLights(Model3DGroup group)
    {
        group.Children.Add(new AmbientLight(Color.FromRgb(0x2A, 0x2E, 0x3D)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x6E, 0x86, 0xE8), new Vector3D(-1, -0.6, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x35, 0x3B, 0x4E), new Vector3D(1, 0.4, 1)));
    }

    private void AddNodes(Model3DGroup group, Point3D[] positions)
    {
        // A handful of shared, frozen materials (one per palette color) — nodes
        // pick one by reference rather than each building its own brush set.
        var materials = new Material[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            materials[i] = BuildNodeMaterial(Palette[i]);
        }

        foreach (var position in positions)
        {
            double scale = 0.7 + _random.NextDouble() * 0.9;
            var transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(scale, scale, scale));
            transform.Children.Add(new TranslateTransform3D((Vector3D)position));
            transform.Freeze();

            var model = new GeometryModel3D(NodeGeometry, materials[_random.Next(materials.Length)])
            {
                Transform = transform,
                BackMaterial = materials[_random.Next(materials.Length)]
            };
            model.Freeze();
            group.Children.Add(model);
        }
    }

    private void AddEdges(Model3DGroup group, Point3D[] positions)
    {
        var edgeMaterial = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(46, 0x8A, 0x9A, 0xC7)));
        edgeMaterial.Freeze();

        var drawn = new HashSet<(int, int)>();

        for (int i = 0; i < positions.Length; i++)
        {
            var distances = new List<(int Index, double Distance)>();
            for (int j = 0; j < positions.Length; j++)
            {
                if (i == j) continue;
                double distance = (positions[i] - positions[j]).Length;
                if (distance <= MaxEdgeDistance) distances.Add((j, distance));
            }

            distances.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            int added = 0;
            foreach (var (j, _) in distances)
            {
                if (added >= MaxEdgesPerNode) break;
                var key = i < j ? (i, j) : (j, i);
                if (!drawn.Add(key)) continue;

                AddEdge(group, positions[i], positions[j], edgeMaterial);
                added++;
            }
        }
    }

    private static void AddEdge(Model3DGroup group, Point3D from, Point3D to, Material material)
    {
        var direction = to - from;
        double length = direction.Length;
        if (length < 0.0001) return;

        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(1, length, 1));

        // The shared cylinder mesh runs along local +Y; rotate it onto the
        // actual edge direction, then translate its base to the start point.
        var axis = Vector3D.CrossProduct(new Vector3D(0, 1, 0), direction);
        if (axis.LengthSquared > 0.0001)
        {
            double angle = Vector3D.AngleBetween(new Vector3D(0, 1, 0), direction);
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(axis, angle)));
        }
        else if (direction.Y < 0)
        {
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180)));
        }

        transform.Children.Add(new TranslateTransform3D((Vector3D)from));
        transform.Freeze();

        var model = new GeometryModel3D(EdgeGeometry, material) { Transform = transform };
        model.Freeze();
        group.Children.Add(model);
    }

    private static Material BuildNodeMaterial(Color color)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B))));
        group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(190, color.R, color.G, color.B))));
        group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 48));
        group.Freeze();
        return group;
    }

    /// <summary>Regular icosahedron (20 triangular faces) — a cheap, recognizably
    /// "gem-like" low-poly sphere stand-in that looks deliberate rather than
    /// like a downgraded circle.</summary>
    private static MeshGeometry3D CreateIcosahedron(double radius)
    {
        double t = (1.0 + Math.Sqrt(5.0)) / 2.0;
        var raw = new[]
        {
            new Point3D(-1,  t,  0), new Point3D( 1,  t,  0), new Point3D(-1, -t,  0), new Point3D( 1, -t,  0),
            new Point3D( 0, -1,  t), new Point3D( 0,  1,  t), new Point3D( 0, -1, -t), new Point3D( 0,  1, -t),
            new Point3D( t,  0, -1), new Point3D( t,  0,  1), new Point3D(-t,  0, -1), new Point3D(-t,  0,  1),
        };

        double norm = Math.Sqrt(1 + t * t);
        double scale = radius / norm;

        var mesh = new MeshGeometry3D();
        foreach (var p in raw)
        {
            mesh.Positions.Add(new Point3D(p.X * scale, p.Y * scale, p.Z * scale));
        }

        int[] faces =
        {
            0, 11, 5,   0, 5, 1,   0, 1, 7,   0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,  11, 10, 2, 10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,   3, 2, 6,   3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,  6, 2, 10,  8, 6, 7,    9, 8, 1,
        };
        foreach (int index in faces)
        {
            mesh.TriangleIndices.Add(index);
        }

        mesh.Freeze();
        return mesh;
    }

    /// <summary>Unit-radius cylinder spanning local Y in [0, 1] — scaled, rotated
    /// and translated per-edge rather than rebuilt, so every edge shares one mesh.</summary>
    private static MeshGeometry3D CreateCylinder(double radius, int sides)
    {
        var mesh = new MeshGeometry3D();
        for (int i = 0; i < sides; i++)
        {
            double angle = i * 2 * Math.PI / sides;
            double x = radius * Math.Cos(angle);
            double z = radius * Math.Sin(angle);
            mesh.Positions.Add(new Point3D(x, 0, z));
            mesh.Positions.Add(new Point3D(x, 1, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int a = i * 2;
            int b = i * 2 + 1;
            int c = (i + 1) % sides * 2;
            int d = (i + 1) % sides * 2 + 1;

            mesh.TriangleIndices.Add(a);
            mesh.TriangleIndices.Add(b);
            mesh.TriangleIndices.Add(d);

            mesh.TriangleIndices.Add(a);
            mesh.TriangleIndices.Add(d);
            mesh.TriangleIndices.Add(c);
        }

        mesh.Freeze();
        return mesh;
    }
}
