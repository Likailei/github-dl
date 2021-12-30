using System.Text.RegularExpressions;

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
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string LastCommitInfo { get; set; } = string.Empty;
        public string LastCommitDate { get; set; } = string.Empty;

    }

    private static List<RowItem> RowItemList = new List<RowItem>();
    private static HttpClient MyClient = new();
    private static byte WhatToDOMask = 0;

    public static void Main(string[] args)
    {
        string url = DealWithArgs(args);
        byte option = (byte)(WhatToDOMask & 0xFF);
        switch (option)
        {
            case 0x00:
                RequestFileList(url);
                ListWithoutDetails(RowItemList);
                break;

            case 0x01:
                RequestFileList(url);
                ListWithDetails(RowItemList);
                break;

            default:
                break;
        }


    }

    static private string DealWithArgs(string[] args)
    {
        string url = String.Empty;
        foreach (var arg in args)
        {
            if (arg[0] != '-')
            {
                if (arg.Contains("github.com")) url = arg;
                else
                {
                    Console.WriteLine("-h for manual");
                    return url;
                }
            }
            else
            {
                for(var i = 1; i < arg.Length; i++)
                {
                    if (arg[i].Equals('l')) WhatToDOMask |= 0x1;
                    else if(arg[i].Equals('d')) WhatToDOMask |= 0x10;
                    else
                    {
                        Console.WriteLine("-h for manual");
                        return url;
                    }
                }
            }
        }
        return url;
    }

    static private void RequestFileList(string url)
    {
        try
        {
            //string path = @"E:\Code\C#\github-dl\tt.txt";
            //FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            //StreamReader sr = new StreamReader(fs);
            //string ret = sr.ReadToEnd();

            HttpResponseMessage response = MyClient.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            string ret = response.Content.ReadAsStringAsync().Result;

            string contentPattern = "<div role=\"grid\"[\\s\\S]*<readme-toc>";
            string typePattern = "<svg aria-label=\"(.*?)\"";
            string headerPattern = "<div role=\"rowheader\"[\\s\\S]*?</div>";
            string cellPattern = "<div role=\"gridcell\"[\\s\\S]*?</div>";
            string urlPattern = "href=\"(.*?)\"";
            string innerPattern = ">(.*?)<";

            Match content = Regex.Match(ret, contentPattern);
            MatchCollection headers = Regex.Matches(content.Value, headerPattern);
            foreach (Match header in headers)
            {
                RowItem item = new RowItem();

                Match href = Regex.Match(header.Value, urlPattern);
                item.Url = href.Groups[1].Value;

                MatchCollection name = Regex.Matches(header.Value, innerPattern);
                foreach (Match n in name)
                {
                    item.Name += n.Groups[1].Value;
                }
                RowItemList.Add(item);
            }

            MatchCollection cells = Regex.Matches(content.Value, cellPattern);
            int index = 0;
            for (var i = 0; i < cells.Count; i += 3)
            {
                Match type = Regex.Match(cells[i].Value, typePattern);
                if (type.Groups[1].Value.Equals("Directory")) RowItemList[index].Type = RowItemType.Directory;
                else if (type.Groups[1].Value.Equals("File")) RowItemList[index].Type = RowItemType.File;

                MatchCollection commitInfos = Regex.Matches(cells[i + 1].Value, innerPattern);
                foreach (Match m in commitInfos)
                {
                    RowItemList[index].LastCommitInfo += m.Groups[1].Value;
                }

                MatchCollection commitDates = Regex.Matches(cells[i + 2].Value, innerPattern);
                foreach (Match m in commitDates)
                {
                    RowItemList[index].LastCommitDate += m.Groups[1].Value;
                }

                index++;
            }

            //fs.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine("ERROR: {0}", e.Message);
        }
    }

    static private void ListWithDetails(List<RowItem> itemList)
    {
        int cnt = 0;
        bool align = itemList.Count > 9 ? true : false;
        foreach (RowItem item in itemList)
        {
            string name = item.Name;
            if (item.Type == RowItemType.Directory) name += "/";
            if(align) Console.WriteLine($"[{cnt,2}]  {name}\t{item.LastCommitDate}\n\t{item.LastCommitInfo}\t{item.Url}");
            else Console.WriteLine($"[{cnt}]  {name}\t{item.LastCommitDate}\n\t{item.LastCommitInfo}\t{item.Url}");
            cnt++;
        }
    }

    static private void ListWithoutDetails(List<RowItem> itemList)
    {
        int cnt = 0;
        bool align = itemList.Count>9 ? true : false;
        foreach (RowItem item in itemList)
        {
            string name = item.Name;
            if (item.Type == RowItemType.Directory) name += "/";
            if(align) Console.WriteLine($"[{cnt,2}]  {name}");
            else Console.WriteLine($"[{cnt}]  {name}");
            cnt++;
        }
    }
}
