using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace ManycoreProject.Models
{
    public static class Effects
    {
        public static SixLabors.ImageSharp.Image<Rgba32> ApplyShader(SixLabors.ImageSharp.Image<Rgba32> originalImage)
        {
            var processedImage = originalImage.CloneAs<Rgba32>();

            for (int y = 0; y < processedImage.Height; y++)
            {
                for (int x = 0; x < processedImage.Width; x++)
                {
                    var pixel = processedImage[x, y];

                    pixel.R = (byte)((pixel.R * 2) % 256);
                    pixel.G = (byte)((pixel.G * 3) % 256);
                    pixel.B = (byte)((pixel.B * 4) % 256);
                    processedImage[x, y] = pixel;
                }
            }
            return processedImage;
        }
    }
}
