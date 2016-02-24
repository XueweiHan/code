using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WebCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            var crawler = new Crawler();
            //crawler.RunAsync(80, @"/search?q=Movies&filters=tsource%3a%22dolphin%22+gssort%3a%22ZXh0OnR5cGUub2JqZWN0LmVudGl0eV9zdGF0aWNfcmFuaz1kZXNj%22+gsfilter%3a%22bXNvOmZpbG0uZmlsbS5nZW5yZT0%3d%22+secq%3a%22action+movies%22+segment%3a%22generic.carousel%22+supwlcar%3a%221%22").Wait();
            //crawler.RunAsync(80, "/search?q=tom+cruise+movies").Wait();
            //crawler.RunAsync(80, "/search?q=tom+hanks+and+steven+spielberg+movies").Wait();
            //crawler.RunAsync(10, "/search?q=movies+starring+actor+of+mission+impossible+5").Wait();
            //crawler.RunAsync(80, "/search?q=Movies+starring+Ian+McKellen+directed+by+Peter+Jackson").Wait();
            crawler.RunAsync(80, "/search?q=2015+action+movies").Wait();
            

            Console.WriteLine();
        }
    }

    class Crawler
    {
        int dumpTopError = 100;
        //private static string FlightString = "&setflight=inter1b&setflight=qpnocache&testhooks=1";
        private static string FlightString = "&setflight=qiuia&testhooks=1";

        //ConcurrentDictionary<string, string> parents = new ConcurrentDictionary<string, string>();
        //ConcurrentTireDictionary parents = new ConcurrentTireDictionary();
        ConcurrentCompressedDictionary parents = new ConcurrentCompressedDictionary();
        ConcurrentQueue<UInt64> urls = new ConcurrentQueue<UInt64>();
        List<ErrorLogger> errorLoggers = new List<ErrorLogger>();
        int queryCount = 0;

        readonly static Regex regex = new Regex("href=\"(/search.*?)\"");

        public Crawler()
        {
            foreach (var errorType in Enum.GetValues(typeof(ErrorType)))
            {
                errorLoggers.Add(new ErrorLogger
                {
                    Logger = new StreamWriter(errorType.ToString() + ".txt", true, Encoding.UTF8),
                    Locker = new SemaphoreSlim(1, 1),
                    Count = 0
                });
            }
        }

        private async Task<string> GetHttpContentAsync(string url, HttpClient client)
        {
            const int maxRetry = 5;
            Exception lastException = null;
            for (int retry = 0; retry < maxRetry; ++retry)
            {
                try
                {
                    var result = await client.GetStringAsync(GetHttpLink(url));
                    if (result.StartsWith("<!DOCTYPE") && !result.Contains("snr error page"))
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    lastException = e;
                }

                // get some strange result, FDDosDefender :-) or snr error page
                Console.Write('?');
                await Task.Delay(500);
            }

            throw lastException ?? new Exception("FDDosDefender");
        }

        private async Task<HttpClient> ProcessAllUrlAsync(string url, UInt64 urlId, HttpClient client)
        {
            UInt64 dummyId;
            if (urls.TryPeek(out dummyId))
            {
                await ProcessUrlAsync(url, urlId, client);

                while (urls.TryDequeue(out urlId) && parents.TryGetValue(urlId, out url, out dummyId))
                {
                    await ProcessUrlAsync(url, urlId, client);
                }
            }
            else
            {
                await ProcessUrlAsync(url, urlId, client);
            }

            return client;
        }

        private async Task<HttpClient> ProcessUrlAsync(string url, UInt64 urlId, HttpClient client)
        {
            const int maxRetry = 5;
            try
            {
                string result = null;
                string timeoutResult = null;
                int start = -1;
                int sparse = -1;
                int retry = 0;
                for (; retry < maxRetry; ++retry)
                {
                    try
                    {
                        result = await GetHttpContentAsync(url, client);
                        if (result != null)
                        {
                            start = result.IndexOf("class=\"carousel\"", StringComparison.Ordinal);
                            if (start > 0)
                            {
                                break;
                            }

                            sparse = result.IndexOf("class=\"b_gridList", StringComparison.Ordinal);
                            if (sparse > 0)
                            {
                                break;
                            }

                            timeoutResult = result;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }

                    await Task.Delay(100);  // no carousel, wait and retry
                }

                var count = Interlocked.Increment(ref queryCount);

                if (result == null)
                {
                    await LogBugAsync(urlId, url, ErrorType.IsUnknowError, null, count);
                    return client;
                }

                if (sparse > 0)
                {
                    await LogBugAsync(urlId, url, ErrorType.IsSparse, result, count);
                    return client;
                }

                if (start < 0)
                {
                    await LogBugAsync(urlId, url, ErrorType.NoCarousel, result, count);
                    return client;
                }

                if (retry > 0)
                {
                    await LogBugAsync(urlId, url, ErrorType.CaTimeOut, timeoutResult, count);
                }

                start = result.IndexOf("class=\"iqiui\"", start, StringComparison.Ordinal);
                if (start < 0)
                {
                    await LogBugAsync(urlId, url, ErrorType.NoQiui, result, count);
                    return client;
                }

                int end = result.IndexOf("carousel-controls", start, StringComparison.Ordinal);

                var matches = regex.Matches(result.Substring(start, end - start));
                if (matches.Count == 0)
                {
                    await LogBugAsync(urlId, url, ErrorType.NoFilters, result, count);
                    return client;
                }

                foreach (Match m in matches)
                {
                    var newUrl = HttpUtility.HtmlDecode(m.Groups[1].Value);
                    UInt64 id;
                    if (parents.TryAdd(newUrl, url, out id))
                    {
                        urls.Enqueue(id);
                    }
                }

                Console.Write('.');
                return client;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return client;
        }

        public async Task RunAsync(int concurrentCount, string startUrl)
        {
            UInt64 startId;
            if (parents.TryAdd(startUrl, null, out startId))
            {
                urls.Enqueue(startId);
            }

            var clients = new Stack<HttpClient>(concurrentCount);
            var tasks = new List<Task<HttpClient>>(concurrentCount);
            while (true)
            {
                while (tasks.Count >= concurrentCount)
                {
                    var task = await Task.WhenAny(tasks);
                    clients.Push(await task);
                    task.Dispose();
                    tasks.Remove(task);
                }

                UInt64 urlId, pId;
                string url;
                if (urls.TryDequeue(out urlId) && parents.TryGetValue(urlId, out url, out pId))
                {
                    //var client = clients.Count > 0 ? clients.Pop() : new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
                    var client = clients.Count > 0 ? clients.Pop() : new HttpClient();
                    tasks.Add(ProcessAllUrlAsync(url, urlId, client));
                }
                else
                {
                    if (tasks.Count == 0)
                    {
                        break;
                    }

                    var task = await Task.WhenAny(tasks);
                    clients.Push(await task);
                    task.Dispose();
                    tasks.Remove(task);
                }
            }

            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        private static string GetHttpLink(string url)
        {
            return "http://www.bing.com" + url + FlightString;
        }

        private async Task LogBugAsync(UInt64 urlId, string errorUrl, ErrorType errorType, string html, int qCount)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("/{0}{1}", qCount, Environment.NewLine);
            while (urlId != 0)
            {
                string url;
                parents.TryGetValue(urlId, out url, out urlId);

                sb.Append(GetHttpLink(url));
                sb.AppendLine(DecodeSortAndFilterURl(url));
            }

            var errorLogger = errorLoggers[(int)errorType];
            var logger = errorLogger.Logger;
            var locker = errorLogger.Locker;

            int eCount;
            await locker.WaitAsync();
            try
            {
                eCount = ++errorLogger.Count;
                sb.Insert(0, (100.0 * eCount / qCount).ToString("0.00") + "% " + eCount);
                await logger.WriteLineAsync(sb.ToString());
                await logger.FlushAsync();
            }
            finally
            {
                locker.Release();
            }

            if (eCount <= this.dumpTopError && !string.IsNullOrEmpty(html))
            {
                var dumpFileName = errorType.ToString() + eCount + ".html";
                File.WriteAllText(dumpFileName, string.Format("<!-- {0} -->{1}{2}", GetHttpLink(errorUrl), Environment.NewLine, html));
            }

            Console.Write(errorType.ToString()[2]);
        }

        static readonly Regex fsRegex = new Regex("gs(sort|filter):\"(.*?)\"");

        private string DecodeSortAndFilterURl(string url)
        {
            StringBuilder sb = new StringBuilder();
            url = HttpUtility.UrlDecode(url);
            foreach (Match match in fsRegex.Matches(url))
            {
                sb.AppendFormat("   ({0}:{1})", match.Groups[1].Value, Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[2].Value)));
            }

            return sb.ToString();
        }

        private string ShortUrl(string url)
        {
            return null;
        }
    }

    enum ErrorType
    {
        NoCarousel = 0,
        NoQiui,
        NoFilters,
        CaTimeOut,
        IsSparse,
        IsUnknowError,
    }

    class ErrorLogger
    {
        public StreamWriter Logger;
        public SemaphoreSlim Locker;
        public int Count;
    };
}
