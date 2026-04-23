using System.Diagnostics;
using System.Text.Json;
namespace MzRubyConvGui;
public partial class MainForm
{
    private sealed class ZoomImageForm : Form
    {
        private readonly Bitmap image;
        private readonly Panel scrollPanel = new();
        private readonly PictureBox pictureBox = new();
        private readonly ToolStripLabel zoomLabel = new();
        private bool fitMode = true;
        private bool isPanning;
        private Point panStartMouse;
        private PointF panStartCenter;
        private PointF centerImagePoint;
        private double zoom = 1.0;

        public ZoomImageForm(string title, string path, Size initialSize, Bitmap? displayImage = null)
        {
            Text = $"{title} - {Path.GetFileName(path)}";
            Size = initialSize;
            MinimumSize = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;

            if (displayImage is not null)
            {
                image = new Bitmap(displayImage);
            }
            else
            {
                using var source = Image.FromFile(path);
                image = new Bitmap(source);
            }
            centerImagePoint = new PointF(image.Width / 2f, image.Height / 2f);

            var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            AddButton(toolStrip, "Fit", (_, _) => SetFit());
            AddButton(toolStrip, "100%", (_, _) => SetZoom(1.0));
            AddButton(toolStrip, "200%", (_, _) => SetZoom(2.0));
            AddButton(toolStrip, "-", (_, _) => SetZoom(zoom / 1.25));
            AddButton(toolStrip, "+", (_, _) => SetZoom(zoom * 1.25));
            zoomLabel.Alignment = ToolStripItemAlignment.Right;
            toolStrip.Items.Add(zoomLabel);

            scrollPanel.Dock = DockStyle.Fill;
            scrollPanel.AutoScroll = true;
            scrollPanel.BackColor = Color.Black;
            scrollPanel.MouseWheel += (_, e) => ZoomByWheel(e.Delta, scrollPanel.PointToScreen(e.Location));
            scrollPanel.MouseDown += (_, e) => BeginPan(e, scrollPanel.PointToScreen(e.Location));
            scrollPanel.MouseMove += (_, e) => ContinuePan(scrollPanel.PointToScreen(e.Location));
            scrollPanel.MouseUp += (_, _) => EndPan();
            scrollPanel.Click += (_, _) => scrollPanel.Focus();
            scrollPanel.DoubleClick += (_, _) => SetFit();

            pictureBox.Image = image;
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.MouseWheel += (_, e) => ZoomByWheel(e.Delta, pictureBox.PointToScreen(e.Location));
            pictureBox.MouseDown += (_, e) => BeginPan(e, pictureBox.PointToScreen(e.Location));
            pictureBox.MouseMove += (_, e) => ContinuePan(pictureBox.PointToScreen(e.Location));
            pictureBox.MouseUp += (_, _) => EndPan();
            pictureBox.Click += (_, _) => scrollPanel.Focus();
            pictureBox.DoubleClick += (_, _) => SetFit();
            scrollPanel.Controls.Add(pictureBox);

            Controls.Add(scrollPanel);
            Controls.Add(toolStrip);
            toolStrip.Dock = DockStyle.Top;

            Shown += (_, _) => SetFit();
            Resize += (_, _) =>
            {
                if (fitMode)
                {
                    ApplyFitZoom();
                }
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pictureBox.Image = null;
            image.Dispose();
            base.OnFormClosed(e);
        }

        private static void AddButton(ToolStrip toolStrip, string text, EventHandler handler)
        {
            var button = new ToolStripButton(text);
            button.Click += handler;
            toolStrip.Items.Add(button);
        }

        private void SetFit()
        {
            fitMode = true;
            ApplyFitZoom();
        }

        private void SetZoom(double value)
        {
            fitMode = false;
            var center = centerImagePoint;
            zoom = Math.Clamp(value, 0.1, 8.0);
            ApplyZoom();
            CenterOnImage(center);
        }

        private void ZoomByWheel(int delta, Point mousePoint)
        {
            fitMode = false;
            var clientPoint = scrollPanel.PointToClient(mousePoint);
            var imagePoint = ScreenToImagePoint(mousePoint);
            zoom = Math.Clamp(delta > 0 ? zoom * 1.1 : zoom / 1.1, 0.1, 8.0);
            ApplyZoom();
            CenterImagePointAtClient(imagePoint, clientPoint);
        }

        private void BeginPan(MouseEventArgs e, Point screenPoint)
        {
            if (e.Button != MouseButtons.Left || !CanPan())
            {
                return;
            }

            scrollPanel.Focus();
            isPanning = true;
            panStartMouse = screenPoint;
            panStartCenter = centerImagePoint;
            scrollPanel.Capture = true;
            pictureBox.Capture = true;
            scrollPanel.Cursor = Cursors.SizeAll;
            pictureBox.Cursor = Cursors.SizeAll;
        }

        private void ContinuePan(Point screenPoint)
        {
            if (!isPanning)
            {
                return;
            }

            var dx = screenPoint.X - panStartMouse.X;
            var dy = screenPoint.Y - panStartMouse.Y;
            CenterOnImage(new PointF(
                panStartCenter.X - (float)(dx / zoom),
                panStartCenter.Y - (float)(dy / zoom)));
        }

        private void EndPan()
        {
            if (!isPanning)
            {
                return;
            }

            isPanning = false;
            scrollPanel.Capture = false;
            pictureBox.Capture = false;
            scrollPanel.Cursor = CanPan() ? Cursors.Hand : Cursors.Default;
            pictureBox.Cursor = CanPan() ? Cursors.Hand : Cursors.Default;
        }

        private bool CanPan()
        {
            return pictureBox.Width > scrollPanel.ClientSize.Width || pictureBox.Height > scrollPanel.ClientSize.Height;
        }

        private void ApplyFitZoom()
        {
            if (image.Width <= 0 || image.Height <= 0)
            {
                return;
            }

            var available = scrollPanel.ClientSize;
            var widthZoom = available.Width / (double)image.Width;
            var heightZoom = available.Height / (double)image.Height;
            zoom = Math.Clamp(Math.Min(widthZoom, heightZoom), 0.1, 8.0);
            ApplyZoom();
            CenterOnImage(new PointF(image.Width / 2f, image.Height / 2f));
            fitMode = true;
        }

        private void ApplyZoom()
        {
            var width = Math.Max(1, (int)Math.Round(image.Width * zoom));
            var height = Math.Max(1, (int)Math.Round(image.Height * zoom));
            pictureBox.Size = new Size(width, height);
            PlaceImageForCenter();
            zoomLabel.Text = $"{zoom * 100:0}%";
            scrollPanel.Cursor = CanPan() ? Cursors.Hand : Cursors.Default;
            pictureBox.Cursor = CanPan() ? Cursors.Hand : Cursors.Default;
        }

        private PointF ScreenToImagePoint(Point screenPoint)
        {
            var picturePoint = pictureBox.PointToClient(screenPoint);
            var imageX = picturePoint.X / zoom;
            var imageY = picturePoint.Y / zoom;
            return new PointF(
                Math.Clamp((float)imageX, 0, image.Width),
                Math.Clamp((float)imageY, 0, image.Height));
        }

        private void CenterImagePointAtClient(PointF imagePoint, Point clientPoint)
        {
            var centerX = imagePoint.X - (float)((clientPoint.X - scrollPanel.ClientSize.Width / 2.0) / zoom);
            var centerY = imagePoint.Y - (float)((clientPoint.Y - scrollPanel.ClientSize.Height / 2.0) / zoom);
            CenterOnImage(new PointF(centerX, centerY));
        }

        private void CenterOnImage(PointF imagePoint)
        {
            centerImagePoint = new PointF(
                Math.Clamp(imagePoint.X, 0, image.Width),
                Math.Clamp(imagePoint.Y, 0, image.Height));
            PlaceImageForCenter();
        }

        private void PlaceImageForCenter()
        {
            pictureBox.Location = new Point(
                (int)Math.Round(scrollPanel.ClientSize.Width / 2.0 - centerImagePoint.X * zoom),
                (int)Math.Round(scrollPanel.ClientSize.Height / 2.0 - centerImagePoint.Y * zoom));
        }
    }

    private sealed class SyncedImageForm : Form
    {
        private readonly List<ImagePane> panes;
        private readonly ToolStripLabel zoomLabel = new();
        private bool fitMode = true;
        private bool isPanning;
        private ImagePane? panPane;
        private Point panStartMouse;
        private readonly Dictionary<ImagePane, PointF> panStartCenters = new();
        private double zoom = 1.0;

        public static SyncedImageForm CreatePair(string originalPath, string previewPath, Size initialSize, Bitmap? originalImage = null)
        {
            return new SyncedImageForm(
                $"Compare - {Path.GetFileName(originalPath)} / {Path.GetFileName(previewPath)}",
                [
                    new ImagePaneSpec("Original", originalPath, new RectangleF(0, 0, 1, 1), originalImage),
                    new ImagePaneSpec("Preview", previewPath, new RectangleF(0, 0, 1, 1))
                ],
                initialSize);
        }

        public static SyncedImageForm CreateSplit(string originalPath, string upperPath, string lowerPath, Size initialSize, Bitmap? originalImage = null)
        {
            return new SyncedImageForm(
                $"Compare Split - {Path.GetFileName(originalPath)}",
                [
                    new ImagePaneSpec("Original", originalPath, new RectangleF(0, 0, 1, 1), originalImage),
                    new ImagePaneSpec("Upper", upperPath, new RectangleF(0, 0, 1, 0.5f)),
                    new ImagePaneSpec("Lower", lowerPath, new RectangleF(0, 0.5f, 1, 0.5f))
                ],
                initialSize);
        }

        private SyncedImageForm(string title, IReadOnlyList<ImagePaneSpec> paneSpecs, Size initialSize)
        {
            Text = title;
            Size = initialSize;
            MinimumSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterParent;
            var sourceCanvasSize = GetImageSize(paneSpecs[0]);

            var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            AddButton(toolStrip, "Fit", (_, _) => SetFit());
            AddButton(toolStrip, "100%", (_, _) => SetZoom(1.0));
            AddButton(toolStrip, "200%", (_, _) => SetZoom(2.0));
            AddButton(toolStrip, "-", (_, _) => SetZoom(zoom / 1.25));
            AddButton(toolStrip, "+", (_, _) => SetZoom(zoom * 1.25));
            zoomLabel.Alignment = ToolStripItemAlignment.Right;
            toolStrip.Items.Add(zoomLabel);

            panes = paneSpecs
                .Select(spec => new ImagePane(spec.Title, spec.Path, spec.SourceRect, spec.Image, sourceCanvasSize, ZoomByWheel, BeginPan, ContinuePan, EndPan))
                .ToList();
            foreach (var pane in panes)
            {
                pane.FitRequested += (_, _) => SetFit();
            }

            Controls.Add(panes.Count == 3 ? BuildSplitLayout() : BuildPairLayout());
            Controls.Add(toolStrip);
            toolStrip.Dock = DockStyle.Top;

            Shown += (_, _) => SetFit();
            Resize += (_, _) =>
            {
                if (fitMode)
                {
                    ApplyFitZoom();
                }
            };
        }

        private Control BuildPairLayout()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = panes.Count,
                RowCount = 2
            };
            for (var i = 0; i < panes.Count; i++)
            {
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / panes.Count));
            }
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (var i = 0; i < panes.Count; i++)
            {
                table.Controls.Add(MakeLabel(panes[i].Title), i, 0);
                table.Controls.Add(panes[i].Panel, i, 1);
            }

            return table;
        }

        private Control BuildSplitLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var originalPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            originalPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            originalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            originalPanel.Controls.Add(MakeLabel(panes[0].Title), 0, 0);
            originalPanel.Controls.Add(panes[0].Panel, 0, 1);

            var splitPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            splitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            splitPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            splitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            splitPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            splitPanel.Controls.Add(MakeLabel(panes[1].Title), 0, 0);
            splitPanel.Controls.Add(panes[1].Panel, 0, 1);
            splitPanel.Controls.Add(MakeLabel(panes[2].Title), 0, 2);
            splitPanel.Controls.Add(panes[2].Panel, 0, 3);

            root.Controls.Add(originalPanel, 0, 0);
            root.Controls.Add(splitPanel, 1, 0);
            return root;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var pane in panes)
            {
                pane.Dispose();
            }
            base.OnFormClosed(e);
        }

        private static void AddButton(ToolStrip toolStrip, string text, EventHandler handler)
        {
            var button = new ToolStripButton(text);
            button.Click += handler;
            toolStrip.Items.Add(button);
        }

        private static Size GetImageSize(ImagePaneSpec spec)
        {
            if (spec.Image is not null)
            {
                return spec.Image.Size;
            }

            using var image = Image.FromFile(spec.Path);
            return image.Size;
        }

        private void SetFit()
        {
            fitMode = true;
            ApplyFitZoom();
        }

        private void SetZoom(double value)
        {
            fitMode = false;
            var centers = panes.ToDictionary(pane => pane, pane => pane.CenterSourcePoint);
            zoom = Math.Clamp(value, 0.1, 8.0);
            ApplyZoom();
            foreach (var pane in panes)
            {
                pane.CenterOnSource(centers[pane]);
            }
        }

        private void ZoomByWheel(ImagePane source, int delta, Point screenPoint)
        {
            fitMode = false;
            var sourcePoint = source.ScreenToSourcePoint(screenPoint);
            var clientPoint = source.Panel.PointToClient(screenPoint);
            var xRatio = source.Panel.ClientSize.Width <= 0 ? 0.5 : clientPoint.X / (double)source.Panel.ClientSize.Width;
            var yRatio = source.Panel.ClientSize.Height <= 0 ? 0.5 : clientPoint.Y / (double)source.Panel.ClientSize.Height;

            zoom = Math.Clamp(delta > 0 ? zoom * 1.1 : zoom / 1.1, 0.1, 8.0);
            ApplyZoom();
            foreach (var pane in panes)
            {
                pane.ScrollSourceToRatio(sourcePoint, xRatio, yRatio);
            }
        }

        private void BeginPan(ImagePane source, MouseEventArgs e, Point screenPoint)
        {
            if (e.Button != MouseButtons.Left || !source.CanPan)
            {
                return;
            }

            source.Panel.Focus();
            isPanning = true;
            panPane = source;
            panStartMouse = screenPoint;
            source.BeginCapture();
            panStartCenters.Clear();
            foreach (var pane in panes)
            {
                panStartCenters[pane] = pane.CenterSourcePoint;
            }
            foreach (var pane in panes)
            {
                pane.SetPanningCursor(true);
            }
        }

        private void ContinuePan(ImagePane source, Point screenPoint)
        {
            if (!isPanning || !ReferenceEquals(source, panPane))
            {
                return;
            }

            var dx = screenPoint.X - panStartMouse.X;
            var dy = screenPoint.Y - panStartMouse.Y;
            foreach (var pane in panes)
            {
                if (!panStartCenters.TryGetValue(pane, out var startCenter))
                {
                    continue;
                }

                pane.SetCenterFromDrag(startCenter, dx, dy);
            }
        }

        private void EndPan()
        {
            if (!isPanning)
            {
                return;
            }

            isPanning = false;
            panPane?.EndCapture();
            panPane = null;
            panStartCenters.Clear();
            foreach (var pane in panes)
            {
                pane.SetPanningCursor(false);
            }
        }

        private void ApplyFitZoom()
        {
            zoom = Math.Clamp(panes.Min(pane => pane.FitZoom), 0.1, 8.0);
            ApplyZoom();
            foreach (var pane in panes)
            {
                pane.CenterOnSource(new PointF(
                    pane.SourceRect.X + pane.SourceRect.Width / 2,
                    pane.SourceRect.Y + pane.SourceRect.Height / 2));
            }
            fitMode = true;
        }

        private void ApplyZoom()
        {
            foreach (var pane in panes)
            {
                pane.ApplyZoom(zoom);
            }
            zoomLabel.Text = $"{zoom * 100:0}%";
        }

        private sealed record ImagePaneSpec(string Title, string Path, RectangleF SourceRect, Bitmap? Image = null);

        private sealed class ImagePane : IDisposable
        {
            private readonly Bitmap image;
            private readonly PictureBox pictureBox = new();
            private readonly RectangleF sourceRect;
            private readonly Size sourceCanvasSize;
            private readonly Action<ImagePane, int, Point> wheelHandler;
            private readonly Action<ImagePane, MouseEventArgs, Point> mouseDownHandler;
            private readonly Action<ImagePane, Point> mouseMoveHandler;
            private readonly Action mouseUpHandler;
            private double zoom = 1.0;
            private PointF centerSourcePoint;

            public ImagePane(
                string title,
                string path,
                RectangleF sourceRect,
                Bitmap? sourceImage,
                Size sourceCanvasSize,
                Action<ImagePane, int, Point> wheelHandler,
                Action<ImagePane, MouseEventArgs, Point> mouseDownHandler,
                Action<ImagePane, Point> mouseMoveHandler,
                Action mouseUpHandler)
            {
                Title = title;
                this.sourceRect = sourceRect;
                this.sourceCanvasSize = sourceCanvasSize;
                centerSourcePoint = new PointF(sourceRect.X + sourceRect.Width / 2, sourceRect.Y + sourceRect.Height / 2);
                this.wheelHandler = wheelHandler;
                this.mouseDownHandler = mouseDownHandler;
                this.mouseMoveHandler = mouseMoveHandler;
                this.mouseUpHandler = mouseUpHandler;

                var specImage = sourceImage;
                if (specImage is not null)
                {
                    image = new Bitmap(specImage);
                }
                else
                {
                    using var source = Image.FromFile(path);
                    image = new Bitmap(source);
                }
                Panel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.Black,
                    AccessibleName = title
                };
                Panel.MouseWheel += (_, e) => wheelHandler(this, e.Delta, Panel.PointToScreen(e.Location));
                Panel.MouseDown += (_, e) => mouseDownHandler(this, e, Panel.PointToScreen(e.Location));
                Panel.MouseMove += (_, e) => mouseMoveHandler(this, Panel.PointToScreen(e.Location));
                Panel.MouseUp += (_, _) => mouseUpHandler();
                Panel.Click += (_, _) => Panel.Focus();
                Panel.DoubleClick += (_, _) => FitRequested?.Invoke(this, EventArgs.Empty);

                pictureBox.Image = image;
                pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox.MouseWheel += (_, e) => wheelHandler(this, e.Delta, pictureBox.PointToScreen(e.Location));
                pictureBox.MouseDown += (_, e) => mouseDownHandler(this, e, pictureBox.PointToScreen(e.Location));
                pictureBox.MouseMove += (_, e) => mouseMoveHandler(this, pictureBox.PointToScreen(e.Location));
                pictureBox.MouseUp += (_, _) => mouseUpHandler();
                pictureBox.Click += (_, _) => Panel.Focus();
                pictureBox.DoubleClick += (_, _) => FitRequested?.Invoke(this, EventArgs.Empty);
                Panel.Controls.Add(pictureBox);
            }

            public string Title { get; }

            public Panel Panel { get; }

            public event EventHandler? FitRequested;

            public RectangleF SourceRect => sourceRect;

            public PointF CenterSourcePoint => centerSourcePoint;

            public bool CanPan => pictureBox.Width > Panel.ClientSize.Width || pictureBox.Height > Panel.ClientSize.Height;

            public double FitZoom
            {
                get
                {
                    if (image.Width <= 0 || image.Height <= 0)
                    {
                        return 1.0;
                    }

                    return Math.Min(
                        Panel.ClientSize.Width / SourceDisplayWidth,
                        Panel.ClientSize.Height / SourceDisplayHeight);
                }
            }

            private double SourceDisplayWidth => Math.Max(1, sourceCanvasSize.Width * sourceRect.Width);

            private double SourceDisplayHeight => Math.Max(1, sourceCanvasSize.Height * sourceRect.Height);

            public Point ScrollOffset => new(-Panel.AutoScrollPosition.X, -Panel.AutoScrollPosition.Y);

            public void ApplyZoom(double value)
            {
                zoom = value;
                var width = Math.Max(1, (int)Math.Round(SourceDisplayWidth * zoom));
                var height = Math.Max(1, (int)Math.Round(SourceDisplayHeight * zoom));
                pictureBox.Size = new Size(width, height);
                PlaceImageForCenter(centerSourcePoint);
                SetPanningCursor(false);
            }

            public PointF ScreenToSourcePoint(Point screenPoint)
            {
                var picturePoint = pictureBox.PointToClient(screenPoint);
                return LocalToSourcePoint(
                    pictureBox.Width <= 0 ? 0 : picturePoint.X / (float)pictureBox.Width,
                    pictureBox.Height <= 0 ? 0 : picturePoint.Y / (float)pictureBox.Height);
            }

            public PointF GetCenterSourcePoint()
            {
                return CenterSourcePoint;
            }

            public void ScrollSourceToCenter(PointF sourcePoint)
            {
                CenterOnSource(sourcePoint);
            }

            public void ScrollSourceToRatio(PointF sourcePoint, double xRatio, double yRatio)
            {
                var clientPoint = new Point(
                    (int)Math.Round(Panel.ClientSize.Width * Math.Clamp(xRatio, 0, 1)),
                    (int)Math.Round(Panel.ClientSize.Height * Math.Clamp(yRatio, 0, 1)));
                var local = SourceToLocalPoint(sourcePoint);
                var centerLocal = new PointF(
                    local.X - (float)((xRatio - 0.5) * Panel.ClientSize.Width / Math.Max(1, pictureBox.Width)),
                    local.Y - (float)((yRatio - 0.5) * Panel.ClientSize.Height / Math.Max(1, pictureBox.Height)));
                CenterOnSource(LocalToSourcePoint(centerLocal.X, centerLocal.Y));
            }

            public void CenterOnSource(PointF sourcePoint)
            {
                centerSourcePoint = sourcePoint;
                PlaceImageForCenter(centerSourcePoint);
            }

            public void SetCenterFromDrag(PointF startCenter, int dx, int dy)
            {
                var local = SourceToLocalPoint(startCenter);
                local.X -= pictureBox.Width <= 0 ? 0 : dx / (float)pictureBox.Width;
                local.Y -= pictureBox.Height <= 0 ? 0 : dy / (float)pictureBox.Height;
                CenterOnSource(LocalToSourcePoint(local.X, local.Y));
            }

            public void BeginCapture()
            {
                Panel.Capture = true;
                pictureBox.Capture = true;
            }

            public void EndCapture()
            {
                Panel.Capture = false;
                pictureBox.Capture = false;
            }

            public void SetPanningCursor(bool panning)
            {
                var cursor = panning ? Cursors.SizeAll : CanPan ? Cursors.Hand : Cursors.Default;
                Panel.Cursor = cursor;
                pictureBox.Cursor = cursor;
            }

            public void Dispose()
            {
                pictureBox.Image = null;
                image.Dispose();
            }

            private PointF ClientToSourcePoint(Point clientPoint)
            {
                return LocalToSourcePoint(
                    pictureBox.Width <= 0 ? 0 : (clientPoint.X - pictureBox.Left) / (float)pictureBox.Width,
                    pictureBox.Height <= 0 ? 0 : (clientPoint.Y - pictureBox.Top) / (float)pictureBox.Height);
            }

            private PointF LocalToSourcePoint(float localX, float localY)
            {
                localX = Math.Clamp(localX, 0, 1);
                localY = Math.Clamp(localY, 0, 1);
                return new PointF(
                    Math.Clamp(sourceRect.X + sourceRect.Width * localX, 0, 1),
                    Math.Clamp(sourceRect.Y + sourceRect.Height * localY, 0, 1));
            }

            private PointF SourceToLocalPoint(PointF sourcePoint)
            {
                var localX = sourceRect.Width <= 0 ? 0 : (sourcePoint.X - sourceRect.X) / sourceRect.Width;
                var localY = sourceRect.Height <= 0 ? 0 : (sourcePoint.Y - sourceRect.Y) / sourceRect.Height;
                return new PointF(localX, localY);
            }

            private void PlaceImageForCenter(PointF sourcePoint)
            {
                var local = SourceToLocalPoint(sourcePoint);
                pictureBox.Location = new Point(
                    (int)Math.Round(Panel.ClientSize.Width / 2.0 - local.X * pictureBox.Width),
                    (int)Math.Round(Panel.ClientSize.Height / 2.0 - local.Y * pictureBox.Height));
                pictureBox.Visible = pictureBox.Bounds.IntersectsWith(Panel.ClientRectangle);
            }
        }
    }
}
