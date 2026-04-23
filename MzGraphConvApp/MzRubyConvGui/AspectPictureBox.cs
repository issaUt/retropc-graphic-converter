using System.Drawing.Drawing2D;

namespace MzRubyConvGui;

internal sealed class AspectPictureBox : PictureBox
{
    private double? displayAspectRatio;
    private string? baseResizeMode;

    public double? DisplayAspectRatio
    {
        get => displayAspectRatio;
        set
        {
            displayAspectRatio = value is > 0 ? value : null;
            Invalidate();
        }
    }

    public string? BaseResizeMode
    {
        get => baseResizeMode;
        set
        {
            baseResizeMode = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        if (Image is not null && BaseResizeMode is "fit" or "keep" or "cut")
        {
            DrawBaseResizePreview(pe);
            return;
        }

        if (Image is null || DisplayAspectRatio is not > 0)
        {
            base.OnPaint(pe);
            return;
        }

        pe.Graphics.Clear(BackColor);
        pe.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        pe.Graphics.DrawImage(Image, GetImageRectangle(ClientRectangle, DisplayAspectRatio.Value));
        ControlPaint.DrawBorder(pe.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
    }

    private void DrawBaseResizePreview(PaintEventArgs pe)
    {
        pe.Graphics.Clear(BackColor);
        pe.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var target = GetImageRectangle(ClientRectangle, 640.0 / 400);
        if (target.IsEmpty)
        {
            return;
        }

        if (BaseResizeMode == "keep")
        {
            using var brush = new SolidBrush(Color.Black);
            pe.Graphics.FillRectangle(brush, target);
            pe.Graphics.DrawImage(Image!, GetImageRectangle(target, Image!.Width / (double)Image.Height));
        }
        else if (BaseResizeMode == "cut")
        {
            pe.Graphics.DrawImage(Image!, target, GetCenterCropSourceRectangle(Image!, 640.0 / 400), GraphicsUnit.Pixel);
        }
        else
        {
            pe.Graphics.DrawImage(Image!, target);
        }

        ControlPaint.DrawBorder(pe.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
    }

    private static Rectangle GetCenterCropSourceRectangle(Image image, double targetAspectRatio)
    {
        var srcAspectRatio = image.Width / (double)image.Height;
        if (srcAspectRatio > targetAspectRatio)
        {
            var width = Math.Max(1, (int)Math.Round(image.Height * targetAspectRatio));
            var x = Math.Max(0, (image.Width - width) / 2);
            return new Rectangle(x, 0, Math.Min(width, image.Width - x), image.Height);
        }
        else
        {
            var height = Math.Max(1, (int)Math.Round(image.Width / targetAspectRatio));
            var y = Math.Max(0, (image.Height - height) / 2);
            return new Rectangle(0, y, image.Width, Math.Min(height, image.Height - y));
        }
    }

    private static Rectangle GetImageRectangle(Rectangle bounds, double aspectRatio)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        var width = bounds.Width;
        var height = (int)Math.Round(width / aspectRatio);
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = (int)Math.Round(height * aspectRatio);
        }

        var x = bounds.X + (bounds.Width - width) / 2;
        var y = bounds.Y + (bounds.Height - height) / 2;
        return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
    }
}
