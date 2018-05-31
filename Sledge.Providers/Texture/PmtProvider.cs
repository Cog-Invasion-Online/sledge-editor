using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Sledge.Common;
using Sledge.Graphics.Helpers;
using Sledge.Packages;
using Sledge.Packages.Vpk;

namespace Sledge.Providers.Texture
{
    // Panda material file... lol
    public class PmtProvider : TextureProvider
    {
        private readonly Dictionary<TexturePackage, QuickRoot> _roots = new Dictionary<TexturePackage, QuickRoot>();

        public override IEnumerable<TexturePackage> CreatePackages(IEnumerable<string> sourceRoots, IEnumerable<string> additionalPackages, IEnumerable<string> blacklist, IEnumerable<string> whitelist)
        {
            var blist = blacklist.Select(x => x.TrimEnd('/', '\\')).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();
            var wlist = whitelist.Select(x => x.TrimEnd('/', '\\')).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();

            // For panda don't use the source roots, only the additional packages for texture directories.
            var roots = additionalPackages.ToList();

            foreach (string root in roots)
            {
                Console.WriteLine(root);
            }
            
            var packages = new Dictionary<string, TexturePackage>();

            var types = new HashSet<string>
            {
                "unlitgeneric",
                "lightmappedgeneric",
                "lightmappedreflective",
                "water",
                "sprite",
                "decalmodulate",
                "modulate",
                "subrect",
                "worldvertextransition",
                "lightmapped_4wayblend",
                "unlittwotexture",
                "worldtwotextureblend",
                "skyfog"
            };

            var packageRoot = String.Join(";", roots);

            string[] extensions = { ".jpg", ".png" };

            //var pmtRoot = new QuickRoot(roots, add, "./", ".pmt");
            var texRoot = new QuickRoot(roots, new List<string>(), "./", extensions.ToList());

            const StringComparison ctype = StringComparison.InvariantCultureIgnoreCase;

            foreach (string tex in texRoot.GetFiles())
            {
                

                Stream str = texRoot.OpenFile(tex);
                if (str == null)
                {
                    continue;
                }
                Bitmap bmp = GetBitmap(str);
                if (bmp == null)
                {
                    continue;
                }
                var idx = tex.LastIndexOf('/');
                var dir = idx >= 0 ? tex.Substring(0, idx) : "";

                if (!packages.ContainsKey(dir))
                    packages.Add(dir, new TexturePackage(packageRoot, dir, this));

                packages[dir].AddTexture(new TextureItem(packages[dir], tex, TextureFlags.None, tex, bmp.Width, bmp.Height));
            }

            foreach (var tp in packages.Values)
            {
                _roots.Add(tp, texRoot);
            }

            return packages.Values;
        }

        private TextureFlags GetFlags(GenericStructure vmt)
        {
            var flags = TextureFlags.None;
            var tp = vmt.PropertyInteger("$translucent") + vmt.PropertyInteger("$alphatest");
            if (tp > 0) flags |= TextureFlags.Transparent;
            return flags;
        }

        public override void DeletePackages(IEnumerable<TexturePackage> packages)
        {
            var packs = packages.ToList();
            var roots = _roots.Where(x => packs.Contains(x.Key)).Select(x => x.Value).ToList();
            foreach (var tp in packs)
            {
                _roots.Remove(tp);
            }
            foreach (var root in roots.Where(x => !_roots.ContainsValue(x)))
            {
                root.Dispose();
            }
        }
        
        private static Bitmap GetBitmap(Stream stream)
        {
            Image img = Image.FromStream(stream);
            if (img == null)
            {
                return null;
            }

            return new Bitmap(img);
        }

        public override void LoadTextures(IEnumerable<TextureItem> items)
        {
            var groups = items.GroupBy(x => x.Package).ToList();
            foreach (var group in groups)
            {
                var root = _roots[group.Key];
                var files = group.Where(ti => root.HasFile(ti.PrimarySubItem.Name)).ToList();
                foreach (var ti in files)
                {
                    using (Bitmap bmp = GetBitmap(root.OpenFile(ti.PrimarySubItem.Name)))
                    {
                        TextureHelper.Create(ti.Name, bmp, ti.Width, ti.Height, ti.Flags);
                    }
                }
            }
        }

        public override ITextureStreamSource GetStreamSource(int maxWidth, int maxHeight, IEnumerable<TexturePackage> packages)
        {
            return new NewTexStreamSource(maxWidth, maxHeight, packages, packages.Select(x => _roots[x]));
        }

        private class NewTexStreamSource : ITextureStreamSource
        {
            private readonly int _maxWidth;
            private readonly int _maxHeight;
            private readonly List<QuickRoot> _roots;
            
            public NewTexStreamSource(int maxWidth, int maxHeight, IEnumerable<TexturePackage> packages, IEnumerable<QuickRoot> roots)
            {
                _maxWidth = maxWidth;
                _maxHeight = maxHeight;
                _roots = roots.ToList();
            }
            
            public bool HasImage(TextureItem item)
            {
                return _roots.Any(x => x.HasFile(item.PrimarySubItem.Name));
            }

            public Bitmap GetImage(TextureItem item)
            {
                var root = _roots.FirstOrDefault(x => x.HasFile(item.PrimarySubItem.Name));
                if (root == null) return null;
                var stream = root.OpenFile(item.PrimarySubItem.Name);
                if (stream == null) return null;

                using (stream)
                {
                    return GetBitmap(stream);// Vtf.VtfProvider.GetImage(stream, _maxWidth, _maxHeight);
                }
            }
            
            public void Dispose()
            {

            }
        }

        private class QuickRoot : IDisposable
        {
            private readonly List<string> _roots;
            private readonly List<string> _files;
            private readonly string _baseFolder;
            private readonly List<string> _extensions;
            private List<string> _extras;

            public QuickRoot(IEnumerable<string> roots, IEnumerable<string> additional, string baseFolder, List<string> extensions)
            {
                _baseFolder = baseFolder;
                _extensions = extensions;
                _roots = roots.ToList();
                var streams = _roots
                    .Where(Directory.Exists)
                    .Select(x => new FileInfo(x))
                    .Where(x => x.Exists)
                    .AsParallel()
                    .Select(x => new { Directory = x })
                    .ToList();
                _extras = additional.Select(x => Directory.Exists(Path.Combine(x, baseFolder)) ? Path.Combine(x, baseFolder) : x).Where(Directory.Exists).ToList();
                _files = new List<string>();

                foreach (string extension in extensions)
                {
                    foreach (string root in _roots)
                    {
                        if (Directory.Exists(Path.Combine(root, baseFolder)))
                        {
                            foreach (string file in Directory.GetFiles(Path.Combine(root, baseFolder), "*" + extension, SearchOption.AllDirectories))
                            {
                                _files.Add(MakeRelative(root, file));
                            }
                        }
                    }

                    /*
                    //Console.WriteLine(Path.Combine(_roots[0], baseFolder, "*" + extension));
                    Console.WriteLine(Directory.Exists(Path.Combine(_roots[0], baseFolder)));
                    string[] testFiles = Directory.GetFiles(Path.Combine(_roots[0], baseFolder), "*" + extension, SearchOption.AllDirectories);
                    foreach (string tfile in testFiles)
                    {
                        Console.WriteLine(tfile);
                    }
                    _files
                    .Union(_roots.Where(x => Directory.Exists(Path.Combine(x, baseFolder)))
                        .SelectMany(x => Directory.GetFiles(Path.Combine(x, baseFolder), "*" + extension, SearchOption.AllDirectories)
                            .Select(f => MakeRelative(x, f))))
                    .Union(_extras.SelectMany(x => Directory.GetFiles(x, "*" + extension, SearchOption.AllDirectories)
                        .Select(f => MakeRelative(x, f)))
                     */
                    _files
                       .GroupBy(x => x)
                       .ToList();
                    for (int i = 0; i < _files.Count; i++)
                    {
                        _files[i] = StripBase(_files[i]);
                    }
                }
                    
            }

            private string MakeRelative(string baseFolder, string file)
            {
                string full = Path.GetFullPath(baseFolder);
                string[] folders = full.Split('\\');
                string lastFolder = folders[folders.Length - 1];

                string relative = lastFolder + "/" + Path.GetFullPath(file).Substring(full.Length).TrimStart('\\', '/').Replace('\\', '/');
                return relative;
            }

            private string StripFirstFolder(string path)
            {
                string[] folders = path.Split('/');
                return path.Substring(folders[0].Length + 1);
            }

            private string StripBase(string path)
            {
                if (path.StartsWith(_baseFolder)) path = path.Substring(_baseFolder.Length);
                foreach (string extension in _extensions)
                {
                    if (path.EndsWith(extension))
                        path = path.Substring(0, path.Length - extension.Length);
                }
                
                return path.TrimStart('/');
            }

            public IEnumerable<string> GetFiles()
            {
                return _files;
            }

            public bool HasFile(string path)
            {
                path = StripFirstFolder(path);
                foreach (string extension in _extensions)
                {
                    if (_extras.Any(x => File.Exists(Path.Combine(x, path + extension)))
                       || _roots.Any(x => File.Exists(Path.Combine(x, _baseFolder, path + extension))))
                    {
                        return true;
                    }
                }

                return false;
            }

            public Stream OpenFile(string path)
            {
                path = StripFirstFolder(path);

                foreach (var extra in _extras)
                {
                    foreach (string extension in _extensions)
                    {
                        var p = Path.Combine(extra, path + extension);
                        if (File.Exists(p))
                        {
                            return File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        }
                    }
                    
                }
                foreach (var root in _roots)
                {
                    foreach (string extension in _extensions)
                    {
                        var p = Path.Combine(root, _baseFolder, path + extension);
                        if (File.Exists(p))
                        {
                            return File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        }
                    }
                    
                }
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}
