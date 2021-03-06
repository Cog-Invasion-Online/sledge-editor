﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // Provides an interface to find and load Panda textures (jpg or png) into Sledge.
    // It's PMT meaning Panda Material, but it's just loading regular texture files.
    public class PmtProvider : TextureProvider
    {
        private readonly Dictionary<TexturePackage, QuickRoot> _roots = new Dictionary<TexturePackage, QuickRoot>();

        static string[] _extensions = new string[] { ".mat" };
        static string[] _texextensions = new string[] { ".jpg", ".png" };

        public override IEnumerable<TexturePackage> CreatePackages(IEnumerable<string> sourceRoots, IEnumerable<string> additionalPackages, IEnumerable<string> blacklist, IEnumerable<string> whitelist)
        {
            // For panda don't use the source roots, only the additional packages for texture directories.
            var roots = additionalPackages.ToList();
            
            var packages = new Dictionary<string, TexturePackage>();

            var packageRoot = String.Join(";", roots);

            var texRoot = new QuickRoot(roots, "./");

            Dictionary<string, GenericStructure> matcache = new Dictionary<string, GenericStructure>();

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
                "skyfog",
                "skybox"
            };

            foreach (string tex in texRoot.GetFiles())
            {
                int width = 1;
                int height = 1;
                bool translucent = false;

                var gs = GenericStructure.Parse(new StreamReader(texRoot.OpenFile(tex))).FirstOrDefault();
                if (gs is null)
                {
                    Console.WriteLine("null gs");
                    continue;
                }
                if (!types.Contains(gs.Name.ToLowerInvariant()))
                {
                    continue;
                }
                var basetexture = gs.GetPropertyValue("$basetexture", true);
                if (basetexture is null)
                {
                    Console.WriteLine("null basetexture");
                    continue;
                }

                using (Stream file = texRoot.OpenTexFile(basetexture))
                {
                    if (file is null)
                    {
                        Console.WriteLine("Texture not found {0}", basetexture);
                        continue;
                    }

                    using (Image img = Image.FromStream(stream: file, useEmbeddedColorManagement: false, validateImageData: false))
                    {
                        width = (int)img.PhysicalDimension.Width;
                        height = (int)img.PhysicalDimension.Height;
                        translucent = (img.Flags & (int)ImageFlags.HasTranslucent) != 0;
                    }
                }

                var idx = tex.LastIndexOf('/');
                var dir = idx >= 0 ? tex.Substring(0, idx) : "";

                if (!packages.ContainsKey(dir))
                    packages.Add(dir, new TexturePackage(packageRoot, dir, this));

                TextureFlags flags;
                if (translucent)
                    flags = TextureFlags.Transparent;
                else
                    flags = TextureFlags.None;

                packages[dir].AddTexture(new TextureItem(packages[dir], tex, flags, basetexture, width, height));
            }

            foreach (var tp in packages.Values)
            {
                _roots.Add(tp, texRoot);
            }

            return packages.Values;
        }

        private static string[] GetExtensions()
        {
            return _extensions;
        }

        public static bool HasExtension(string path)
        {
            foreach (string ext in _extensions)
            {
                if (path.EndsWith(ext))
                {
                    return true;
                }
            }

            return false;
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
                var files = group.Where(ti => root.HasTexFile(ti.PrimarySubItem.Name)).ToList();
                foreach (var ti in files)
                {
                    using (Bitmap bmp = GetBitmap(root.OpenTexFile(ti.PrimarySubItem.Name)))
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
                return _roots.Any(x => x.HasTexFile(item.PrimarySubItem.Name));
            }

            public Bitmap GetImage(TextureItem item)
            {
                var root = _roots.FirstOrDefault(x => x.HasTexFile(item.PrimarySubItem.Name));
                if (root == null) return null;
                var stream = root.OpenTexFile(item.PrimarySubItem.Name);
                if (stream == null) return null;

                using (stream)
                {
                    return GetBitmap(stream);
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
            private readonly List<string> _lExtensions;

            public QuickRoot(IEnumerable<string> roots, string baseFolder)
            {
                _baseFolder = baseFolder;
                _lExtensions = _extensions.ToList();
                _roots = roots.ToList();
                var streams = _roots
                    .Where(Directory.Exists)
                    .Select(x => new FileInfo(x))
                    .Where(x => x.Exists)
                    .AsParallel()
                    .Select(x => new { Directory = x })
                    .ToList();
                _files = new List<string>();

                foreach (string extension in _lExtensions)
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
                
                return path.TrimStart('/');
            }

            public IEnumerable<string> GetFiles()
            {
                return _files;
            }

            public bool HasFileExtension(string path)
            {
                if (_roots.Any(x => File.Exists(Path.Combine(x, _baseFolder, path))))
                {
                    return true;
                }

                return false;
            }

            public bool HasTexFile(string path)
            {
                path = StripFirstFolder(path);
                if (!HasTexExtension(path))
                {
                    foreach (string ext in _extensions)
                    {
                        if (HasTexExtension(path + ext))
                        {
                            return true;
                        }
                    }

                }
                else
                {
                    if (HasTexExtension(path))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool HasFile(string path)
            {
                path = StripFirstFolder(path);
                if (!HasExtension(path))
                {
                    foreach (string ext in _extensions)
                    {
                        if (HasFileExtension(path + ext))
                        {
                            return true;
                        }
                    }
                    
                }
                else
                {
                    if (HasFileExtension(path))
                    {
                        return true;
                    }
                }

                return false;
            }

            public Stream OpenFileExtension(string path)
            {
                foreach (var root in _roots)
                {
                    var p = Path.Combine(root, _baseFolder, path);
                    if (File.Exists(p))
                    {
                        return File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    }

                }
                return null;
            }

            public bool HasTexExtension(string path)
            {
                foreach (string ext in _texextensions)
                {
                    if (path.EndsWith(ext))
                        return true;
                }

                return false;
            }

            public Stream OpenFile(string path)
            {
                path = StripFirstFolder(path);
                return OpenFileExtension(path);
            }

            public Stream OpenTexFile(string path)
            {
                path = StripFirstFolder(path);

                //if (!HasExtension(path))
                //{
                //    foreach (string ext in _extensions)
                //    {
                //        Stream result = OpenFileExtension(path + ext);
                //        if (result != null)
                //        {
                //            return result;
                //        }
                //    }
                //}
                //else
                // {
                //    Stream result = OpenFileExtension(path);
                //    if (result != null)
                //    {
                //        return result;
                //    }
                //}

                if (!HasTexExtension(path))
                    return null;

                return OpenFileExtension(path);
            }

            public void Dispose()
            {
            }
        }
    }
}
