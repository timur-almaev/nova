﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;

namespace ssi
{

    public delegate void AnnoTierChangeEventHandler(AnnoTier track, EventArgs e);

    public delegate void AnnoTierSegmentChangeEventHandler(AnnoTierLabel segment, EventArgs e);

    public delegate void AnnoTierResizeEventHandler(double pos);

    public partial class AnnoTierStatic : Canvas
    {
        static protected AnnoTier selectedTier = null;
        static protected AnnoTierLabel selectedLabel = null;

        static protected int selectedZindex = 0;
        static protected int selectedZindexMax = 0;
        static public double mouseDownPos;
        static public int closestIndex = -1;
        static public int closestIndexOld = 0;
        public static bool continuousAnnoMode = false;
        public static bool askForLabel = false;
        public static AnnoTierLabel objectContainer = null;

        static public event AnnoTierChangeEventHandler OnTierChange;

        static public event AnnoTierSegmentChangeEventHandler OnTierSegmentChange;

        static public event AnnoTierResizeEventHandler OnTrackResize;

        static public DispatcherTimer dispatcherTimer = new DispatcherTimer();

        static public AnnoTierLabel Label
        {
            get { return selectedLabel; }
        }

        static public AnnoTier Selected
        {
            get { return selectedTier; }            
        }

        public static bool CorrectMode { get; set; }


        static public void SelectTier(AnnoTier t)
        {
            UnselectTier();
            selectedTier = t;
            t.select(true);

            if (OnTierChange != null)
            {
                OnTierChange(selectedTier, null);
            }
        }

        static public void UnselectTier()
        {
            if (selectedTier != null)
            {
                selectedTier.select(false);
                selectedTier = null;
            }
        }

        static public void SelectLabel(AnnoTierLabel s)
        {
            UnselectLabel();
            if (s != null)
            {
                s.select(true);

                if (objectContainer == null)
                {
                    objectContainer = s;
                }
                selectedLabel = s;
                selectedZindex = Panel.GetZIndex(selectedLabel);
                Panel.SetZIndex(selectedLabel, selectedZindexMax + 1);

                if (OnTierSegmentChange != null)
                {
                    OnTierSegmentChange(s, null);
                }
            }
        }

        static public void UnselectLabel()
        {
            if (selectedLabel != null)
            {
                selectedLabel.select(false);
                Panel.SetZIndex(selectedLabel, selectedZindex);
                selectedLabel = null;
            }
        }

        static public void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.Delete) || e.KeyboardDevice.IsKeyDown(Key.Back) || (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) && e.KeyboardDevice.IsKeyDown(Key.X)))
            {
                if (selectedLabel != null && selectedTier != null/* && GetSelectedTrack().isDiscrete*/)
                {
                    AnnoTierLabel tmp = selectedLabel;
                    UnselectLabel();
                    selectedTier.remSegment(tmp);
                }
            }
        }

        static public void FireOnMove(double pos)
        {
            if (OnTrackResize != null)
            {
                OnTrackResize(pos);
            }
        }
    }

    public class AnnoTier : AnnoTierStatic, ITrack
    {
        #region Properties

        private AnnoTierUndoRedo _UnDoObject;

        public AnnoTierUndoRedo UnDoObject
        {
            get { return _UnDoObject; }
            set
            {
                _UnDoObject = value;
                //  UnDoObject.adornerevent += new EventHandler(UnDoObject_adornerevent);
            }
        }

        #endregion Properties

        private bool isSelected = false;
        public List<AnnoTierLabel> segments = new List<AnnoTierLabel>();

        private double currentPositionX = 0;
        public AnnoScheme.TYPE currentAnnoType = AnnoScheme.TYPE.FREE;

        public int lastLabelIndex;
        public string DefaultLabel = "";
        public Color DefaultColor = Colors.Black;
        private double dx = 0;
        private double lastX;
        private int direction;
        private bool annorightdirection = true;   
        private bool isMouseAlreadydown = false;

        private List<Line> continuousTierLines = new List<Line>();
        private List<Line> continuousTierMarkers = new List<Line>();
        private Ellipse continuousTierEllipse = new Ellipse();

        public AnnoList AnnoList { get; set; }

        public Brush BackgroundBrush { get; set; }
 
        public Brush ContinuousBrush { get; set; }


        public AnnoTier(AnnoList anno)
        {
            AnnoList = anno;

            AllowDrop = true;            
            SizeChanged += new SizeChangedEventHandler(sizeChanged);

            currentAnnoType = anno.Scheme.Type;
           
            UnDoObject = new AnnoTierUndoRedo();
            UnDoObject.Container = this;

            double mean = (anno.Scheme.MinScore + anno.Scheme.MaxScore) / 2;
            double range = anno.Scheme.MaxScore - anno.Scheme.MinScore;

            DefaultColor = Colors.Black;
            DefaultLabel = "";

            switch (anno.Scheme.Type)
            {
                case AnnoScheme.TYPE.DISCRETE:

                    if (AnnoList.Scheme.Labels.Count > 0)
                    {
                        DefaultColor = AnnoList.Scheme.Labels[0].Color;
                        DefaultLabel = AnnoList.Scheme.Labels[0].Name;
                    }
                    break;

                case AnnoScheme.TYPE.FREE:
                    
                    DefaultColor = AnnoList.Scheme.MaxOrForeColor;
                    if (AnnoList.Count > 0)
                    {
                        DefaultLabel = AnnoList[0].Label;
                    }
                    break;

                case AnnoScheme.TYPE.CONTINUOUS:

                    DefaultLabel = mean.ToString();
                    break;
            }

            AnnoTier.SelectTier(this);

            if (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE))
            {
                Loaded += delegate
                {
                    if (currentAnnoType == AnnoScheme.TYPE.CONTINUOUS)
                    {
                        InitContinousValues(anno.Scheme.SampleRate);
                        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(20);
                        dispatcherTimer.Tick += new EventHandler(delegate (object s, EventArgs a)
                        {
                            if (continuousAnnoMode && this.isSelected)
                            {
                                double closestposition = MainHandler.Time.CurrentPlayPosition;
                                closestIndex = GetClosestContinuousIndex(closestposition);
                                if (closestIndex > -1)
                                {
                                    if (this == Mouse.DirectlyOver || (Mouse.GetPosition(this).Y > 0 &&
                                        Mouse.GetPosition(this).Y < this.ActualHeight && continuousTierEllipse == Mouse.DirectlyOver))
                                    {
                                        double normal = 1.0 - (Mouse.GetPosition(this).Y / this.ActualHeight);
                                        double normalized = (normal * range) + anno.Scheme.MinScore;

                                        continuousTierEllipse.Height = this.ActualHeight / 10;
                                        continuousTierEllipse.Width = continuousTierEllipse.Height;
                                        continuousTierEllipse.SetValue(Canvas.TopProperty, (Mouse.GetPosition(this).Y - continuousTierEllipse.Height / 2));
                                        AnnoList[closestIndex].Label = (normalized).ToString();

                                        for (int i = closestIndexOld; i < closestIndex; i++)
                                        {
                                            if (closestIndexOld > -1) AnnoList[i].Label = (normalized).ToString();
                                        }
                                        closestIndexOld = closestIndex;

                                        TimeRangeChanged(MainHandler.Time);
                                    }
                                }
                            }
                            else continuousTierEllipse.Visibility = Visibility.Hidden;
                        });
                    }
                    else if (currentAnnoType == AnnoScheme.TYPE.POINT)
                    {
                        InitPointValues(anno);

                    }
                    else if (currentAnnoType == AnnoScheme.TYPE.POLYGON)
                    { }
                    else if (currentAnnoType == AnnoScheme.TYPE.GRPAH)
                    { }
                    else if (currentAnnoType == AnnoScheme.TYPE.SEGMENTATION)
                    { }
                };
            }
            selectedTier = this;

            foreach (AnnoListItem item in anno)
            {
                AnnoScheme.Label l = new AnnoScheme.Label(item.Label, item.Color);

                if (currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE)
                {
                    addSegment(item);
                }
            }
        }

        public void ExportToXPS(Uri path, Canvas surface)
        {
            if (path == null) return;

            Transform transform = surface.LayoutTransform;
            surface.LayoutTransform = null;

            Size size = new Size(surface.ActualWidth, surface.ActualHeight);
            surface.Measure(size);
            surface.Arrange(new Rect(size));

            Package package = Package.Open(path.LocalPath, FileMode.Create);
            XpsDocument doc = new XpsDocument(package);
            XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(doc);
            writer.Write(surface);
            doc.Close();
            package.Close();
            surface.LayoutTransform = transform;
        }

        public void ExportToPng(Uri path, Canvas surface)
        {
            if (path == null) return;
            Transform transform = surface.LayoutTransform;
            surface.LayoutTransform = null;
            Size size = new Size(surface.ActualWidth, surface.ActualHeight);
            surface.Measure(size);
            surface.Arrange(new Rect(size));
            RenderTargetBitmap renderBitmap =
              new RenderTargetBitmap(
                (int)size.Width,
                (int)size.Height,
                96d,
                96d,
                PixelFormats.Pbgra32);
            renderBitmap.Render(surface);

            using (FileStream outStream = new FileStream(path.LocalPath, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(outStream);
            }

            // Restore previously saved layout
            surface.LayoutTransform = transform;
        }

        private void sizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AnnoList.Scheme.Type == AnnoScheme.TYPE.DISCRETE || AnnoList.Scheme.Type == AnnoScheme.TYPE.FREE)
            {
                this.Visibility = Visibility.Hidden;
                foreach (AnnoTierLabel segment in segments)
                {
                    segment.Height = e.NewSize.Height;
                }
                this.Visibility = Visibility.Visible;
            }
            else
            {   //it has to be called twice, otherwise there are some weird effects.
                TimeRangeChanged(MainHandler.Time);
                TimeRangeChanged(MainHandler.Time);
            }
        }

        public void select(bool flag)
        {
            this.isSelected = flag;
            if (flag)
            {
                if (BackgroundBrush != null)
                {
                    if (this.currentAnnoType == AnnoScheme.TYPE.FREE || this.currentAnnoType == AnnoScheme.TYPE.DISCRETE)
                    {
                        byte newAlpha = 100;
                        Color newColor = Color.FromArgb(newAlpha, ((SolidColorBrush)BackgroundBrush).Color.R, ((SolidColorBrush)BackgroundBrush).Color.G, ((SolidColorBrush)BackgroundBrush).Color.B);
                        Brush brush = new SolidColorBrush(newColor);
                        this.Background = brush;
                    }
                    else if (ContinuousBrush != null && !(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE))
                    {
                        LinearGradientBrush myBrush = new LinearGradientBrush();
                        myBrush.StartPoint = new Point(0, 0);
                        myBrush.EndPoint = new Point(0, 1);
                        myBrush.GradientStops.Add(new GradientStop(((LinearGradientBrush)ContinuousBrush).GradientStops[0].Color, 0));
                        myBrush.GradientStops.Add(new GradientStop(((LinearGradientBrush)ContinuousBrush).GradientStops[1].Color, 1));
                        myBrush.Opacity = 0.6;
                        this.Background = myBrush;
                    }
                }
            }
            else
            {
                if (BackgroundBrush != null && (currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE))
                {
                    this.Background = BackgroundBrush;
                }
                else if (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE))
                {
                    if (ContinuousBrush == null)
                    {
                        LinearGradientBrush myBrush = new LinearGradientBrush();
                        myBrush.StartPoint = new Point(0, 0);
                        myBrush.EndPoint = new Point(0, 1);
                        myBrush.GradientStops.Add(new GradientStop(Colors.Blue, 0));
                        myBrush.GradientStops.Add(new GradientStop(Colors.Red, 1));
                        myBrush.Opacity = 0.75;
                        ContinuousBrush = myBrush;
                    }

                    this.Background = ContinuousBrush;
                }
            }
        }

        public AnnoTierLabel getSegment(AnnoListItem item)
        {
            foreach (AnnoTierLabel segment in segments)
            {
                if (segment.Item == item)
                {
                    return segment;
                }
            }
            return null;
        }

        public AnnoTierLabel addSegment(AnnoListItem item)
        {
            AnnoTierLabel segment = new AnnoTierLabel(item, this);
            segments.Add(segment);
            this.Children.Add(segment);
            selectedZindexMax = Math.Max(selectedZindexMax, Panel.GetZIndex(segment));

            return segment;
        }

        public void remSegment(AnnoTierLabel s)
        {
            ChangeRepresentationObject RememberDelete = UnDoObject.MakeChangeRepresentationObjectForDelete((FrameworkElement)s);
            UnDoObject.InsertObjectforUndoRedo(RememberDelete);
            deleteSegment(s);
        }

        public void deleteSegment(AnnoTierLabel s)
        {
            AnnoList.Remove(s.Item);
            s.Tier.Children.Remove(s);
            s.Tier.segments.Remove(s);
        }

        public void InitContinousValues(double sr)
        {
            //add markers
            for (int i = 0; i < 4; i++)
            {
                Line marker = new Line();
                marker.StrokeThickness = 1;
                marker.StrokeDashArray = new DoubleCollection() { 1.0, 7.0 };
                marker.Stroke = Brushes.DarkGray;
                marker.Y1 = (this.ActualHeight / 4) * i;
                marker.Y2 = marker.Y1;
                marker.X1 = 0;
                marker.X2 = this.ActualWidth;
                continuousTierMarkers.Add(marker);
                this.Children.Add(continuousTierMarkers[i]);
            }

            double mean = (AnnoList.Scheme.MinScore + AnnoList.Scheme.MaxScore) / 2.0;

            //add lines

            int samples = (int) Math.Round(MainHandler.Time.TotalDuration * sr);
            double delta = 1.0 / sr;
            if (AnnoList.Count < samples)
            {
                for (int i = AnnoList.Count; i < samples; i++)
                {
                    AnnoListItem ali = new AnnoListItem(i * delta, delta, mean.ToString("F4"), "", Colors.Black);
                    AnnoList.Add(ali);
                }
            }

            int drawlinesnumber;
            if (this.ActualWidth == 0) drawlinesnumber = 1000;
            else if (AnnoList.Count > this.ActualWidth) drawlinesnumber = (int) (this.ActualWidth);
            else drawlinesnumber = AnnoList.Count;

            for (int i = 0; i < drawlinesnumber; i++)
            {
                Line line = new Line();
                line.StrokeThickness = 1;
                line.Stroke = Brushes.Black;
                line.Y1 = mean;
                line.Y2 = line.Y1;
                line.X1 = MainHandler.Time.PixelFromTime(MainHandler.Time.TotalDuration / 1000 * i);
                line.X2 = MainHandler.Time.PixelFromTime(MainHandler.Time.TotalDuration / 1000 * i + MainHandler.Time.TotalDuration / 1000 * (i + 1));
                continuousTierLines.Add(line);
                selectedZindexMax = Math.Max(selectedZindexMax, Panel.GetZIndex(line));
                this.Children.Add(line);
            }

            continuousTierEllipse.Width = this.ActualHeight / 10;
            continuousTierEllipse.Height = continuousTierEllipse.Width;
            continuousTierEllipse.Fill = Brushes.WhiteSmoke;
            continuousTierEllipse.Stroke = Brushes.Black;
            continuousTierEllipse.Visibility = Visibility.Hidden;
            continuousTierEllipse.SetValue(Canvas.LeftProperty, 0.0);
            this.Children.Add(continuousTierEllipse);

            TimeRangeChanged(MainHandler.Time);
            TimeRangeChanged(MainHandler.Time);
        }

        public void InitPointValues(AnnoList anno)
        {
            double sr = anno.Scheme.SampleRate;
            int numPoints = Convert.ToInt32(anno.Scheme.MinScore);

            int samples = (int)Math.Round(MainHandler.Time.TotalDuration * sr * numPoints);
            double delta = 1.0 / sr;
            if (AnnoList.Count < samples)
            {
                for (int i = AnnoList.Count; i < samples; i++)
                {
                    Types.Point[] points = new Types.Point[numPoints];
                    for (int j = 0; j < numPoints; ++j)
                    {
                        points[j] = new Types.Point(0, 0, (j + 1).ToString());
                    }
                    AnnoListItem ali = new AnnoListItem(i * delta, delta, "Frame " + (i + 1).ToString(), "", anno.Scheme.MinOrBackColor, 1, true, points);
                    AnnoList.Add(ali);
                }
            }

            TimeRangeChanged(MainHandler.Time);
            //TimeRangeChanged(MainHandler.Time);
        }

        public void ContinuousAnnoMode()
        {
            if (!continuousAnnoMode)
            {
                dispatcherTimer.Start();
                continuousAnnoMode = true;

                closestIndex = -1;
                closestIndexOld = closestIndex;
            }
            else
            {
                dispatcherTimer.Stop();
                continuousAnnoMode = false;
            }
        }

        public void NewAnnoKey()
        {
            if (!CorrectMode)
            {
                if (currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE)
                {
                    double start = MainHandler.Time.TimeFromPixel(MainHandler.Time.CurrentSelectPosition);
                    double stop = MainHandler.Time.CurrentPlayPosition;

                    if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                    {
                        if (start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                        {
                            int round = (int)(start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                            start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                        }

                        if (stop % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                        {
                            int round = (int)(stop / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                            stop = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                        }
                    }

                    if (stop < start)
                    {
                        double temp = start;
                        start = stop;
                        stop = temp;
                    }

                    //  double stop = ViewHandler.Time.TimeFromPixel(e.GetPosition(this).X + AnnoTierSegment.RESIZE_OFFSET);
                    double len = stop - start;

                    double closestposition = start;
                    closestIndex = GetClosestContinuousIndex(closestposition);
                    closestIndexOld = closestIndex;

                    if ((currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) && stop < MainHandler.Time.TotalDuration)
                    {
                        AnnoListItem temp = new AnnoListItem(start, len, DefaultLabel, "", DefaultColor, 1.0);
                        temp.Color = DefaultColor;
                        AnnoList.AddSorted(temp);
                        AnnoTierLabel segment = new AnnoTierLabel(temp, this);

                        ChangeRepresentationObject ChangeRepresentationObjectforInsert = UnDoObject.MakeChangeRepresentationObjectForInsert(segment);
                        UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectforInsert);

                        annorightdirection = true;
                        segments.Add(segment);
                        Children.Add(segment);
                        SelectLabel(segment);
                    }
                }
            }
        }

        public void NewAnnoCopy(double start, double stop, string label, Color color, double confidence = 1.0)
        {
            if (!CorrectMode)
            {
                if (stop < start)
                {
                    double temp = start;
                    start = stop;
                    stop = temp;
                }
                if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                {
                    if (start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                    {
                        int round = (int)(start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                        start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                    }

                    if (stop % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                    {
                        int round = (int)(stop / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                        stop = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                    }
                }

                //  double stop = ViewHandler.Time.TimeFromPixel(e.GetPosition(this).X + AnnoTierSegment.RESIZE_OFFSET);
                double len = stop - start;

                double closestposition = start;
                closestIndex = GetClosestContinuousIndex(closestposition);
                closestIndexOld = closestIndex;
                
                if (AnnoList.Scheme.Type == AnnoScheme.TYPE.DISCRETE)
                {
                    label = DefaultLabel;
                    color = DefaultColor;
                }
                else if (AnnoList.Scheme.Type == AnnoScheme.TYPE.CONTINUOUS)
                {
                    label = "";                    
                    color = Colors.Black;
                }

                if (stop < MainHandler.Time.TotalDuration)
                {
                    AnnoListItem temp = new AnnoListItem(start, len, label, "", color, confidence);
                    temp.Color = this.DefaultColor;

                    bool alreadyinlist = false;
                    foreach (AnnoListItem ali in this.AnnoList)
                    {
                        if (ali.Start == temp.Start && ali.Stop == temp.Stop)
                        {
                            alreadyinlist = true;
                            break;
                        }
                    }

                    if (!alreadyinlist)
                    {
                        if (this.AnnoList.Scheme.Type != AnnoScheme.TYPE.CONTINUOUS) AnnoList.AddSorted(temp);
                        AnnoTierLabel segment = new AnnoTierLabel(temp, this);
                        annorightdirection = true;
                        ChangeRepresentationObject ChangeRepresentationObjectforInsert = UnDoObject.MakeChangeRepresentationObjectForInsert(segment);
                        UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectforInsert);
                        segments.Add(segment);
                        this.Children.Add(segment);
                        SelectLabel(segment);
                    }
                }
            }
        }

        public void LeftMouseButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            UnselectLabel();
            this.select(true);

            // change track
            if (selectedTier != this)
            {
                AnnoTier.SelectTier(this);
            }

            if ((currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) || (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) && Keyboard.IsKeyDown(Key.LeftShift)))
            {
                // check for segment selection

                foreach (AnnoTierLabel s in segments)
                {
                    if (s.IsMouseOver)
                    {
                        SelectLabel(s);
                        this.select(true);
                        break;
                    }
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            LeftMouseButtonDown(e);

        }

        public void RightMouseButtonDown(MouseButtonEventArgs e)
        {
            dx = 0;

            UnselectLabel();
            this.select(true);

            base.OnMouseRightButtonDown(e);
            if (!CorrectMode)
            {
                double start = MainHandler.Time.TimeFromPixel(e.GetPosition(this).X);

                if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                {
                    if (start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                    {
                        int round = (int)(start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                        start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                    }
                }

                double minsr = 0;
                if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                {
                    int factor = (int)(Properties.Settings.Default.DefaultMinSegmentSize / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                    minsr = (factor + 1) * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                }
                double stop = MainHandler.Time.TimeFromPixel(e.GetPosition(this).X) + Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                //  double stop = ViewHandler.Time.TimeFromPixel(e.GetPosition(this).X + AnnoTierSegment.RESIZE_OFFSET);
                double len = stop - start;
                double closestposition = MainHandler.Time.TimeFromPixel(e.GetPosition(this).X);
                closestIndex = GetClosestContinuousIndex(closestposition);
                closestIndexOld = closestIndex;

                if ((currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) && stop < MainHandler.Time.TotalDuration)
                {
                    AnnoListItem temp = new AnnoListItem(start, len, this.DefaultLabel, "", this.DefaultColor);
                    AnnoList.AddSorted(temp);
                    AnnoTierLabel segment = new AnnoTierLabel(temp, this);

                    segment.Width = 1;
                    annorightdirection = true;
                    ChangeRepresentationObject ChangeRepresentationObjectforInsert = UnDoObject.MakeChangeRepresentationObjectForInsert(segment);
                    UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectforInsert);
                    segments.Add(segment);

                    this.Children.Add(segment);
                    SelectLabel(segment);
                    this.select(true);

                    selectedLabel.Item.Duration = Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                    selectedLabel.Item.Stop = selectedLabel.Item.Start + selectedLabel.Item.Duration;
                }
                else if (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) && Keyboard.IsKeyDown(Key.LeftShift) && stop < MainHandler.Time.TotalDuration)
                {                    
                    AnnoListItem temp = new AnnoListItem(start, len, "", "", Colors.Black);
                    AnnoTierLabel segment = new AnnoTierLabel(temp, this);
                    segment.Width = 1;
                    annorightdirection = true;
                    segments.Add(segment);
                    this.Children.Add(segment);
                    SelectLabel(segment);
                    this.select(true);
                }
            }
        }

        public int GetClosestContinuousIndex(double nearestitem)
        {
            for (int i = 0; i < AnnoList.Count; i++)
            {
                if (AnnoList[i].Start - nearestitem < 1 / AnnoList.Scheme.SampleRate && AnnoList[i].Start - nearestitem > 0)
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            isMouseAlreadydown = false;
            base.OnMouseUp(e);


            double minsr = 0;

            if(Properties.Settings.Default.DefaultMinSegmentSize != 0 && Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
            { 
            int factor = (int)(Properties.Settings.Default.DefaultMinSegmentSize / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
            minsr = (factor) * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
            }

            if (selectedLabel != null && selectedLabel.Item.Duration < Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr))
            {
                if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0 && Properties.Settings.Default.DefaultMinSegmentSize != 0)
                {
                    int factor = (int)(Properties.Settings.Default.DefaultMinSegmentSize / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                    int round = (int)(selectedLabel.Item.Start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                    selectedLabel.Item.Start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                    selectedLabel.Item.Duration = Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                    selectedLabel.Item.Stop = selectedLabel.Item.Start + selectedLabel.Item.Duration;
                }
                else
                {
                    selectedLabel.Item.Duration = Properties.Settings.Default.DefaultMinSegmentSize;
                    selectedLabel.Item.Stop = selectedLabel.Item.Start + selectedLabel.Item.Duration;
                }

               
            }
        }

        public void MouseMove(MouseEventArgs e)
        {
            dx = e.GetPosition(Application.Current.MainWindow).X - lastX;

            direction = (dx > 0) ? 1 : 0;
            lastX = e.GetPosition(Application.Current.MainWindow).X;

            if ((currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) || (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE) && Keyboard.IsKeyDown(Key.LeftShift)))
            {
                if (e.RightButton == MouseButtonState.Pressed /*&& this.is_selected*/)

                {
                    Point point = e.GetPosition(selectedLabel);

                    if (selectedLabel != null)
                    {
                        this.select(true);
                        double delta = point.X - selectedLabel.ActualWidth;
                        if (annorightdirection)
                        {
                            //fight the rounding error
                            if (MainHandler.Time.PixelFromTime(selectedLabel.Item.Stop) > MainHandler.Time.CurrentSelectPosition && MainHandler.Time.PixelFromTime(selectedLabel.Item.Start) > MainHandler.Time.CurrentSelectPosition)
                            {
                                selectedLabel.Item.Start = MainHandler.Time.TimeFromPixel(MainHandler.Time.CurrentSelectPosition);
                            }
                            selectedLabel.resize_right(delta);

                            FireOnMove(selectedLabel.Item.Stop);
                            MainHandler.Time.CurrentPlayPosition = selectedLabel.Item.Stop;

                            if (MainHandler.Time.PixelFromTime(selectedLabel.Item.Stop) >= MainHandler.Time.CurrentSelectPosition - 1 && MainHandler.Time.PixelFromTime(selectedLabel.Item.Start) >= MainHandler.Time.CurrentSelectPosition - 1 && point.X < 0) annorightdirection = false;
                            SelectLabel(selectedLabel);
                            this.select(true);
                        }
                        else
                        {
                            delta = point.X;
                            //fight the rounding error
                            if (MainHandler.Time.PixelFromTime(selectedLabel.Item.Stop) < MainHandler.Time.CurrentSelectPosition && MainHandler.Time.PixelFromTime(selectedLabel.Item.Start) < MainHandler.Time.CurrentSelectPosition)
                            {
                                selectedLabel.Item.Stop = MainHandler.Time.TimeFromPixel(MainHandler.Time.CurrentSelectPosition);
                            }
                            selectedLabel.resize_left(delta);
                            FireOnMove(selectedLabel.Item.Start);
                            MainHandler.Time.CurrentPlayPosition = selectedLabel.Item.Start;

                            if ((MainHandler.Time.PixelFromTime(selectedLabel.Item.Start) > MainHandler.Time.CurrentSelectPosition - 1)) annorightdirection = true;
                            SelectLabel(selectedLabel);
                            this.select(true);
                        }


                        if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                        {
                            if (selectedLabel.Item.Start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                            {
                                int round = (int)(selectedLabel.Item.Start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                selectedLabel.Item.Start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                            }

                            if (selectedLabel.Item.Stop % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                            {
                                int round = (int)(selectedLabel.Item.Stop / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                selectedLabel.Item.Stop = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                            }
                            SelectLabel(selectedLabel);
                        }
                    }
                }

                if (selectedLabel != null && this.isSelected)
                {
                    this.select(true);
                    Point point = e.GetPosition(selectedLabel);

                    // check if use wants to resize/move

                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        double segmentwidth = point.X * (selectedLabel.Item.Duration / selectedLabel.ActualWidth);

                        // resize segment right
                        if (selectedLabel.isResizeableRight)
                        {
                            double delta = point.X - selectedLabel.ActualWidth;
                            double pos = GetLeft(selectedLabel);

                            if (isMouseAlreadydown == false)
                            {
                                ChangeRepresentationObject ChangeRepresentationObjectOfResize = UnDoObject.MakeChangeRepresentationObjectForResize(pos, (FrameworkElement)selectedLabel);
                                UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectOfResize);
                                isMouseAlreadydown = true;
                                SelectLabel(selectedLabel);
                            }

                            double minsr = 0;

                            if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                            {
                                int factor = (int)(Properties.Settings.Default.DefaultMinSegmentSize / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                minsr = (factor) * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                            }

                            if (segmentwidth >= Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr))
                            {
                                if (point.X > MainHandler.Time.PixelFromTime(MainHandler.Time.SelectionStop)) delta = MainHandler.Time.PixelFromTime(MainHandler.Time.SelectionStop) - selectedLabel.ActualWidth;

                                selectedLabel.resize_right(delta);

                                SelectLabel(selectedLabel);
                                this.select(true);
                                FireOnMove(selectedLabel.Item.Stop);
                            }
                            else
                            {
                                selectedLabel.Item.Duration = Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                                selectedLabel.Item.Stop = selectedLabel.Item.Start + selectedLabel.Item.Duration;
                                SelectLabel(selectedLabel);
                                this.select(true);
                            }

                            if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                            {
                                if (selectedLabel.Item.Stop % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                                {
                                    int round = (int)(selectedLabel.Item.Stop / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                    selectedLabel.Item.Stop = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                                    SelectLabel(selectedLabel);
                                }
                            }
                        }
                        // resize segment left
                        else if (selectedLabel.isResizeableLeft)
                        {
                            double pos = GetLeft(selectedLabel);
                            if (isMouseAlreadydown == false)
                            {
                                ChangeRepresentationObject ChangeRepresentationObjectOfResize = UnDoObject.MakeChangeRepresentationObjectForResize(pos, (FrameworkElement)selectedLabel);
                                UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectOfResize);
                                isMouseAlreadydown = true;
                            }

                            double delta = point.X;

                            double minsr = 0;
                            if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                            {
                                int factor = (int)(Properties.Settings.Default.DefaultMinSegmentSize / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate));
                                minsr = (factor + 1) * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                            }

                            if (selectedLabel.Item.Duration - segmentwidth >= Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr))
                            {
                                selectedLabel.resize_left(delta);
                                SelectLabel(selectedLabel);
                                this.select(true);
                                if (selectedLabel != null) FireOnMove(selectedLabel.Item.Start);
                            }
                            else
                            {
                                selectedLabel.Item.Start = selectedLabel.Item.Stop - Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                                selectedLabel.Item.Duration = Math.Max(Properties.Settings.Default.DefaultMinSegmentSize, minsr);
                                SelectLabel(selectedLabel);
                            }

                            if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                            {
                                if (selectedLabel.Item.Start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                                {
                                    int round = (int)(selectedLabel.Item.Start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                    selectedLabel.Item.Start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                                    SelectLabel(selectedLabel);
                                }
                            }
                        }

                        // move segment
                        else if (selectedLabel.isMoveable)
                        {
                            double pos = GetLeft(selectedLabel);
                            double delta = point.X - selectedLabel.ActualWidth / 2;
                            if (pos + delta >= 0 && pos + selectedLabel.ActualWidth + delta <= this.ActualWidth)
                            {
                                if (isMouseAlreadydown == false)
                                {
                                    ChangeRepresentationObject ChangeRepresentationObjectOfMove = UnDoObject.MakeChangeRepresentationObjectForMove(pos, (FrameworkElement)selectedLabel);
                                    UnDoObject.InsertObjectforUndoRedo(ChangeRepresentationObjectOfMove);
                                    isMouseAlreadydown = true;
                                   
                                }
                                selectedLabel.move(delta);
                                SelectLabel(selectedLabel);

                                FireOnMove(selectedLabel.Item.Start + (selectedLabel.Item.Stop - selectedLabel.Item.Start) * 0.5);
                            }

                            if (Properties.Settings.Default.DefaultDiscreteSampleRate != 0)
                            {
                                if (selectedLabel.Item.Start % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                                {
                                    int round = (int)(selectedLabel.Item.Start / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                    selectedLabel.Item.Start = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                                }

                                if (selectedLabel.Item.Stop % (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) != 0)
                                {
                                    int round = (int)(selectedLabel.Item.Stop / (1 / Properties.Settings.Default.DefaultDiscreteSampleRate) + 0.5);
                                    selectedLabel.Item.Stop = round * (1 / Properties.Settings.Default.DefaultDiscreteSampleRate);
                                }
                                SelectLabel(selectedLabel);
                            }
                        }
                    }
                    else
                    {
                        isMouseAlreadydown = false;

                        // check if use can resize/move
                        selectedLabel.checkResizeable(point);
                    }
                }
            }
            else if (AnnoList.Scheme.Type == AnnoScheme.TYPE.CONTINUOUS && !Keyboard.IsKeyDown(Key.LeftAlt))
            {
                if (continuousAnnoMode) continuousTierEllipse.Visibility = Visibility.Visible;
                else continuousTierEllipse.Visibility = Visibility.Hidden;

                if (e.RightButton == MouseButtonState.Pressed && this.isSelected)
                {
                    double deltaDirection = currentPositionX - e.GetPosition(this).X;
                    currentPositionX = e.GetPosition(this).X;

                    if (deltaDirection < 0)
                    {
                        double closestposition = MainHandler.Time.TimeFromPixel(e.GetPosition(this).X);
                        closestIndex = GetClosestContinuousIndex(closestposition);
                        if (closestIndex > -1)
                        {
                            double range = AnnoList.Scheme.MaxScore - AnnoList.Scheme.MinScore;
                            double normal = 1.0 - ((e.GetPosition(this).Y / this.ActualHeight));
                            double normalized = (normal * range) + AnnoList.Scheme.MinScore;
                            AnnoList[closestIndex].Label = normalized.ToString();

                            for (int i = closestIndexOld; i < closestIndex; i++)
                            {
                                if (AnnoList[i].Confidence < Properties.Settings.Default.UncertaintyLevel && CorrectMode == true)
                                {
                                    AnnoList[i].Label = normalized.ToString();
                                }
                                else if (CorrectMode == false) AnnoList[i].Label = normalized.ToString();
                            }
                            closestIndexOld = closestIndex;
                            TimeRangeChanged(MainHandler.Time);
                            //  nicer drawing but slower
                            if (!(currentAnnoType == AnnoScheme.TYPE.FREE || currentAnnoType == AnnoScheme.TYPE.DISCRETE))
                            {
                                TimeRangeChanged(MainHandler.Time);
                            }
                        }
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            MouseMove(e);
        }

        public void TimeRangeChanged(Timeline time)
        {
            this.Width = time.SelectionInPixel;

            //segments can happen in both, discrete and continuous annotations, so we check them in any case
            foreach (AnnoTierLabel s in segments)
            {
                s.Visibility = Visibility.Collapsed;
                if (s.Item.Start >= time.SelectionStart && s.Item.Start <= time.SelectionStop)
                {
                    s.update();
                    //if (s.Item.Confidence < Properties.Settings.Default.UncertaintyLevel /*&& CorrectMode == true*/) s.Visibility = Visibility.Visible;
                    s.Visibility = Visibility.Visible;
                }
                else if (s.Item.Stop >= time.SelectionStart && s.Item.Start <= time.SelectionStop)
                {
                    s.update2();
                    //if (s.Item.Confidence < Properties.Settings.Default.UncertaintyLevel /*&& CorrectMode == true*/) s.Visibility = Visibility.Visible;
                    s.Visibility = Visibility.Visible;
                }
            }

            if (this.AnnoList.Scheme.Type == AnnoScheme.TYPE.CONTINUOUS)
            {
                if (this.ActualHeight > 0)
                {
                    //markers

                    if (continuousTierMarkers.Count > 0)
                    {
                        for (int i = 0; i < continuousTierMarkers.Count; i++)
                        {
                            if (i == continuousTierMarkers.Count / 2)
                            {
                                continuousTierMarkers[i].StrokeDashArray = new DoubleCollection() { 2.0, 5.0 };
                                continuousTierMarkers[i].StrokeThickness = 1.5;
                            }
                            continuousTierMarkers[i].Y1 = (this.ActualHeight / continuousTierMarkers.Count) * i;
                            continuousTierMarkers[i].Y2 = continuousTierMarkers[i].Y1;
                            continuousTierMarkers[i].X1 = 0;
                            continuousTierMarkers[i].X2 = this.ActualWidth;
                        }
                    }

                    double timeRange = time.SelectionStop - time.SelectionStart;

                    int linesinrangenum = 0;
                    foreach (AnnoListItem ali in AnnoList)
                    {
                        if (ali.Start >= time.SelectionStart && ali.Stop <= time.SelectionStop)
                        {
                            linesinrangenum++;
                        }
                    }

                    if (linesinrangenum <= continuousTierLines.Count)
                    {
                        foreach (Line l in continuousTierLines)
                        {
                            l.Visibility = Visibility.Hidden;
                        }

                        int i = 0;
                        foreach (AnnoListItem ali in AnnoList)
                        {
                            if (ali.Start >= time.SelectionStart && ali.Stop < time.SelectionStop)
                            {
                                continuousTierLines[i % continuousTierLines.Count].X1 = MainHandler.Time.PixelFromTime(ali.Start);
                                if (i % continuousTierLines.Count < continuousTierLines.Count - 1 && ali.Stop < time.SelectionStop - ali.Duration) continuousTierLines[i % continuousTierLines.Count].X2 = continuousTierLines[i % continuousTierLines.Count + 1].X1;
                                else continuousTierLines[i % continuousTierLines.Count].X2 = MainHandler.Time.PixelFromTime(ali.Stop);

                                if (ali.Label == "") ali.Label = "0.5";
                                double value = 0.0;
                                double.TryParse(ali.Label, out value);
                                double range = AnnoList.Scheme.MaxScore - AnnoList.Scheme.MinScore;
                                value = 1.0 - (value - AnnoList.Scheme.MinScore) / range;

                                continuousTierLines[i % continuousTierLines.Count].Y1 = (value) * this.ActualHeight;
                                if (i % continuousTierLines.Count < continuousTierLines.Count - 1 && ali.Stop < time.SelectionStop - ali.Duration) continuousTierLines[i % continuousTierLines.Count].Y2 = continuousTierLines[i % continuousTierLines.Count + 1].Y1;
                                else if (i > 0) continuousTierLines[i % continuousTierLines.Count].Y2 = continuousTierLines[i % continuousTierLines.Count - 1].Y1;
                                if (ali.Confidence < Properties.Settings.Default.UncertaintyLevel && CorrectMode == true) continuousTierLines[i % continuousTierLines.Count].Visibility = Visibility.Visible;
                                else if (CorrectMode == false) continuousTierLines[i % continuousTierLines.Count].Visibility = Visibility.Visible;
                                else continuousTierLines[i % continuousTierLines.Count].Visibility = Visibility.Collapsed;

                                i++;
                            }
                        }
                    }
                    else
                    {
                        int i = 0;
                        int index = 0;

                        foreach (Line s in continuousTierLines)
                        {
                            s.Visibility = Visibility.Hidden;

                            index = (int)((double)AnnoList.Count / (double)continuousTierLines.Count * (double)i + 0.5f);
                            if (index > AnnoList.Count) index = AnnoList.Count - 1;

                            if (AnnoList[index].Start >= time.SelectionStart && AnnoList[index].Stop <= time.SelectionStop)
                            {
                                int offset = (int)((double)AnnoList.Count / (double)continuousTierLines.Count + 0.5f);
                                s.X1 = MainHandler.Time.PixelFromTime(AnnoList[index].Start);
                                if (i < continuousTierLines.Count - 1 && AnnoList[index + offset].Stop <= time.SelectionStop) s.X2 = continuousTierLines[i + 1].X1;
                                else s.X2 = MainHandler.Time.PixelFromTime(AnnoList[index].Start);

                                double median = 0;

                                double range = AnnoList.Scheme.MaxScore - AnnoList.Scheme.MinScore;
                                if (index > 0)
                                {
                                    for (int k = index - offset; k < index + offset; k++)
                                    {
                                        double value = 0;
                                        double.TryParse(AnnoList[k].Label, out value);

                                        value = 1.0 - (value - AnnoList.Scheme.MinScore) / range;

                                        median = median + value;
                                    }
                                    median = median / (2 * offset);
                                    s.Y1 = (median) * this.ActualHeight;
                                }
                                else
                                {
                                    double value = median;
                                    double.TryParse(AnnoList[index].Label, out value);
                                    value = 1.0 - (value - AnnoList.Scheme.MinScore) / range;
                                    s.Y1 = (value) * this.ActualHeight;
                                }
                                if (i < continuousTierLines.Count - 1 && AnnoList[index + offset].Stop <= time.SelectionStop) s.Y2 = continuousTierLines[i + 1].Y1;
                                else s.Y2 = s.Y1;

                                if (AnnoList[index].Confidence < Properties.Settings.Default.UncertaintyLevel && CorrectMode == true) s.Visibility = Visibility.Visible;
                                else if (CorrectMode == false) s.Visibility = Visibility.Visible;
                                else s.Visibility = Visibility.Collapsed;
                            }
                            i++;
                        }
                    }
                }
            }
        }
    }
}