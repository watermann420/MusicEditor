using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Mixer
{
    /// <summary>
    /// Stereo VU meter visualization with peak hold indicators.
    /// </summary>
    public partial class MeterControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty LeftLevelProperty =
            DependencyProperty.Register(nameof(LeftLevel), typeof(double), typeof(MeterControl),
                new PropertyMetadata(0.0, OnLevelChanged, CoerceLevel));

        public static readonly DependencyProperty RightLevelProperty =
            DependencyProperty.Register(nameof(RightLevel), typeof(double), typeof(MeterControl),
                new PropertyMetadata(0.0, OnLevelChanged, CoerceLevel));

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(Level), typeof(double), typeof(MeterControl),
                new PropertyMetadata(0.0, OnMonoLevelChanged, CoerceLevel));

        public static readonly DependencyProperty PeakHoldTimeProperty =
            DependencyProperty.Register(nameof(PeakHoldTime), typeof(TimeSpan), typeof(MeterControl),
                new PropertyMetadata(TimeSpan.FromSeconds(1.5)));

        #endregion

        #region Properties

        /// <summary>
        /// Left channel level (0-1).
        /// </summary>
        public double LeftLevel
        {
            get => (double)GetValue(LeftLevelProperty);
            set => SetValue(LeftLevelProperty, value);
        }

        /// <summary>
        /// Right channel level (0-1).
        /// </summary>
        public double RightLevel
        {
            get => (double)GetValue(RightLevelProperty);
            set => SetValue(RightLevelProperty, value);
        }

        /// <summary>
        /// Mono level (0-1). Sets both left and right channels.
        /// </summary>
        public double Level
        {
            get => (double)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        /// <summary>
        /// Time to hold peak indicator before decay.
        /// </summary>
        public TimeSpan PeakHoldTime
        {
            get => (TimeSpan)GetValue(PeakHoldTimeProperty);
            set => SetValue(PeakHoldTimeProperty, value);
        }

        #endregion

        #region Private Fields

        private double _leftPeakLevel;
        private double _rightPeakLevel;
        private DateTime _leftPeakTime;
        private DateTime _rightPeakTime;

        private Rectangle? _leftMeterRect;
        private Rectangle? _rightMeterRect;
        private Rectangle? _leftPeakRect;
        private Rectangle? _rightPeakRect;

        private static readonly LinearGradientBrush MeterGradient;

        #endregion

        #region Static Constructor

        static MeterControl()
        {
            // Create the green/yellow/red gradient for meters
            MeterGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(0, 0)
            };
            MeterGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 200, 80), 0.0));   // Green
            MeterGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 200, 80), 0.6));   // Green
            MeterGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 200, 0), 0.75)); // Yellow
            MeterGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 80, 80), 0.9));  // Red
            MeterGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));    // Bright Red
            MeterGradient.Freeze();
        }

        #endregion

        #region Constructor

        public MeterControl()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeMeterElements();
            UpdateMeters();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMeters();
        }

        private static object CoerceLevel(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MeterControl meter)
            {
                meter.UpdateMeters();
            }
        }

        private static void OnMonoLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MeterControl meter)
            {
                var level = (double)e.NewValue;
                meter.LeftLevel = level;
                meter.RightLevel = level;
            }
        }

        #endregion

        #region Private Methods

        private void InitializeMeterElements()
        {
            // Clear existing elements
            LeftMeterCanvas.Children.Clear();
            RightMeterCanvas.Children.Clear();

            // Create meter rectangles
            _leftMeterRect = new Rectangle
            {
                Fill = MeterGradient,
                Width = LeftMeterCanvas.ActualWidth > 0 ? LeftMeterCanvas.ActualWidth : 15
            };
            LeftMeterCanvas.Children.Add(_leftMeterRect);

            _rightMeterRect = new Rectangle
            {
                Fill = MeterGradient,
                Width = RightMeterCanvas.ActualWidth > 0 ? RightMeterCanvas.ActualWidth : 15
            };
            RightMeterCanvas.Children.Add(_rightMeterRect);

            // Create peak hold indicators
            _leftPeakRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(0, 217, 255)), // Accent color
                Height = 3,
                Width = LeftMeterCanvas.ActualWidth > 0 ? LeftMeterCanvas.ActualWidth : 15
            };
            LeftMeterCanvas.Children.Add(_leftPeakRect);

            _rightPeakRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb(0, 217, 255)), // Accent color
                Height = 3,
                Width = RightMeterCanvas.ActualWidth > 0 ? RightMeterCanvas.ActualWidth : 15
            };
            RightMeterCanvas.Children.Add(_rightPeakRect);
        }

        private void UpdateMeters()
        {
            if (_leftMeterRect == null || _rightMeterRect == null ||
                _leftPeakRect == null || _rightPeakRect == null)
            {
                return;
            }

            var leftHeight = LeftMeterCanvas.ActualHeight;
            var rightHeight = RightMeterCanvas.ActualHeight;
            var leftWidth = LeftMeterCanvas.ActualWidth;
            var rightWidth = RightMeterCanvas.ActualWidth;

            if (leftHeight <= 0 || rightHeight <= 0)
            {
                return;
            }

            // Update meter widths
            _leftMeterRect.Width = leftWidth;
            _rightMeterRect.Width = rightWidth;
            _leftPeakRect.Width = leftWidth;
            _rightPeakRect.Width = rightWidth;

            // Update left meter
            var leftMeterHeight = LeftLevel * leftHeight;
            _leftMeterRect.Height = leftMeterHeight;
            Canvas.SetBottom(_leftMeterRect, 0);
            Canvas.SetTop(_leftMeterRect, leftHeight - leftMeterHeight);

            // Update right meter
            var rightMeterHeight = RightLevel * rightHeight;
            _rightMeterRect.Height = rightMeterHeight;
            Canvas.SetBottom(_rightMeterRect, 0);
            Canvas.SetTop(_rightMeterRect, rightHeight - rightMeterHeight);

            // Update peak holds
            UpdatePeakHold(ref _leftPeakLevel, ref _leftPeakTime, LeftLevel, _leftPeakRect, leftHeight);
            UpdatePeakHold(ref _rightPeakLevel, ref _rightPeakTime, RightLevel, _rightPeakRect, rightHeight);
        }

        private void UpdatePeakHold(ref double peakLevel, ref DateTime peakTime, double currentLevel,
            Rectangle peakRect, double canvasHeight)
        {
            var now = DateTime.Now;

            // Update peak if current level is higher
            if (currentLevel >= peakLevel)
            {
                peakLevel = currentLevel;
                peakTime = now;
            }
            // Decay peak after hold time
            else if (now - peakTime > PeakHoldTime)
            {
                peakLevel = Math.Max(currentLevel, peakLevel - 0.02);
            }

            // Position peak indicator
            var peakY = canvasHeight - (peakLevel * canvasHeight);
            Canvas.SetTop(peakRect, Math.Max(0, peakY - 1.5));
            peakRect.Visibility = peakLevel > 0.01 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Resets peak hold indicators.
        /// </summary>
        public void ResetPeaks()
        {
            _leftPeakLevel = 0;
            _rightPeakLevel = 0;
            UpdateMeters();
        }

        #endregion
    }
}
