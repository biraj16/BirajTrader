// TradingConsole.Wpf/Views/MtmGraphWindow.xaml.cs
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Linq;
using System.Windows;
using System.Windows.Media;
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

                Chart.Series = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = pnlValues,
                        Name = "MTM",
                        Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(50)),
                        GeometrySize = 0,
                        LineSmoothness = 0.5,
                        Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 2 }
                    }
                };

                Chart.XAxes = new[]
                {
                    new Axis
                    {
                        Labeler = value => new System.DateTime((long)value).ToString("hh:mm tt"),
                        UnitWidth = System.TimeSpan.FromMinutes(1).Ticks,
                        MinStep = System.TimeSpan.FromMinutes(5).Ticks,
                        LabelsPaint = new SolidColorPaint(SKColors.White)
                    }
                };

                Chart.YAxes = new[]
                {
                    new Axis
                    {
                        Labeler = value => value.ToString("C", new System.Globalization.CultureInfo("en-IN")),
                        LabelsPaint = new SolidColorPaint(SKColors.White)
                    }
                };
            }
        }
    }
}
