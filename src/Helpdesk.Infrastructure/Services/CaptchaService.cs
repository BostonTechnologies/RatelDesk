using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Helpdesk.Infrastructure.Services;

/// <summary>
/// Defines methods for generating and validating CAPTCHA challenges.
/// </summary>
/// <remarks>This interface provides functionality to create CAPTCHA challenges and validate user responses. It is
/// designed to help prevent automated interactions by requiring users to solve a challenge.</remarks>
public interface ICaptchaService
{
    (Guid Id, string Challenge) GenerateCaptcha();
    byte[] RenderCaptchaImage(string challenge);
    bool ValidateCaptcha(Guid id, string answer);
}

/// <summary>
/// Provides functionality for generating and validating CAPTCHA challenges.
/// </summary>
/// <remarks>This service generates CAPTCHA challenges consisting of a unique identifier and a random string.  The
/// challenges are stored in memory with a time-to-live (TTL) of 5 minutes.  Callers can validate a CAPTCHA by providing
/// the identifier and the user's response.</remarks>
/// <param name="cache"></param>
public class CaptchaService(IMemoryCache cache) : ICaptchaService
{
    private readonly IMemoryCache _cache = cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const int PixelScale = 4;
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private static readonly IReadOnlyDictionary<char, string[]> DigitGlyphs = new Dictionary<char, string[]>
    {
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "10000", "10000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"]
    };

    public (Guid Id, string Challenge) GenerateCaptcha()
    {
        var id = Guid.NewGuid();
        var text = Random.Shared.Next(100000, 999999).ToString();
        _cache.Set(id, text, Ttl);
        return (id, text);
    }

    public byte[] RenderCaptchaImage(string challenge)
    {
        const int width = 200;
        const int height = 60;
        var random = Random.Shared;
        var background = new Rgba32(255, 255, 255);
        var textColor = new Rgba32((byte)random.Next(20, 80), (byte)random.Next(20, 80), (byte)random.Next(20, 80));

        using var image = new Image<Rgba32>(width, height, background);

        var x = 12;
        foreach (var c in challenge)
        {
            if (!DigitGlyphs.TryGetValue(c, out var glyph))
            {
                continue;
            }

            var yOffset = random.Next(2, 9);
            for (var row = 0; row < GlyphHeight; row++)
            {
                var bits = glyph[row];
                for (var col = 0; col < GlyphWidth; col++)
                {
                    if (bits[col] != '1')
                    {
                        continue;
                    }

                    FillBlock(
                        image,
                        x + (col * PixelScale),
                        yOffset + (row * PixelScale),
                        PixelScale,
                        textColor);
                }
            }

            x += (GlyphWidth * PixelScale) + 6;
        }

        for (var i = 0; i < 6; i++)
        {
            var x1 = random.Next(0, width);
            var y1 = random.Next(0, height);
            var x2 = random.Next(0, width);
            var y2 = random.Next(0, height);
            var lineColor = new Rgba32((byte)random.Next(150, 210), (byte)random.Next(150, 210), (byte)random.Next(150, 210));
            DrawLine(image, x1, y1, x2, y2, lineColor);
        }

        for (var i = 0; i < 220; i++)
        {
            var noiseX = random.Next(0, width);
            var noiseY = random.Next(0, height);
            image[noiseX, noiseY] = new Rgba32((byte)random.Next(130, 230), (byte)random.Next(130, 230), (byte)random.Next(130, 230));
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static void FillBlock(Image<Rgba32> image, int x, int y, int size, Rgba32 color)
    {
        for (var yy = y; yy < y + size; yy++)
        {
            for (var xx = x; xx < x + size; xx++)
            {
                if ((uint)xx < image.Width && (uint)yy < image.Height)
                {
                    image[xx, yy] = color;
                }
            }
        }
    }

    private static void DrawLine(Image<Rgba32> image, int x1, int y1, int x2, int y2, Rgba32 color)
    {
        var dx = Math.Abs(x2 - x1);
        var sx = x1 < x2 ? 1 : -1;
        var dy = -Math.Abs(y2 - y1);
        var sy = y1 < y2 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            if ((uint)x1 < image.Width && (uint)y1 < image.Height)
            {
                image[x1, y1] = color;
            }

            if (x1 == x2 && y1 == y2)
            {
                break;
            }

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x1 += sx;
            }
            if (e2 <= dx)
            {
                error += dx;
                y1 += sy;
            }
        }
    }

    public bool ValidateCaptcha(Guid id, string answer)
    {
        if (_cache.TryGetValue<string>(id, out var expected))
        {
            _cache.Remove(id);
            return string.Equals(expected, answer, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
