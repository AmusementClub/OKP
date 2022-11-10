
using BencodeNET.Torrents;

namespace OKP.Core.Utils
{
    public class FileSize
    {
        public long Length { get; private set; }

        private static readonly string[] SizeTail = { "B", "KB", "MB", "GB", "TB", "PB" };

        private static string ToString(long length)
        {
            var scale = length == 0 ? 0 : (int)Math.Floor(Math.Log(length, 1024));
            return $"{length / Math.Pow(1024, scale):F3}{SizeTail[scale]}";
        }

        public override string ToString() => ToString(Length);

        public static string FileSizeToString(long length) => ToString(length);

        public FileSize(long length)
        {
            Length = length;
        }
    }

    public class Node : Dictionary<string, Node>
    {
        public FileSize Attribute { get; private set; } = new FileSize(0);

        private Node? ParentNode { get; set; }

        public string NodeName { get; } = ".";

        public override string ToString() => NodeName;

        public Node() { }

        public Node(IEnumerable<IEnumerable<string>> fileList)
        {
            foreach (var list in fileList)
            {
                Insert(list);
            }
        }

        public Node(IEnumerable<(IEnumerable<string> path, FileSize size)> fileList)
        {
            foreach (var (path, size) in fileList)
            {
                Insert(path, size);
            }
        }

        public Node(MultiFileInfoList multiFileInfos)
        {
            foreach (var fileInfo in multiFileInfos)
            {
                Insert(fileInfo.Path, new FileSize(fileInfo.FileSize));
            }
        }

        public Node(SingleFileInfo singleFileInfo)
        {
            Insert(new[] { singleFileInfo.FileName }, new FileSize(singleFileInfo.FileSize));
        }

        private Node(string node)
        {
            NodeName = node;
        }

        public enum NodeTypeEnum
        {
            File,
            Directory
        }

        public NodeTypeEnum NodeType => Values.Count == 0 ? NodeTypeEnum.File : NodeTypeEnum.Directory;

        public IEnumerable<Node> GetFiles()
        {
            return Values.Where(item => item.Count == 0);
        }

        public IEnumerable<Node> GetDirectories()
        {
            return Values.Where(item => item.Count > 0);
        }

        public string FullPath
        {
            get
            {
                var path = NodeName;
                const string separator = "/";
                path = GetParentsNode().Aggregate(path, (current, node) => node + separator + current);
                if (NodeType == NodeTypeEnum.Directory) path += separator;
                return path;
            }
        }

        private IEnumerable<Node> GetParentsNode()
        {
            for (var currentNode = ParentNode; currentNode != null; currentNode = currentNode.ParentNode)
            {
                yield return currentNode;
            }
        }

        public Node Insert(IEnumerable<string> nodes, FileSize? attribute = null)
        {
            var currentNode = this;
            foreach (var node in nodes)
            {
                if (!currentNode.ContainsKey(node))
                {
                    currentNode.Add(node, new Node(node));
                    currentNode[node].ParentNode = currentNode;
                }
                currentNode = currentNode[node];
            }
            if (attribute != null)
            {
                currentNode.Attribute = attribute;
            }
            return currentNode;
        }

        public IEnumerable<string> GetFileList() => GetFileListInner(this);

        private static IEnumerable<string> GetFileListInner(Node currentNode)
        {
            foreach (var file in currentNode.GetDirectories().SelectMany(GetFileListInner)) yield return file;
            foreach (var node in currentNode.GetFiles()) yield return node.FullPath;
        }

        public static IEnumerable<string> GetFileTree(Node currentNode, int indent = 0)
        {
            foreach (var dir in currentNode.GetDirectories())
            {
                yield return string.Format("{0}+{1}", string.Concat(Enumerable.Repeat("|  ", indent)), dir.NodeName);
                foreach (var childNode in  GetFileTree(dir, indent + 1))
                {
                    yield return childNode;
                }
            }
            foreach (var node in currentNode.GetFiles())
            {
                yield return string.Format("{0}{1}({2}", string.Concat(Enumerable.Repeat("|  ", indent)), node.NodeName, node.Attribute);
            }
        }
    }
}
