using HtmlAgilityPack;
using System.Text;

class GithubDL
{
    enum RowItemType
    {
        Directory,
        File
    }

    private class RowItem
    {
        public RowItemType Type { get; set; }
        public string Name { get; set; } = String.Empty;
        public string Url { get; set; } = String.Empty;
        public string LastCommitInfo { get; set; } = String.Empty;
        public string LastCommitDate { get; set; } = String.Empty;
    }

    private static byte WhatToDOMask = 0;
    private static readonly HttpClient Client = new();
    private static string MainUrl = string.Empty;
    private static string RootOutPath = ".";

    public static void Main(string[] args)
    {
        MainUrl = DealWithArgs(args);

        List<RowItem> rowItemList = new();
        if (MainUrl != "")
        {
            RequestFileList(MainUrl, rowItemList);
            List(rowItemList);
            GetRequiedFiles(rowItemList);
        }

        var key = Console.ReadKey();
        if (key.KeyChar.Equals('q')) return;
    }

    static private string DealWithArgs(string[] args)
    {
        string url = String.Empty;
        for(int i=0;i<args.Length; i++)
        {
            var arg = args[i];
            if (arg[0] != '-')
            {
                if (arg.Contains("github.com")) url = Uri.UnescapeDataString(arg);
                else if(arg.Contains('\\') || arg.Contains('/') || arg.Contains(':'))
                {
                    RootOutPath = arg;
                }
                else
                {
                    Console.WriteLine("-h for manual");
                    return url;
                }
            }
            else
            {
                for (var j = 1; j < arg.Length; j++)
                {
                    switch (arg[j])
                    {
                    case 'l':
                        WhatToDOMask |= 0x0F;
                        break;
                    case 'd':
                        WhatToDOMask |= 0xF0;
                        break;
                    case 'o':
                        RootOutPath = args[i+1];
                        break;
                    case 'h':
                        PrintHelpInfo();
                        return "";
                    }
                }
            }
        }
        return url;
    }

    static private void PrintHelpInfo()
    {
        Console.WriteLine(@"\ngithub-dl.exe [OPTION]... [URL]...\n\n  -l\t
            list files with details\n\n  -d\tdownload all files in current path directly\n\n  -o\toutput path");
    }

    static private void RequestFileList(string url, List<RowItem> list)
    {
        try
        {
            var htmlWeb = new HtmlWeb();
            var htmlDoc = htmlWeb.Load(url);

            HtmlNodeCollection gridChildren = htmlDoc.DocumentNode
                .SelectNodes("//div[@class='js-details-container Details']/div[@role='grid']/div[contains(@class, 'py-2')]");

            int cnt = gridChildren.Count;
            foreach (var grid in gridChildren)
            {
                RowItem item = new();

                HtmlNode header = grid.SelectSingleNode(".//div[@role='rowheader']/span/a");
                item.Name = header.InnerText;
                item.Url = header.GetAttributeValue("href", "not find");

                HtmlNodeCollection cells = grid.SelectNodes(".//div[@role='gridcell']");

                var svg = cells[0].SelectSingleNode(".//svg");
                var type = svg.GetAttributeValue("aria-label", "not find");
                if (type.Contains("Directory"))
                {
                    item.Type = RowItemType.Directory;
                    item.Name += "/";
                }
                else item.Type = RowItemType.File;


                var commitMsg = cells[1].SelectSingleNode(".//span/a");
                if (commitMsg != null) item.LastCommitInfo = commitMsg.GetAttributeValue("title", "not find").Split('\n')[0];

                var time = cells[2].SelectSingleNode(".//time-ago");
                string t = cells[2].InnerText.Trim();
                if (t.Length < 12) t = t.Insert(4, " ");
                if (time != null) item.LastCommitDate = t;

                list.Add(item);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("ERROR: {0}", e.Message);
        }
    }

    static void GetRequiedFiles(List<RowItem> list)
    {
        try
        {
            string input;
            if ((WhatToDOMask & 0xF0) == 0xF0)
            {
                list.ForEach(i => Download(i));
                return;
            }
            else input = Console.ReadLine();

            var indexAndPath = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<int> fileIndices = new();

            foreach (string str in indexAndPath)
            {
                if (str.Contains('/') || str.Contains('\\') || str.Contains(':'))
                {
                    RootOutPath = str;
                }
                else
                {
                    fileIndices.Add(Convert.ToInt32(str));
                }
            }
            fileIndices.ForEach(i => Download(list[i]));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Get requied files error: {e.Message}");
        }
    }

    static void Download(RowItem item)
    {
        if (item.Type == RowItemType.File)
        {
            DownloadAndSaveFileAsync(item);
        }
        else if (item.Type == RowItemType.Directory)
        {
            DownloadAndSaveDirectory(item);
        }
    }

    static async void DownloadAndSaveFileAsync(RowItem item)
    {
        var rawUrl = "https://raw.githubusercontent.com" + item.Url.Replace("/blob/", "/");
        var filePath = GetFilePath(rawUrl);
        var str = await DownloadFileAsync(rawUrl);
        if(str?.Length != 0)
        {
            SaveFile(str, filePath);
        }
    }

    private static string GetFilePath(string rawFileUrl)
    {
        var rootName = $"/{MainUrl.Split('/').Last()}/";
        return $"{RootOutPath}{rawFileUrl[rawFileUrl.IndexOf(rootName)..]}";
    }

    static void DownloadAndSaveDirectory(RowItem item)
    {
        var dirUrl = "https://github.com" + item.Url;
        List<RowItem> list = new List<RowItem>();
        RequestFileList(dirUrl, list);
        foreach (RowItem i in list)
        {
            Download(i);
        }
    }

    static async Task<string> DownloadFileAsync(string url)
    {
        try
        {
            return await Client.GetStringAsync(url);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Download file error: {e.Message}");
            return null;
        }
    }

    static void SaveFile(string content, string path)
    {
        try
        {
            var dirPath = Path.GetDirectoryName(path);
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(System.Text.Encoding.Default.GetBytes(content), 0, content.Length);
            fs.Close();
            Console.WriteLine($"Save file: \"{path}\" done.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Save file error: {e.Message}");
        }
    }

    static private int GetLongestNameLength(List<RowItem> itemList)
    {
        int lenth = 0;
        foreach (var i in itemList)
        {
            if (lenth < i.Name.Length) lenth = i.Name.Length;
        }
        return lenth;
    }

    static void List(List<RowItem> itemList)
    {
        bool withDetails = ((WhatToDOMask & 0x0F) == 0x0F) ? true : false;

        int index = 0;
        int indexPadding = itemList.Count.ToString().Length;
        int namePadding = GetLongestNameLength(itemList);

        StringBuilder sb = new();
        sb.Append("\n\t");
        sb.AppendLine(MainUrl);
        sb.AppendLine();
        foreach (RowItem item in itemList)
        {
            sb.Append('[');
            sb.Append($"{index}".PadLeft(indexPadding, ' '));
            sb.Append($"]  {item.Name}".PadRight(namePadding + 3, ' '));
            if (withDetails)
            {
                sb.Append($"  {item.LastCommitDate}".PadRight(14, ' ')); // "Nov 24, 2021".length + 2 spaces = 14
                sb.Append($"  {item.LastCommitInfo}");
            }
            sb.AppendLine();
            index++;
        }
        if((WhatToDOMask & 0xF0) != 0xF0)
            sb.AppendLine("\nInput the file indices and saving path to download:");

        Console.WriteLine(sb.ToString());
    }
}
