#nullable disable

using Emgu.CV;
using ManycoreProject.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using Image = SixLabors.ImageSharp.Image;
using System.Drawing;
using Cloo;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;

namespace ManycoreProject
{
    class VideoProcessor
    {

        /// <summary>
        /// Extracts all video frames from a video file and saves them as temporary images in an output folder.
        /// </summary>
        /// <param name="videoPath">The path to the video file</param>
        /// <param name="outputDirectory">The output folder to save the images to</param>
        public void ExtractFrames(string videoPath, string outputDirectory)
        {
            using (var videoCapture = new VideoCapture(videoPath))
            {
                if (!videoCapture.IsOpened)
                {
                    Console.WriteLine("Error: Unable to open the video file.");
                    return;
                }

                int frameIndex = 0;

                while (true)
                {
                    Mat frame = new Mat();
                    if (videoCapture.Read(frame))
                    {
                        string outputPath = Path.Combine(outputDirectory, $"frame_{frameIndex}.png");
                        CvInvoke.Imwrite(outputPath, frame);

                        frameIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }


        /// <summary>
        /// A factory method that delegates the rendering for a sequence of images. It re-renders the sequence by applying a visual effect to it.
        /// </summary>
        /// <param name="inputDirectory">The directory where the prerendered images are located</param>
        /// <param name="outputDirectory">The directory where the renders will be saved</param>
        /// <param name="renderingOption">The rendering option</param>
        public void RenderFrames(string inputDirectory, string outputDirectory, string renderingOption)
        {
            if (renderingOption == "Parallell rendering")
                RenderFramesParallel(inputDirectory, outputDirectory);
            else if (renderingOption == "Normal rendering")
                RenderFramesSequential(inputDirectory, outputDirectory);
            else if (renderingOption == "GPU-rendering")
                RenderFramesOnGPU(inputDirectory, outputDirectory);
        }


        /// <summary>
        /// Encodes a image sequence into a video file with the same properties as the original video.
        /// </summary>
        /// <param name="imageDirectory">The directory for the output video</param>
        public void CreateVideo(string imageDirectory)
        {
            string outPutVideo = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "output_files", $"{GlobalConstants.FileName}.mp4");

            string[] imagePaths = Directory.GetFiles(imageDirectory, "frame_*.png")
                                             .OrderBy(path => int.Parse(Path.GetFileNameWithoutExtension(path).Substring(6)))
                                             .ToArray();

            double videoFramerate = GlobalConstants.VideoFramerate;
            string framerateString = $"{videoFramerate:N0}/1";

            string ffmpegCommand = $"ffmpeg -y -framerate {framerateString} -i \"{imageDirectory}\\frame_%d.png\" -c:v libx264 -pix_fmt yuv420p \"{outPutVideo}\"";

            using (Process process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c {ffmpegCommand}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
        }


        private void RenderFramesOnGPU(string inputDirectory, string outputDirectory)
        {
            object lockObject = new object();

            ReadOnlyCollection<ComputePlatform> platforms = ComputePlatform.Platforms;
            ComputeDevice gpuDevice = platforms.SelectMany(p => p.Devices).FirstOrDefault(device => device.Type == ComputeDeviceTypes.Gpu);
            Console.WriteLine($"Selected GPU Device: {gpuDevice.Name}");

            ComputeContextPropertyList properties = new ComputeContextPropertyList(platforms[0]);
            ComputeContext context = new ComputeContext(new ComputeDevice[] { gpuDevice }, properties, null, IntPtr.Zero);
            string kernelCode = Kernel.ApplyShader;
            ComputeProgram program = new ComputeProgram(context, kernelCode);
            program.Build(null, null, null, IntPtr.Zero);

            string[] framePaths = Directory.GetFiles(inputDirectory, "*.png").OrderBy(f => f).ToArray();
            int chunks = Environment.ProcessorCount;
            var framePathChunks = PartitionIntoChunks(framePaths, chunks);

            ComputeKernel kernel = program.CreateKernel("applyShader");
            ComputeCommandQueue queue = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);

            foreach (var framePathChunk in framePathChunks)
            {
                Parallel.ForEach(framePathChunk, framePath =>
                {
                    using (Bitmap image = new Bitmap(framePath))
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(framePath);
                        int width = image.Width;
                        int height = image.Height;

                        BitmapData bmpData = image.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        ComputeBuffer<byte> imageBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.ReadWrite | ComputeMemoryFlags.CopyHostPointer, width * height * 4, bmpData.Scan0);

                        lock (lockObject)
                        {
                            kernel.SetMemoryArgument(0, imageBuffer);
                            kernel.SetValueArgument(1, width);
                            kernel.SetValueArgument(2, height);
                            queue.ExecuteTask(kernel, null);
                        }

                        byte[] resultData = new byte[width * height * 4];
                        GCHandle resultHandle = GCHandle.Alloc(resultData, GCHandleType.Pinned);
                        IntPtr resultDataPtr = resultHandle.AddrOfPinnedObject();
                        ComputeBuffer<byte> resultBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.ReadWrite | ComputeMemoryFlags.UseHostPointer, resultData.Length, resultDataPtr);
                        queue.ReadFromBuffer(imageBuffer, ref resultData, true, null);

                        using (Bitmap processedImage = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, resultDataPtr))
                        {
                            string outputPath = Path.Combine(outputDirectory, $"{fileNameWithoutExtension}.png");
                            processedImage.Save(outputPath, ImageFormat.Png);
                        }

                        resultBuffer.Dispose();
                        resultHandle.Free();
                        imageBuffer.Dispose();
                        image.UnlockBits(bmpData);
                    }
                });
            }
            kernel.Dispose();
            queue.Dispose();
            program.Dispose();
            context.Dispose();
        }


        private void RenderFramesParallel(string inputFramesDirectory, string outputFramesDirectory)
        {
            string[] framePaths = Directory.GetFiles(inputFramesDirectory, "frame_*.png");
            int chunks = System.Environment.ProcessorCount;
            Console.WriteLine("Number of chunks: " + chunks);
            var framePathChunks = PartitionIntoChunks(framePaths, chunks);

            Parallel.ForEach(framePathChunks, framePathChunk =>
            {
                foreach (var framePath in framePathChunk)
                {
                    using (var originalImage = Image.Load<Rgba32>(framePath))
                    {
                        using (var processedImage = Effects.ApplyShader(originalImage))
                        {
                            string fileName = Path.GetFileName(framePath);
                            string outputPath = Path.Combine(outputFramesDirectory, fileName);
                            processedImage.Save(outputPath);
                        }
                    }
                }
            });
        }


        private void RenderFramesSequential(string inputFramesDirectory, string outputFramesDirectory)
        {
            string[] framePaths = Directory.GetFiles(inputFramesDirectory, "frame_*.png");

            foreach (var framePath in framePaths)
            {
                using (var originalImage = Image.Load<Rgba32>(framePath))
                {
                    using (var processedImage = Effects.ApplyShader(originalImage))
                    {
                        string fileName = Path.GetFileName(framePath);
                        string outputPath = Path.Combine(outputFramesDirectory, fileName);
                        processedImage.Save(outputPath);
                    }
                }
            }
        }


        private static List<List<string>> PartitionIntoChunks(string[] source, int chunkCount)
        {
            var chunks = new List<List<string>>();
            for (int i = 0; i < chunkCount; i++)
            {
                chunks.Add(new List<string>());
            }

            int chunkIndex = 0;
            foreach (var item in source)
            {
                chunks[chunkIndex].Add(item);
                chunkIndex = (chunkIndex + 1) % chunkCount;
            }
            return chunks;
        }

    }
}
