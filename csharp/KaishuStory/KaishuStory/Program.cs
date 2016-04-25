using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KaishuStory
{
    class Program
    {
		/*
		Sample request

		POST http://weixin.kaishustory.com/weixin/media/list HTTP/1.1
		Host: weixin.kaishustory.com
		Content-Length: 73
		Content-Type: application/json

		{"page_num":1,"page_size":3000,"q":"","open_params":"[\"100026100007\"]"}

		*/
        static void Main(string[] args)
        {
            DownlaodAsync("100026100005").Wait();// Xiyouji 1
            DownlaodAsync("100026100007").Wait();// Xiyouji 2
        }

        static async Task DownlaodAsync(string listId)
        {
            string content = ("{'page_num':1,'page_size':3000,'q':'','open_params':'[\\'" + listId + "\\']'}").Replace('\'', '"');

            Console.WriteLine(content);
            using (var client = new HttpClient())
            {
                var postContent = new StringContent(content, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://weixin.kaishustory.com/weixin/media/list", postContent);
                var text = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<dynamic>(text);
                var result = json.result;
                if (result != null)
                {
                    var list = result.media_list;
                    if (list != null)
                    {
                        int i = 0;
                        foreach (var item in list)
                        {
                            Console.WriteLine(item.name);
                            var url = (string)item.media_url;
                            var file = await client.GetByteArrayAsync(url);
                            File.WriteAllBytes(++i + ". " + item.name + ".mp3", file);
                        }
                    }
                }
            }
        }
    }
}
