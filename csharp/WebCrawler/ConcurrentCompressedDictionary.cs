using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace WebCrawler
{
    class ConcurrentCompressedDictionary
    {
        public bool TryAdd(string key, string value, out UInt64 id)
        {
            id = KeyShorter(key);
            var pid = value == null ? 0UL : KeyShorter(value);
            Item item = new Item() {url = key, parent = pid};
            return dict.TryAdd(KeyShorter(key), item);
        }

        public bool TryGetValue(UInt64 id, out string value, out UInt64 parent)
        {
            Item item;
            if (dict.TryGetValue(id, out item))
            {
                value = item.url;
                parent = item.parent;
                return true;
            }
            value = null;
            parent = 0UL;
            return false;
        }

        private UInt64 KeyShorter(string key)
        {
            MD5 md5;
            if (!md5s.TryPop(out md5))
            {
                md5 = MD5.Create();
            }

            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));

            md5s.Push(md5);
            return BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8);
        }

        private readonly ConcurrentDictionary<UInt64, Item> dict = new ConcurrentDictionary<UInt64, Item>();
        private readonly ConcurrentStack<MD5> md5s = new ConcurrentStack<MD5>();

        struct Item
        {
            public string url;
            public UInt64 parent;
        }
    }
}
