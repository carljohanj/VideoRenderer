using System;
using System.Diagnostics;
using ManycoreProject.Models;
using OfficeOpenXml;
using Xabe.FFmpeg;

namespace ManycoreProject
{
    public class Repository
    {
        public void RenderVideo(IFormFile file, string fileUploads, string extracts, string renders, string renderingOption)
        {
            string videoFilePath = Path.Combine(fileUploads, file.FileName);
            Stopwatch watch = new Stopwatch();

            var videoProcessor = new VideoProcessor();

            using (var fileStream = new FileStream(videoFilePath, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            SaveMetaInformation(videoFilePath);

            //Extract the frames:
            Console.WriteLine("Extracting frames from video...");
            watch.Start();
            videoProcessor.ExtractFrames(videoFilePath, extracts);
            watch.Stop();
            Console.WriteLine("Extraction complete. Rendering frames...");
            ExportDataToExcel("-", watch.ElapsedMilliseconds, GetFileCount(extracts), "Extracting images", file.FileName, GlobalConstants.Resolution);

            //Re-render the frames:
            watch.Restart();
            videoProcessor.RenderFrames(extracts, renders, renderingOption);
            watch.Stop();
            Console.WriteLine("Rendering complete. Encoding video...\n\n");
            ExportDataToExcel(renderingOption, watch.ElapsedMilliseconds, GetFileCount(renders), "Re-rendering images", file.FileName, GlobalConstants.Resolution);

            //Re-render the video:
            watch.Restart();
            videoProcessor.CreateVideo(renders);
            watch.Stop();
            Console.WriteLine("Encoding complete.");
            ExportDataToExcel("-", watch.ElapsedMilliseconds, GetFileCount(extracts), "Re-rendering video", file.FileName, GlobalConstants.Resolution);

            //Make sure there are no extracts and renders left to interfere with the next rendering process:
            Console.WriteLine("Cleaning up temporary files...");
            RemoveTempFiles(fileUploads, extracts, renders);
            Console.WriteLine("Cleaning complete.");
        }

        
        private void ExportDataToExcel(string type, long time, int frames, string functionCall, string fileName, string resolution)
        {
            FileInfo excelFile = new("Results.xlsx");

            using (ExcelPackage package = new ExcelPackage(excelFile))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    worksheet = package.Workbook.Worksheets.Add("Image-rendering");
                    AddColumnNames(worksheet);
                }

                int row = 1;

                if (worksheet.Dimension != null)
                    row = worksheet.Dimension.End.Row + 1;

                worksheet.Cells[row, 1].Value = functionCall;
                worksheet.Cells[row, 2].Value = type;
                worksheet.Cells[row, 3].Value = time;
                worksheet.Cells[row, 4].Value = frames;
                worksheet.Cells[row, 5].Value = resolution;
                worksheet.Cells[row, 6].Value = fileName;
                package.Save();
            }
        }


        private static void AddColumnNames(ExcelWorksheet worksheet)
        {
            worksheet.Cells[1, 1].Value = "Operation";
            worksheet.Cells[1, 2].Value = "Setup";
            worksheet.Cells[1, 3].Value = "Time (Ms)";
            worksheet.Cells[1, 4].Value = "Frames rendered";
            worksheet.Cells[1, 5].Value = "Resolution";
            worksheet.Cells[1, 6].Value = "Name of file";
        }


        private int GetFileCount(string folder)
        {
            try
            {
                string[] files = Directory.GetFiles(folder);
                int fileCount = files.Length;
                return fileCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error counting files: {ex.Message}");
                return 0;
            }
        }


        private void RemoveTempFiles(string fileuploads, string extracts, string renders)
        {
            List<string> directories = [fileuploads, extracts, renders];

            foreach (string dir in directories)
            {
                string[] files = Directory.GetFiles(dir);
                foreach (string filePath in files)
                {
                    File.Delete(filePath);
                }
            }
        }


        private static void SaveMetaInformation(string videoFilePath)
        {
            //Extract the meta information from the file so we can use it to put the video back together again later on:
            var mediaInfo = FFmpeg.GetMediaInfo(videoFilePath).Result;
            GlobalConstants.VideoFramerate = mediaInfo.VideoStreams.First().Framerate;
            GlobalConstants.Width = mediaInfo.VideoStreams.First().Width;
            GlobalConstants.Height = mediaInfo.VideoStreams.First().Height;
            GlobalConstants.FileName = Path.GetFileNameWithoutExtension(videoFilePath);
            GlobalConstants.Bitrate = mediaInfo.VideoStreams.First().Bitrate;
            long bitrateKbps = GlobalConstants.Bitrate / 1000;
            Console.WriteLine($"Bitrate: {bitrateKbps}" + " kbps");
            GlobalConstants.Resolution = GlobalConstants.Width + " x " + GlobalConstants.Height;
            Console.WriteLine($"Framerate: {GlobalConstants.VideoFramerate}" + " frames/second");
            Console.WriteLine($"Resolution: {GlobalConstants.Width}" + " x " + $"{GlobalConstants.Height}");
        }
    }
}
