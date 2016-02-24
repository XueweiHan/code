//using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Web.UI;

namespace WebCrawler
{
    class ConcurrentTireDictionary
    {
        public bool TryAdd(string key, string value)
        {
            Node nodeKey = FindOrAddNode(key);
            if (nodeKey.value != null)
            {
                return false;
            }

            Node nodeValue;
            if (!TryFindNode(value, out nodeValue))
            {
                return false;
            }

            return null == Interlocked.CompareExchange(ref nodeKey.value, nodeValue, null);
        }

        public bool TryGetValue(string key, out string value)
        {
            value = null;
            Node node;
            if (!TryFindNode(key, out node))
            {
                return false;
            }

            node = node.value;
            if (node == null)
            {
                return false;
            }

            if (node == root)
            {
                return true;
            }

            StringBuilder sb = new StringBuilder();
            for (; node != root; node = node.parent)
            {
                sb.Insert(0, node.ch);
            }
            value = sb.ToString();

            return true;
        }

        private Node FindOrAddNode(string str)
        {
            Node p = root;
            if (!string.IsNullOrEmpty(str))
            {
                foreach (var ch in str)
                {
                    if (p.children == null)
                    {
                        Interlocked.CompareExchange(ref p.children, new ConcurrentDictionary<char, Node>(), null);
                        //Interlocked.MemoryBarrier();
                    }

                    Node child;
                    if (!p.children.TryGetValue(ch, out child))
                    {
                        child = new Node() {ch = ch, parent = p};
                        if (!p.children.TryAdd(ch, child))
                        {
                            p.children.TryGetValue(ch, out child);
                        }
                    }

                    p = child;
                }
            }
            return p;
        }

        private bool TryFindNode(string str, out Node node)
        {
            node = root;
            if (!string.IsNullOrEmpty(str))
            {
                foreach (var ch in str)
                {
                    if (node.children == null)
                    {
                        return false;
                    }

                    Node child;
                    if (!node.children.TryGetValue(ch, out child))
                    {
                        return false;
                    }

                    node = child;
                }
            }

            return true;
        }

        private Node root = new Node();

        class Node
        {
            public char ch;
            public Node parent = null;
            public Node value = null;
            public ConcurrentDictionary<char, Node> children = null;
        }

        class ConcurrentDictionary<TKey, TValue> : List<Node>
        {
            public bool TryAdd(char key, Node value)
            {
                lock (this)
                {
                    foreach (var node in this)
                    {
                        if (node.ch == key)
                        {
                            return false;
                        }
                    }

                    this.Add(value);
                    return true;
                }
            }

            public bool TryGetValue(char key, out Node value)
            {
                lock (this)
                {
                    foreach (var node in this)
                    {
                        if (node.ch == key)
                        {
                            value = node;
                            return true;
                        }
                    }

                    value = null;
                    return false;
                }
            }
        }


        class MyConcurrentDictionary<TKey, TValue> : Dictionary<TKey, TValue>
        {
            public bool TryAdd(TKey key, TValue value)
            {
                lock (this)
                {
                    if (base.ContainsKey(key))
                    {
                        return false;
                    }
                    base.Add(key, value);
                    return true;
                }
            }

            public new bool TryGetValue(TKey key, out TValue value)
            {
                lock (this)
                {
                    return base.TryGetValue(key, out value);
                }
            }
        }

    }
}
