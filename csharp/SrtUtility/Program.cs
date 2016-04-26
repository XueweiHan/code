using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SrtUtility
{
    class Program
    {
        class Item
        {
            public TimeSpan BeginTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Content { get; set; }
        }

        static CultureInfo TimeSpanParsingCulture = new CultureInfo("fr-FR");

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 4 && args[0] == "-m")
                {
                    Console.WriteLine("Reading file: {0}", args[1]);
                    var a = ParseSrtFile(args[1]);
                    Console.WriteLine("Reading file: {0}", args[2]);
                    var b = ParseSrtFile(args[2]);

                    Console.WriteLine("Merging to  : {0}", args[3]);
                    var c = Merge(a, b);
                    SaveSrtFile(c, args[3]);
                    Console.WriteLine("Success");
                    return;
                }
                else if (args.Length == 5 && args[0] == "-sm")
                {
                    Console.WriteLine("Reading file: {0}", args[1]);
                    var a = ParseSrtFile(args[1]);
                    Console.WriteLine("Reading file: {0}", args[2]);
                    var b = ParseSrtFile(args[2]);

                    Console.WriteLine("Testing....");
                    if (args[3] == "0")
                    {
                        var x = Test(a, b);
                        Console.WriteLine("Move file: {0} for Time delta: {1:0.000}", args[2], x);
                        b = MoveTime(b, TimeSpan.FromSeconds(x));
                    }
                    else
                    {
                        var x = Test(b, a);
                        Console.WriteLine("Move file: {0} for Time delta: {1:0.000}", args[1], x);
                        a = MoveTime(a, TimeSpan.FromSeconds(x));
                    }

                    Console.WriteLine("Merging to  : {0}", args[4]);
                    var c = Merge(a, b);
                    SaveSrtFile(c, args[4]);
                    Console.WriteLine("Success");
                    return;
                }
                else if (args.Length == 5 && args[0] == "-t")
                {
                    string line = string.Format("{0} --> {1}", args[2], args[3]);
                    var times = TryParseTime(line);
                    if (times != null)
                    {
                        Console.WriteLine("Reading file: {0}", args[1]);
                        var a = ParseSrtFile(args[1]);

                        var c = MoveTime(a, times.Item2 - times.Item1);

                        Console.WriteLine("Writing file: {0}", args[4]);
                        SaveSrtFile(c, args[4]);
                        Console.WriteLine("Success");
                        return;
                    }
                }
                else if (args.Length == 3 && args[0] == "-test")
                {
                    Console.WriteLine("Reading file: {0}", args[1]);
                    var a = ParseSrtFile(args[1]);
                    Console.WriteLine("Reading file: {0}", args[2]);
                    var b = ParseSrtFile(args[2]);

                    Console.WriteLine("Testing....");
                    var x = Test(a, b);
                    Console.WriteLine("Best time span: {0:0.000}", x);
                    return;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            Console.WriteLine("merge       : SrtUtility -m <input1> <input2> <output>");
            Console.WriteLine("smart merge : SrtUtility -sm <input1> <input2> <0 or 1> <output>");
            Console.WriteLine("time align  : SrtUtility -t <input> <hh:mm:ss,fff> <hh:mm:ss,fff> <output>");
        }

        static double Test(List<Item> a, List<Item> b)
        {
            var minCount = a.Count + b.Count;
            var minTimeSpan = .0;
            for (double i = -10; i <= 10; i += 0.05)
            {
                var c = Merge(a, MoveTime(b, TimeSpan.FromSeconds(i)));
                if (c.Count < minCount)
                {
                    minCount = c.Count;
                    minTimeSpan = i;
                }
                else if (c.Count == minCount && Math.Abs(i) < Math.Abs(minTimeSpan))
                {
                    minTimeSpan = i;
                }
            }
            return minTimeSpan;
        }

        static List<Item> MoveTime(List<Item> a, TimeSpan time)
        {
            var b = Clone(a);
            foreach (var item in b)
            {
                item.BeginTime += time;
                item.EndTime += time;
            }
            return b;
        }

        static void SaveSrtFile(List<Item> items, string filePath)
        {
            using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.UTF8))
                for (var i = 0; i < items.Count; ++i)
                {
                    sw.WriteLine(i + 1);
                    var item = items[i];
                    sw.WriteLine("{0:hh\\:mm\\:ss\\,fff} --> {1:hh\\:mm\\:ss\\,fff}", item.BeginTime, item.EndTime);
                    sw.WriteLine(item.Content);
                    sw.WriteLine();
                }
        }

        static List<Item> Clone(List<Item> a)
        {
            var b = new List<Item>();
            foreach (var item in a)
            {
                b.Add(new Item()
                {
                    BeginTime = item.BeginTime,
                    EndTime = item.EndTime,
                    Content = item.Content,
                });
            }
            return b;
        }

        static List<Item> Merge(List<Item> a, List<Item> b)
        {
            a = Clone(a);
            b = Clone(b);
            int indexa = 0;
            int indexb = 0;
            List<Item> c = new List<Item>();
            Item itemc = null;

            while (indexa < a.Count && indexb < b.Count)
            {
                var itema = a[indexa];
                var itemb = b[indexb];

                if (itema.EndTime < itemb.BeginTime)
                {
                    itemc = itema;
                    itemc.Content += Environment.NewLine + ".";
                    ++indexa;
                }
                else if (itemb.EndTime < itema.BeginTime)
                {
                    itemc = itemb;
                    itemc.Content = "." + Environment.NewLine + itemc.Content;
                    ++indexb;
                }
                else
                {
                    if (Math.Abs((itema.BeginTime - itemb.BeginTime).TotalSeconds) < 0.5)
                    {
                        var content = itema.Content + Environment.NewLine + itemb.Content;
                        itema.BeginTime = itemb.BeginTime =
                            itemc != null && itemc.EndTime == itema.BeginTime ? itema.BeginTime :
                            itemc != null && itemc.EndTime == itemb.BeginTime ? itemb.BeginTime :
                            TimeSpan.FromTicks((itema.BeginTime + itemb.BeginTime).Ticks / 2);

                        if (Math.Abs((itema.EndTime - itemb.EndTime).TotalSeconds) < 0.5)
                        {
                            itema.EndTime = TimeSpan.FromTicks((itema.EndTime + itemb.EndTime).Ticks / 2);
                            itemc = itema;
                            ++indexa;
                            ++indexb;
                        }
                        else if (itema.EndTime < itemb.EndTime)
                        {
                            itemb.BeginTime = itema.EndTime;
                            itemc = itema;
                            ++indexa;
                        }
                        else
                        {
                            itema.BeginTime = itemb.EndTime;
                            itemc = itemb;
                            ++indexb;
                        }
                        itemc.Content = content;
                    }
                    else if (itema.BeginTime < itemb.BeginTime)
                    {
                        itemc = new Item() { BeginTime = itema.BeginTime, EndTime = itemb.BeginTime, Content = itema.Content + Environment.NewLine + "." };
                        itema.BeginTime = itemb.BeginTime;
                    }
                    else
                    {
                        itemc = new Item() { BeginTime = itemb.BeginTime, EndTime = itema.BeginTime, Content = "." + Environment.NewLine + itemb.Content };
                        itemb.BeginTime = itema.BeginTime;
                    }
                }

                c.Add(itemc);
            }

            return c.OrderBy(i => i.BeginTime).ToList();
        }

        static List<Item> ParseSrtFile(string filePath)
        {
            List<Item> items = new List<Item>();
            Item item = null;
            var lineCount = 0;
            string line;
            int row = 0;

            using (StreamReader sr = new StreamReader(filePath, Encoding.UTF8))
                while ((line = sr.ReadLine()) != null)
                {
                    ++lineCount;
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) && row > 2)
                    {
                        if (!string.IsNullOrEmpty(item.Content))
                        {
                            items.Add(item);
                        }
                        row = 0;
                    }
                    else if (row == 0)
                    {
                        if (Regex.Match(line, @"^\d+$").Success)
                        {
                            ++row;
                        }
                        else
                        {
                            Console.Error.WriteLine("Srt format error at line: {0}", lineCount);
                            row = 0;
                        }
                    }
                    else if (row == 1)
                    {
                        var times = TryParseTime(line);
                        if (times != null)
                        {
                            item = new Item() { BeginTime = times.Item1, EndTime = times.Item2 };
                            ++row;
                        }
                        else
                        {
                            Console.Error.WriteLine("Srt format error at line: {0}", lineCount);
                            row = 0;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(item.Content))
                        {
                            item.Content = line;
                        }
                        else
                        {
                            item.Content += Environment.NewLine + line;
                        }
                        ++row;
                    }
                }

            if (row > 2)
            {
                items.Add(item);
            }

            return items.OrderBy(i => i.BeginTime).ToList();
        }

        static Tuple<TimeSpan, TimeSpan> TryParseTime(string line)
        {
            var match = Regex.Match(line, @"^(.+)-->(.+)$");
            if (match.Success)
            {
                TimeSpan t1, t2;
                if (TimeSpan.TryParse(match.Groups[1].Value, TimeSpanParsingCulture, out t1) &&
                    TimeSpan.TryParse(match.Groups[2].Value, TimeSpanParsingCulture, out t2))
                {
                    return new Tuple<TimeSpan, TimeSpan>(t1, t2);
                }
            }
            return null;
        }
    }
}