using System;
using System.IO;
using System.Text.RegularExpressions;
using TagLib;

using File = System.IO.File;

namespace MediaInfo
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var dir in Directory.EnumerateDirectories("."))
            {
                var album = dir.Substring(2);
                foreach (var file in Directory.EnumerateFiles(dir, "*.mp3"))
                {
                    //var path = Path.GetDirectoryName(file);
                    //var name = Path.GetFileName(file);
                    //name = Regex.IsMatch(name, "^\\d\\.") ? "0" + name : name;

                    //Console.WriteLine("move: " + file + "   " + Path.Combine(path, name));
                    //File.Move(file, Path.Combine(path, name));


                    var match = Regex.Match(file, "((\\d+)[^\\d]*)\\.mp3");
                    var title = match.Groups[1].Value.Trim();
                    var track = uint.Parse(match.Groups[2].Value);
                    Console.WriteLine(file);
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        tagFile.RemoveTags(TagTypes.Id3v1);
                        tagFile.RemoveTags(TagTypes.Id3v2);
                        var tags = tagFile.GetTag(TagTypes.Id3v2, true);
                        tags.Album = album;
                        tags.Artists = new[] { "光头王凯" };
                        tags.Title = album + "-" + title;
                        tags.Track = track;
                        tagFile.Save();
                    }
                }
            }
        }
    }
}
