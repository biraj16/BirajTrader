// TradingConsole.Wpf/Views/MtmGraphWindow.xaml.cs
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;
using System.Linq;
using System.Windows;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Views
{
    public partial class MtmGraphWindow : Window
    {
        public MtmGraphWindow(MtmGraphViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            if (viewModel.PnlHistory.Any())
            {
                var pnlValues = viewModel.PnlHistory.Select(p => new DateTimePoint(p.Timestamp, (double)p.Pnl)).ToList();
                var drawdownValues = viewModel.DrawdownHistory.Select(p => new DateTimePoint(p.Timestamp, (double)p.Pnl)).ToList();

                Chart.Series = new ISeries[]
                {
                    // --- NEW: Drawdown Series (Area Chart) ---
                    new LineSeries<DateTimePoint>
                    {
                        Values = drawdownValues,
                        Name = "Drawdown",
                        Fill = new SolidColorPaint(SKColors.IndianRed.WithAlpha(90)),
                        Stroke = null,
                        GeometrySize = 0,
                        LineSmoothness = 0.2
                    },
                    // --- REVAMPED: MTM Series (Line Chart) ---
                    new LineSeries<DateTimePoint>
                    {
                        Values = pnlValues,
                        Name = "MTM",
                        Fill = new LinearGradientPaint(new SKColor(45, 128, 210, 90), new SKColor(45, 128, 210, 10)),
                        Stroke = new SolidColorPaint(new SKColor(45, 128, 210)) { StrokeThickness = 3 },
                        GeometrySize = 0,
                        LineSmoothness = 0.6
                    }
                };

                Chart.XAxes = new[]
                {
                    new Axis
                    {
                        Labeler = value => new System.DateTime((long)value).ToString("hh:mm tt"),
                        UnitWidth = System.TimeSpan.FromMinutes(1).Ticks,
                        MinStep = System.TimeSpan.FromMinutes(5).Ticks,
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50))
                    }
                };

                Chart.YAxes = new[]
                {
                    new Axis
                    {
                        Labeler = value => value.ToString("C0", new System.Globalization.CultureInfo("en-IN")),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50))
                    }
                };

                // --- NEW: Add a title to the chart ---
                Chart.Title = new LabelVisual
                {
                    Text = "Intraday Performance",
                    TextSize = 20,
                    Paint = new SolidColorPaint(SKColors.White)
                };
            }
        }
    }
}
