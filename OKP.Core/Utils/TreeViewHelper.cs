
namespace OKP.Core.Utils
{
    public class FileSize
    {
        public long Length { get; private set; }

        private static readonly string[] SizeTail = { "B", "KB", "MB", "GB", "TB", "PB" };

        private static string _toString(long length)
        {
            var scale = length == 0 ? 0 : (int)Math.Floor(Math.Log(length, 1024));
            return $"{length / Math.Pow(1024, scale):F3}{SizeTail[scale]}";
        }

        public override string ToString() => _toString(Length);

        public static string FileSizeToString(long length) => _toString(length);

        public FileSize(long length)
        {
            Length = length;
        }
    }

    public class Node : Dictionary<string, Node>
    {
        public FileSize Attribute { get; private set; }

        private Node ParentNode { get; set; }

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
            foreach (var list in fileList)
            {
                Insert(list.path, list.size);
            }
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

        public Node Insert(IEnumerable<string> nodes, FileSize attribute = null)
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
            currentNode.Attribute = attribute;
            return currentNode;
        }

        public IEnumerable<string> GetFileList() => GetFileListInner(this);

        private static IEnumerable<string> GetFileListInner(Node currentNode)
        {
            foreach (var file in currentNode.GetDirectories().SelectMany(GetFileListInner)) yield return file;
            foreach (var node in currentNode.GetFiles()) yield return node.FullPath;
        }
    }
}
