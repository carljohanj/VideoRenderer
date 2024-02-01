namespace ManycoreProject.Models
{
    public static class Kernel
    {
        public const string ApplyShader = @"
        __kernel void applyShader(__global uchar4* imageData, const int width, const int height) 
        { 
            int gid = get_global_id(0);
            int totalPixels = width * height;

            for (int i = gid * 4; i < totalPixels; i += get_global_size(0) * 4)
            {
                uchar4 pixel0 = imageData[i];
                uchar4 pixel1 = imageData[i + 1];
                uchar4 pixel2 = imageData[i + 2];
                uchar4 pixel3 = imageData[i + 3];

                pixel0.x = (pixel0.x * 2) % 256;
                pixel0.y = (pixel0.y * 3) % 256;
                pixel0.z = (pixel0.z * 4) % 256;

                pixel1.x = (pixel1.x * 2) % 256;
                pixel1.y = (pixel1.y * 3) % 256;
                pixel1.z = (pixel1.z * 4) % 256;

                pixel2.x = (pixel2.x * 2) % 256;
                pixel2.y = (pixel2.y * 3) % 256;
                pixel2.z = (pixel2.z * 4) % 256;

                pixel3.x = (pixel3.x * 2) % 256;
                pixel3.y = (pixel3.y * 3) % 256;
                pixel3.z = (pixel3.z * 4) % 256;

                imageData[i] = pixel0;
                imageData[i + 1] = pixel1;
                imageData[i + 2] = pixel2;
                imageData[i + 3] = pixel3;
            }   

            barrier(CLK_GLOBAL_MEM_FENCE);
        }
        ";
    }
}
