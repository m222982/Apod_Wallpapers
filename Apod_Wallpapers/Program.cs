using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text.Json;

namespace Apod_Wallpapers
{
    class Program
    {
        // 使用 DllImport 來匯入 user32.dll 的 SystemParametersInfo 函式
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        // 定義 SystemParametersInfo 的常數值
        const uint SPI_SETDESKWALLPAPER = 0x0014;
        const uint SPIF_UPDATEINIFILE = 0x01;
        const uint SPIF_SENDCHANGE = 0x02;

        static HttpClient client = new HttpClient();

        static Setting setting = new Setting() { NASA = false, Explaination = true};

        static void Main()
        {
            if (File.Exists("Apod_Wallpaper.json"))
            {
                string txtSetting = File.ReadAllText("Apod_Wallpaper.json");
                try
                {
                    setting = JsonSerializer.Deserialize<Setting>(txtSetting);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            Console.WriteLine("Apod wallpaper start...");
            try
            {
                RunAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task RunAsync()
        {
            //var response = await client.GetAsync("http://sprite.phys.ncku.edu.tw/astrolab/mirrors/apod/apod.html");

            string url_apod = setting.NASA ? "https://apod.nasa.gov/apod/astropix.html" :
                "http://sprite.phys.ncku.edu.tw/astrolab/mirrors/apod/apod.html";
            var response = await client.GetAsync(url_apod);

            if (response.IsSuccessStatusCode)
            {
                var webContent = await response.Content.ReadAsStringAsync();

                var imgUrl = parseImgUrl(webContent);
                if (imgUrl == null)
                    return;

                imgUrl = "https://apod.nasa.gov/apod/image/" + imgUrl;

                // 取得使用者的圖片目錄
                string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                // 使用 Uri 類別來處理 URL
                Uri uri = new Uri(imgUrl);

                // 取得檔案名稱
                string fileName = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                string fileNameExt = System.IO.Path.GetExtension(uri.LocalPath);
                string saveImgFilename = Path.Combine(picturesPath, fileName + "_wp" + fileNameExt);

                if (File.Exists(saveImgFilename))
                {
                    Console.WriteLine(string.Format("File is exist {0}!", saveImgFilename));
                    return;
                }

                Console.WriteLine(string.Format("Download image file {0}.", imgUrl));
                //下載照片
                var imageStream = await client.GetStreamAsync(imgUrl);

                System.Drawing.Image image = Image.FromStream(imageStream);

                var wh_ratio_img = (float)image.Width / image.Height;

                var screenRes = getScreenResolution();
                var wh_ratio_screen = screenRes.Width / screenRes.Height;

                //調整照片跟螢幕解析度一樣大
                //照片比較方，高度為主
                if (wh_ratio_screen > wh_ratio_img)
                {
                    if (image.Height > screenRes.Height)
                    {
                        image = ResizeImage(image, (int)(screenRes.Height*wh_ratio_img), (int)screenRes.Height);
                    }
                }
                else
                {
                    if (image.Width > screenRes.Width)
                    {
                        image = ResizeImage(image, (int)screenRes.Width, (int)(screenRes.Width / wh_ratio_img));
                    }
                }
                
                Bitmap bitmap = new Bitmap(image);
                
                if (setting.Explaination)
                {
                    var explainStr = setting.NASA ? parseNasaExplainText(webContent) : parseExplainText(webContent);
                    //Console.WriteLine(explainStr);

                    var lineCnt = explainStr.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;

                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        Font font = new Font("Arial", 10);
                        Brush brush = Brushes.White;
                        g.DrawString(explainStr, font, brush, new PointF(10, image.Height - lineCnt * font.Height -40)); //40 為工作表高度
                    }
                }
                //取得說明


                Console.WriteLine(string.Format("Save image file {0}.", saveImgFilename));
                bitmap.Save(saveImgFilename);


                // 設定桌布置中顯示的註冊表
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key != null)
                {
                    key.SetValue(@"WallpaperStyle", "0");   // 0: Centered (置中)
                    key.SetValue(@"TileWallpaper", "0");    // 0: No tile (不平鋪)
                    key.Close();
                }
                // 呼叫 SystemParametersInfo 設定桌布
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, saveImgFilename, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
        }


        //from https://stackoverflow.com/questions/71537840/get-current-screen-resolution-using-net-core-console-application
        static SizeF getScreenResolution()
        {
            //get WMI object for VideoController1
            var wmiMonitor = new ManagementObject("Win32_VideoController.DeviceID=\"VideoController1\"");

            var width = wmiMonitor["CurrentHorizontalResolution"];
            var height = wmiMonitor["CurrentVerticalResolution"];

            return new SizeF((uint)width, (uint)height);
        }
        //用正則表達式相片網址
        static string parseImgUrl(string webContent)
        {
            // 正則表達式匹配 SRC 屬性中的字串
            //使用 <a href="image 匹配，預設都在 image 目錄內
            string pattern = @"<a href=""image/([^""]+)""(.|\r|\n)*?<IMG";


            // 使用正則表達式進行匹配
            Match match = Regex.Match(webContent, pattern, RegexOptions.IgnoreCase);

            // 如果匹配失敗，回傳空字串
            if (!match.Success)
            {
                Console.WriteLine("Wrong image url.");
                return string.Empty;
            }
            // 提取匹配組中的內容
            return match.Groups[1].Value;
        }

        static string parseExplainText(string webContent)
        {
            // 正則表達式匹配 SRC 屬性中的字串
            string pattern = @"<b>\s*說明:\s*</b>\s*([\p{L}\p{Z}\p{S}\p{N}\p{P}\p{C}]*?)<p>";

            // 使用正則表達式進行匹配
            Match match = Regex.Match(webContent, pattern, RegexOptions.IgnoreCase);

            // 如果匹配失敗，回傳空字串
            if (!match.Success)
            {
                Console.WriteLine("Can't get explanation.");
                return string.Empty;
            }
            // 提取匹配組中的內容
            // 正則表達式移除 HTML 標籤
            return Regex.Replace(match.Groups[1].Value, @"<\/?(.|\r|\n)+?\/?>", "").Replace("\n\n", "\n");
        }

        static string parseNasaExplainText(string webContent)
        {
            // 正則表達式匹配 SRC 屬性中的字串
            //string pattern = @"<b>\s*說明:\s*</b>\s*([\p{L}\p{Z}\p{S}\p{N}\p{P}\p{C}]*?)<p>";
            string pattern = @"<b>\s*Explanation:\s*</b>\s*([\p{L}\p{Z}\p{S}\p{N}\p{P}\p{C}]*?)<p>";
            // 使用正則表達式進行匹配
            Match match = Regex.Match(webContent, pattern, RegexOptions.IgnoreCase);

            // 如果匹配失敗，回傳空字串
            if (!match.Success)
            {
                Console.WriteLine("Can't get explanation.");
                return string.Empty;
            }
            // 提取匹配組中的內容
            // 正則表達式移除 HTML 標籤
            return Regex.Replace(match.Groups[1].Value, @"<\/?(.|\r|\n)+?\/?>", "")
                .Replace("\n", "")
                .Replace(".", ".\n")
                .Replace("?", "?\n")
                .Replace("!", "!\n");
        }

        static Image ResizeImage(Image image, int width, int height)
        {
            Bitmap resizedBitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(resizedBitmap))
            {
                g.DrawImage(image, 0, 0, width, height);
            }
            return resizedBitmap;
        }
    }
}
