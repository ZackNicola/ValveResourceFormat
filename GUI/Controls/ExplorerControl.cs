using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using GUI.Utils;
using ValveKeyValue;

namespace GUI.Controls
{
    public partial class ExplorerControl : UserControl
    {
        private List<(TreeNode ParentNode, int AppID, TreeNode[] Children)> TreeData = new();

        public ExplorerControl()
        {
            InitializeComponent();

#if DEBUG
            var timer = Stopwatch.StartNew();
#endif

            try
            {
                treeView.BeginUpdate();
                treeView.ImageList = MainForm.ImageList;
                Scan();
            }
            finally
            {
                treeView.EndUpdate();
            }

#if DEBUG
            timer.Stop();
            Console.WriteLine($"Explorer scan time: {timer.Elapsed}");
#endif
        }

        private void Scan()
        {
            var vpkImage = MainForm.ImageList.Images.IndexOfKey("vpk");
            var vcsImage = MainForm.ImageList.Images.IndexOfKey("vcs");
            var mapImage = MainForm.ImageList.Images.IndexOfKey("map");
            var folderImage = MainForm.ImageList.Images.IndexOfKey("_folder");

            var steam = Settings.GetSteamPath();

            var vpkRegex = new Regex(@"_[0-9]{3}\.vpk$");
            var kvDeserializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

            var libraryfolders = Path.Join(steam, "libraryfolders.vdf");
            KVObject libraryFoldersKv;

            using (var libraryFoldersStream = File.OpenRead(libraryfolders))
            {
                libraryFoldersKv = kvDeserializer.Deserialize(libraryFoldersStream, KVSerializerOptions.DefaultOptions);
            }

            var steamPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };

            foreach (var child in libraryFoldersKv.Children)
            {
                steamPaths.Add(Path.GetFullPath(Path.Join(child["path"].ToString(), "steamapps")));
            }

            foreach (var steamPath in steamPaths)
            {
                var manifests = Directory.GetFiles(steamPath, "appmanifest_*.acf");

                Parallel.ForEach(manifests, (appManifestPath) =>
                {
                    KVObject appManifestKv;

                    try
                    {
                        using var appManifestStream = File.OpenRead(appManifestPath);
                        appManifestKv = kvDeserializer.Deserialize(appManifestStream, KVSerializerOptions.DefaultOptions);
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    var appId = appManifestKv["appid"].ToInt32(CultureInfo.InvariantCulture);
                    var appName = appManifestKv["name"].ToString();
                    var installDir = appManifestKv["installdir"].ToString();

                    var gamePath = Path.Combine(steamPath, "common", installDir);
                    var allFoundGamePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (!Directory.Exists(gamePath))
                    {
                        return;
                    }

                    var gameInfos = Directory.GetFiles(gamePath, "gameinfo.gi", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        MaxRecursionDepth = 10,
                    });

                    foreach (var file in gameInfos)
                    {
                        KVObject gameInfo;

                        try
                        {
                            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read);
                            gameInfo = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        var gameRoot = Path.GetDirectoryName(Path.GetDirectoryName(file));

                        foreach (var searchPath in (IEnumerable<KVObject>)gameInfo["FileSystem"]["SearchPaths"])
                        {
                            if (searchPath.Name != "Game")
                            {
                                continue;
                            }

                            var path = Path.Combine(gameRoot, searchPath.Value.ToString());

                            if (Directory.Exists(path))
                            {
                                allFoundGamePaths.Add(path);
                            }
                        }
                    }

                    var foundFiles = new List<TreeNode>();

                    foreach (var path in allFoundGamePaths)
                    {
                        var vpks = Directory.GetFiles(path, "*.vpk", new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            MaxRecursionDepth = 5,
                        });

                        foreach (var vpk in vpks)
                        {
                            if (vpkRegex.IsMatch(vpk))
                            {
                                continue;
                            }

                            var image = vpkImage;

                            if (Path.GetFileName(vpk).StartsWith("shaders_", StringComparison.Ordinal))
                            {
                                image = vcsImage;
                            }
                            else if (vpk[path.Length..].StartsWith($"{Path.DirectorySeparatorChar}maps{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                            {
                                image = mapImage;
                            }

                            var vpkName = vpk[(gamePath.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
                            var toAdd = new TreeNode(vpkName)
                            {
                                Tag = vpk,
                                ImageIndex = image,
                                SelectedImageIndex = image,
                            };

                            foundFiles.Add(toAdd);
                        }
                    }

                    if (foundFiles.Count > 0)
                    {
                        foundFiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                        var foundFilesArray = foundFiles.ToArray();

                        var treeNodeName = $"[{appId}] {appName} - {gamePath.Replace(Path.DirectorySeparatorChar, '/')}";
                        var treeNode = new TreeNode(treeNodeName)
                        {
                            Tag = gamePath,
                            ImageIndex = folderImage,
                            SelectedImageIndex = folderImage,
                        };
                        treeNode.Nodes.AddRange(foundFilesArray);
                        treeNode.Expand();

                        lock (TreeData)
                        {
                            TreeData.Add((treeNode, appId, foundFilesArray));
                        }
                    }
                });
            }

            // Recent files
            {
                var recentFiles = GetRecentFileNodes();
                var recentImage = MainForm.ImageList.Images.IndexOfKey("_recent");
                var recentFilesTreeNode = new TreeNode("Recent files")
                {
                    ImageIndex = recentImage,
                    SelectedImageIndex = recentImage,
                };
                recentFilesTreeNode.Nodes.AddRange(recentFiles);
                recentFilesTreeNode.Expand();

                TreeData.Add((recentFilesTreeNode, -1, recentFiles));
            }

            TreeData.Sort((a, b) => a.AppID - b.AppID);

            treeView.Nodes.AddRange(TreeData.Select(node => node.ParentNode).ToArray());
        }

        private void OnTreeViewNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var path = (string)e.Node.Tag;

            if (File.Exists(path))
            {
                Program.MainForm.OpenFile(path);
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = path + Path.DirectorySeparatorChar,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        private void OnFilterTextBoxTextChanged(object sender, EventArgs e)
        {
            treeView.BeginUpdate();
            treeView.Nodes.Clear();

            var foundNodes = new List<TreeNode>(TreeData.Count);

            foreach (var node in TreeData)
            {
                node.ParentNode.Nodes.Clear();

                var foundChildren = Array.FindAll(node.Children, (child) =>
                {
                    return child.Text.Contains(filterTextBox.Text, StringComparison.OrdinalIgnoreCase);
                });

                if (foundChildren.Any())
                {
                    node.ParentNode.Nodes.AddRange(foundChildren);
                    foundNodes.Add(node.ParentNode);
                }
            }

            treeView.Nodes.AddRange(foundNodes.ToArray());
            treeView.EndUpdate();
        }

        private void OnVisibleChanged(object sender, EventArgs e)
        {
            // Refresh recent files list whenever explorer becomes visible
            if (!Visible)
            {
                return;
            }

            var recentFiles = GetRecentFileNodes();
            var recentFilesNode = TreeData.Find(node => node.AppID == -1);
            recentFilesNode.ParentNode.Nodes.Clear();
            recentFilesNode.ParentNode.Nodes.AddRange(recentFiles);
            recentFilesNode.Children = recentFiles;
        }

        private static TreeNode[] GetRecentFileNodes()
        {
            return Settings.Config.RecentFiles.Select(path =>
            {
                var imageIndex = MainForm.GetImageIndexForExtension(Path.GetExtension(path));
                var toAdd = new TreeNode(path)
                {
                    Tag = path,
                    ImageIndex = imageIndex,
                    SelectedImageIndex = imageIndex,
                };

                return toAdd;
            }).Reverse().ToArray();
        }

    }
}
