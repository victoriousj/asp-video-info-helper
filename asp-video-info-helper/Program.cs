using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace asp_ffMpeg_console_app
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var converter = new Converter();
                var video = new VideoFile(GetFilePath(args[0]));
                converter.GetVideoInfo(video);
            }
        }

        private static string GetFilePath(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory + fileName);
    }

    public class Converter
    {
        public string ffExe { get; set; }
        public string WorkingPath { get; set; }

        public Converter()
        {
            Initialize();
        }
        public Converter(string ffmpegExePath)
        {
            ffExe = ffmpegExePath;
            Initialize();
        }

        //Make sure we have valid ffMpeg.exe file and working directory to do our dirty work.
        private void Initialize()
        {
            ffExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            WorkingPath = AppDomain.CurrentDomain.BaseDirectory;
        }

        private string GetWorkingFile()
        {
            //try the stated directory
            if (File.Exists(ffExe))
            {
                return ffExe;
            }

            //oops, that didn't work, try the base directory
            if (File.Exists(Path.GetFileName(ffExe)))
            {
                return Path.GetFileName(ffExe);
            }

            //well, now we are really unlucky, let's just return null
            return null;
        }

        public static Image LoadImageFromFile(string fileName)
        {
            Image theImage = null;
            using (var fileStream = new FileStream(fileName, FileMode.Open,
            FileAccess.Read))
            {
                byte[] img;
                img = new byte[fileStream.Length];

                //ImageConverter converter = new ImageConverter();
                //Image image = (Image)converter.ConvertFrom(img);

                //theImage = Image.FromStream(fileStream);
                //theImage.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "asdf.png"), ImageFormat.Png);
                fileStream.Read(img, 0, img.Length);
                fileStream.Close();
                img = null;
            }
            GC.Collect();
            return theImage;
        }

        public static MemoryStream LoadMemoryStreamFromFile(string fileName)
        {
            MemoryStream ms = null;
            using (var fileStream = new FileStream(fileName, FileMode.Open,
            FileAccess.Read))
            {
                byte[] fil;
                fil = new byte[fileStream.Length];
                fileStream.Read(fil, 0, fil.Length);
                fileStream.Close();
                ms = new MemoryStream(fil);
            }
            GC.Collect();
            return ms;
        }

        //Note the private call here and the argument for Parameters.  The private call is
        //being made here because, in this class, we don't really want to have this method
        //called from outside of the class -- this, however flies in the face of allowing the
        //parameters argument (why not just allow out the public call so that a developer can
        //put in the parameters from their own code?  I guess one could do it and it would probably
        //work fine but, for this implementation, I chose to leave it private.
        private string RunProcess(string Parameters)
        {
            //create a process info object so we can run our app
            var oInfo = new ProcessStartInfo(ffExe, Parameters);
            oInfo.UseShellExecute = false;
            oInfo.CreateNoWindow = false;

            //so we are going to redirect the output and error so that we can parse the return
            oInfo.RedirectStandardOutput = true;
            oInfo.RedirectStandardError = true;

            //Create the output and streamreader to get the output
            string output = null;
            StreamReader srOutput = null;

            //try the process
            try
            {
                //run the process
                var proc = Process.Start(oInfo);

                proc.WaitForExit();

                //get the output
                srOutput = proc.StandardError;

                //now put it in a string
                output = srOutput.ReadToEnd();

                proc.Close();
            }
            catch (Exception)
            {
                output = string.Empty;
            }
            finally
            {
                //now, if we succeded, close out the streamreader
                if (srOutput != null)
                {
                    srOutput.Close();
                    srOutput.Dispose();
                }
            }
            return output;
        }

        //And now the important code for the GetVideoInfo
        public void GetVideoInfo(VideoFile input)
        {
            //set up the parameters for video info -- these will be passed into ffMpeg.exe
            string Params = string.Format(" -i {0}", input.Path);
            string output = RunProcess(Params);
            input.RawInfo = output;

            //Use a regular expression to get the different properties from the video parsed out.
            Regex re = new Regex("[D|d]uration:.((\\d|:|\\.)*)");
            Match m = re.Match(input.RawInfo);
            string thumbnailDuration = string.Empty;
            if (m.Success)
            {
                string duration = m.Groups[1].Value;
                string[] timepieces = duration.Split(new char[] { ':', '.' });
                if (timepieces.Length == 4)
                {
                    input.Duration = new TimeSpan(0, Convert.ToInt16(timepieces[0]), Convert.ToInt16(timepieces[1]), Convert.ToInt16(timepieces[2]), Convert.ToInt16(timepieces[3]));
                    thumbnailDuration = TimeSpan.FromSeconds((long)Math.Floor(input.Duration.TotalSeconds / 10.001)).TotalSeconds.ToString();
                }
            }

            //get audio bit rate
            re = new Regex("[B|b]itrate:.((\\d|:)*)");
            m = re.Match(input.RawInfo);
            double kb = 0.0;
            if (m.Success)
            {
                Double.TryParse(m.Groups[1].Value, out kb);
            }
            input.BitRate = kb;

            //get the audio format
            re = new Regex("[A|a]udio:.*");
            m = re.Match(input.RawInfo);
            if (m.Success)
            {
                input.AudioFormat = m.Value;
            }

            //get the video format
            re = new Regex("[V|v]ideo:.*");
            m = re.Match(input.RawInfo);
            if (m.Success)
            {
                input.VideoFormat = m.Value;
            }

            //get the video format
            re = new Regex("(\\d{2,3})x(\\d{2,3})");
            m = re.Match(input.RawInfo);
            if (m.Success)
            {
                int width = 0; int height = 0;
                int.TryParse(m.Groups[1].Value, out width);
                int.TryParse(m.Groups[2].Value, out height);
                input.Width = width;
                input.Height = height;
            }

            //Params = $" -i {input.Path} -frames:v 11 -vf fps=fps=1/{thumbnailDuration} out%02d.jpg";
            //output = RunProcess(Params);

            input.infoGathered = true;
        }
    }

    public class VideoFile
    {
        public string Path { get; set; }

        public TimeSpan Duration { get; set; }
        public double BitRate { get; set; }
        public string AudioFormat { get; set; }
        public string VideoFormat { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public string RawInfo { get; set; }
        public bool infoGathered { get; set; }

        public VideoFile(string path)
        {
            Path = path;
            Initialize();
        }

        #region Initialization
        private void Initialize()
        {
            this.infoGathered = false;
            //first make sure we have a value for the video file setting
            if (string.IsNullOrEmpty(Path))
            {
                throw new Exception("Could not find the location of the video file");
            }

            //Now see if the video file exists
            if (!File.Exists(Path))
            {
                throw new Exception("The video file " + Path + " does not exist.");
            }
        }
        #endregion
    }

    public class OutputPackage
    {
        public MemoryStream VideoStream { get; set; }
        public Image PreviewImage { get; set; }
        public string RawOutput { get; set; }
        public bool Success { get; set; }
    }
}
