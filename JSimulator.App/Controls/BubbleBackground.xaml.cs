using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace JSimulator.App.Controls;

/// <summary>
/// Ambient background effect: soft, softly-glowing bubbles that continuously
/// drift upward with a gentle side-to-side sway. Purely decorative — sits
/// behind page content (see <c>MainWindow.xaml</c>) and never intercepts
/// input (<see cref="UIElement.IsHitTestVisible"/> is false throughout).
/// </summary>
public partial class BubbleBackground : UserControl
{
    private const int BubbleCount = 16;
    private static readonly Color[] Palette =
    {
        (Color)ColorConverter.ConvertFromString("#5B7CFA"), // accent blue
        (Color)ColorConverter.ConvertFromString("#5B7CFA"),
        (Color)ColorConverter.ConvertFromString("#2FCB84"), // success green, used sparingly
        (Color)ColorConverter.ConvertFromString("#6E7891"), // muted slate
    };

    private readonly Random _random = new();
    private double _lastWidth;
    private double _lastHeight;

    public BubbleBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => Spawn();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Only respawn on a meaningful resize (e.g. maximizing the window) —
        // small layout jitter shouldn't restart every bubble's animation.
        if (Math.Abs(e.NewSize.Width - _lastWidth) > 80 || Math.Abs(e.NewSize.Height - _lastHeight) > 80)
        {
            Spawn();
        }
    }

    private void Spawn()
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        _lastWidth = width;
        _lastHeight = height;

        RootCanvas.Children.Clear();
        for (int i = 0; i < BubbleCount; i++)
        {
            CreateBubble(width, height);
        }
    }

    private void CreateBubble(double width, double height)
    {
        double size = 14 + _random.NextDouble() * 90;
        double startX = _random.NextDouble() * width;
        double startDelayFraction = _random.NextDouble(); // spreads bubbles across the cycle instead of all starting at the bottom together
        double baseOpacity = 0.14 + _random.NextDouble() * 0.22;
        double riseSeconds = 16 + _random.NextDouble() * 20;
        double swaySeconds = 4 + _random.NextDouble() * 4;
        double swayAmplitude = 16 + _random.NextDouble() * 50;
        var color = Palette[_random.Next(Palette.Length)];

        // Radial gradient (opaque-ish center fading to transparent edge) gives a
        // soft, glassy look without a Gaussian blur, which is what made earlier
        // bubbles look like smudges instead of bubbles.
        var fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.32),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        fill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(baseOpacity * 255), color.R, color.G, color.B), 0.0));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(baseOpacity * 140), color.R, color.G, color.B), 0.6));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
        fill.Freeze();

        var bubble = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = fill,
            Stroke = new SolidColorBrush(Color.FromArgb((byte)(baseOpacity * 255), color.R, color.G, color.B)),
            StrokeThickness = 1
        };

        var sway = new TranslateTransform();
        bubble.RenderTransform = sway;

        Canvas.SetLeft(bubble, startX);
        Canvas.SetTop(bubble, height + size);
        RootCanvas.Children.Add(bubble);

        var riseAnimation = new DoubleAnimation
        {
            From = height + size,
            To = -size * 1.5,
            Duration = TimeSpan.FromSeconds(riseSeconds),
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(-riseSeconds * startDelayFraction)
        };
        Storyboard.SetTarget(riseAnimation, bubble);
        Storyboard.SetTargetProperty(riseAnimation, new PropertyPath("(Canvas.Top)"));

        var swayAnimation = new DoubleAnimation
        {
            From = -swayAmplitude,
            To = swayAmplitude,
            Duration = TimeSpan.FromSeconds(swaySeconds),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(swayAnimation, bubble);
        Storyboard.SetTargetProperty(swayAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        var storyboard = new Storyboard();
        storyboard.Children.Add(riseAnimation);
        storyboard.Children.Add(swayAnimation);
        storyboard.Begin();
    }
}
