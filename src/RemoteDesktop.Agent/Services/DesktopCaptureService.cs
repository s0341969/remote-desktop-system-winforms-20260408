using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Models;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class DesktopCaptureService
{
    private static readonly ImageCodecInfo JpegCodec = ImageCodecInfo.GetImageEncoders()
        .First(static encoder => string.Equals(encoder.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));

    private readonly AgentOptions _options;

    public DesktopCaptureService(IOptions<AgentOptions> options)
    {
        _options = options.Value;
    }

    public DesktopFrame Capture()
    {
        var bounds = GetVirtualScreenBounds();
        using var source = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        var outputSize = CalculateOutputSize(bounds.Width, bounds.Height, _options.MaxFrameWidth);
        using var resized = outputSize.Width == bounds.Width
            ? new Bitmap(source)
            : ResizeBitmap(source, outputSize.Width, outputSize.Height);

        if (IsEffectivelyBlackFrame(resized))
        {
            throw new InvalidOperationException("The interactive desktop is currently returning a black frame.");
        }

        return new DesktopFrame(EncodeJpeg(resized, _options.JpegQuality), bounds.Width, bounds.Height);
    }

    public Size GetVirtualScreenSize()
    {
        return GetVirtualScreenBounds().Size;
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        return SystemInformation.VirtualScreen;
    }

    private static Size CalculateOutputSize(int width, int height, int maxWidth)
    {
        if (width <= maxWidth)
        {
            return new Size(width, height);
        }

        var ratio = maxWidth / (double)width;
        return new Size(maxWidth, Math.Max((int)Math.Round(height * ratio), 1));
    }

    private static Bitmap ResizeBitmap(Bitmap original, int width, int height)
    {
        var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(original, 0, 0, width, height);
        return resized;
    }

    private static byte[] EncodeJpeg(Image image, long quality)
    {
        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        using var stream = new MemoryStream();
        image.Save(stream, JpegCodec, encoderParameters);
        return stream.ToArray();
    }

    private static bool IsEffectivelyBlackFrame(Bitmap bitmap)
    {
        const int sampleSteps = 12;
        const int brightnessThreshold = 8;
        const int requiredVisibleSamples = 3;

        var widthStep = Math.Max(bitmap.Width / sampleSteps, 1);
        var heightStep = Math.Max(bitmap.Height / sampleSteps, 1);
        var visibleSamples = 0;

        for (var y = 0; y < bitmap.Height; y += heightStep)
        {
            for (var x = 0; x < bitmap.Width; x += widthStep)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > brightnessThreshold || pixel.G > brightnessThreshold || pixel.B > brightnessThreshold)
                {
                    visibleSamples++;
                    if (visibleSamples >= requiredVisibleSamples)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
