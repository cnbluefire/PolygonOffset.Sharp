using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PolygonOffset.Sample;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        List<Vector2> points = [new Vector2(100, 0), new Vector2(160, 180), new Vector2(10, 60), new Vector2(190, 60), new Vector2(40, 180)];
        var offset = PolygonOffsetHelper.OffsetPolygon(points, -10);

        ArgumentNullException.ThrowIfNull(offset);

        this.Content = new Path()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Data = new GeometryGroup()
            {
                Children =
                {
                    Geometry.Parse(ToPath(points)),
                    Geometry.Parse(ToPath(offset)),
                }
            }
        };

        static string ToPath(List<Vector2> vectors)
        {
            if (vectors.Count < 2) return string.Empty;

            var sb = new StringBuilder($"M{vectors[0].X},{vectors[0].Y}L");
            for (int i = 1; i < vectors.Count; i++)
            {
                sb.Append($"{vectors[i].X},{vectors[i].Y} ");
            }
            sb.Length--;
            sb.Append('Z');
            return sb.ToString();
        }
    }
}