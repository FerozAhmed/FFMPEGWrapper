/*
 * This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;

namespace FFMPEGWrapper
{
    public enum WatermarkPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
        MiddleLeft,
        MiddleRight,
        CenterTop,
        CenterBottom,
    }

    public class VideoFile
    {
        public TimeSpan Duration
        {
            get;
            private set;
        }

        public double AudioBitRate
        {
            get;
            private set;
        }

        public string AudioFormat
        {
            get;
            private set;
        }

        public string VideoFormat
        {
            get;
            private set;
        }

        public Size Dimensions
        {
            get;
            private set;
        }

        public string FilePath
        {
            get;
            private set;
        }

        public VideoFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception("Could not find the location of the video file");
            }

            if (!File.Exists(filePath))
            {
                throw new Exception(String.Format("The video file {0} does not exist.", FilePath));
            }

            FilePath = filePath;
            GetVideoInfo();
        }

        protected static Image LoadImageFromFile(string filePath)
        {
            Image loadedImage = null;
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] img;
                img = new byte[fileStream.Length];
                fileStream.Read(img, 0, img.Length);
                fileStream.Close();
                loadedImage = Image.FromStream(new MemoryStream(img));
                img = null;
            }

            GC.Collect();

            return loadedImage;
        }

        // I would have made "dimensions" be an optional parameter, but
        // unfortunately C# requires that optional parameters be a "compile
        // time constant" so I cannot use either "Size.Empty" or "new Size()"
        // as the default parameter value. Hence, we have to fall back to a
        // method overload.
        public Image ExtractSingleFrame(int positionToExtract)
        {
            return ExtractSingleFrame(positionToExtract, Size.Empty);
        }

        public Image ExtractSingleFrame(int positionToExtract, Size dimensions)
        {
            string tempFile = Path.ChangeExtension(Path.GetTempFileName(), "jpg");
            FFMPEGParameters parameters = new FFMPEGParameters()
            {
                InputFilePath = FilePath,
                DisableAudio = true,
                OutputOptions = String.Format("-f image2 -ss {0} -vframes 1", positionToExtract),
                Size = dimensions,
                OutputFilePath = tempFile,
            };

            ;
            string output = FFMPEG.Execute(parameters);

            if (!File.Exists(tempFile))
            {
                throw new Exception("Could not create single frame image from video clip");
            }

            Image previewImage = LoadImageFromFile(tempFile);

            try
            {
                File.Delete(tempFile);
            }

            catch (Exception ex)
            {
                throw new Exception("Failed to delete temporary file used for thumbnail " + ex.Message);
            }

            return previewImage;
        }

        protected void GetVideoInfo()
        {
            string output = FFMPEG.Execute(FilePath);

            Duration = InfoProcessor.GetDuration(output);
            AudioBitRate = InfoProcessor.GetAudioBitRate(output);
            AudioFormat = InfoProcessor.GetAudioFormat(output);
            VideoFormat = InfoProcessor.GetVideoFormat(output);
            Dimensions = InfoProcessor.GetVideoDimensions(output);
        }

        public string WatermarkVideo(string watermarkImageFilePath, bool overwrite, WatermarkPosition position, Point offset)
        {
            string extension = Path.GetExtension(FilePath);
            string tempOutputFile = Path.ChangeExtension(Path.GetTempFileName(), extension);

            string overlayFormat;
            switch (position)
            {
                case WatermarkPosition.TopLeft:
                    overlayFormat = "{0}:{1}";
                    break;
                case WatermarkPosition.TopRight:
                    overlayFormat = "main_w-overlay_w-{0}:{1}";
                    break;
                case WatermarkPosition.BottomLeft:
                    overlayFormat = "{0}:main_h-overlay_h-{1}";
                    break;
                case WatermarkPosition.BottomRight:
                    overlayFormat = "main_w-overlay_w-{0}:main_h-overlay_h-{1}";
                    break;
                case WatermarkPosition.Center:
                    overlayFormat = "(main_w-overlay_w)/2-{0}:(main_h-overlay_h)/2-{1}";
                    break;
                case WatermarkPosition.MiddleLeft:
                    overlayFormat = "{0}:(main_h-overlay_h)/2-{1}";
                    break;
                case WatermarkPosition.MiddleRight:
                    overlayFormat = "main_w-overlay_w-{0}:(main_h-overlay_h)/2-{1}";
                    break;
                case WatermarkPosition.CenterTop:
                    overlayFormat = "(main_w-overlay_w)/2-{0}:{1}";
                    break;
                case WatermarkPosition.CenterBottom:
                    overlayFormat = "(main_w-overlay_w)/2-{0}:main_h-overlay_h-{1}";
                    break;

                default:
                    throw new ArgumentException("Invalid position specified");

            }

            string overlayPostion = String.Format(overlayFormat, offset.X, offset.Y);

            FFMPEGParameters parameters = new FFMPEGParameters
            {
                InputFilePath = FilePath,
                OutputFilePath = tempOutputFile,
                SameQ = true,
                Overwrite = true,
                VideoFilter = String.Format("\"movie=\\'{0}\\' [logo]; [in][logo] overlay={1} [out]\"", watermarkImageFilePath.Replace("\\", "\\\\"), overlayPostion)
            };


            string output = FFMPEG.Execute(parameters);
            if (File.Exists(tempOutputFile) == false)
            {
                throw new ApplicationException(String.Format("Failed to watermark video {0}{1}{2}", FilePath, Environment.NewLine, output));
            }

            FileInfo watermarkedVideoFileInfo = new FileInfo(tempOutputFile);
            if (watermarkedVideoFileInfo.Length == 0)
            {
                throw new ApplicationException(String.Format("Failed to watermark video {0}{1}{2}", FilePath, Environment.NewLine, output));
            }

            if (overwrite)
            {
                File.Delete(FilePath);
                File.Move(tempOutputFile, FilePath);

                return FilePath;
            }

            return tempOutputFile;
        }


    }
}
