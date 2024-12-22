namespace tmdl_utility;

public partial class ModelUtility
{
    public class Node
    {
        public string name;
        public Vec3 Position = new Vec3();
        public Vec4 Rotation = new Vec4(0,0,0,1);
        public Vec3 Scale = new Vec3(1);

        public List<int> Meshes = new List<int>();
        public bool IsBone;

        public List<Node> children = new List<Node>();
        public Node parent = null;

        public int id = -1;

        public Node(string name = "Node")
        {
            this.name = name;
        }

        public Node(Bone bone)
        {
            this.name = bone.name;
            IsBone = true;
            this.Position = bone.Position;
            this.Rotation = bone.Rotation;
            this.Scale = bone.Scale;

            foreach (var boneChild in bone.children)
            {
                var n = new Node(boneChild);
                n.SetParent(this);
            }
        }

        public void SetParent(Node n)
        {
            if (parent != null)
                parent.children.Remove(this);

            parent = n;
            parent.children.Add(this);
        }

        public void AddChild(Node n)
        {
            n.SetParent(this);
        }

        public void Write(ModelWriter writer)
        {
            writer.WriteNonSigString(name);

            Position.Write(writer);
            Rotation.Write(writer);
            Scale.Write(writer);

            writer.Write(Meshes.Count);
            foreach (var idx in Meshes)
            {
                writer.Write(idx);
            }

            writer.Write(IsBone);

            writer.Write(children.Count);
            foreach (var child in children)
            {
                child.Write(writer);
            }
        }

        public Node GetChild(string name)
        {
            foreach (var child in children)
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }

        public bool RemoveChild(string name)
        {
            int i = 0;
            bool found = false;
            foreach (var child in children)
            {
                if (child.name == name)
                {
                    found = true;
                    break;
                }
                else
                {
                    found = child.RemoveChild(name);
                    if (found)
                    {
                        return true;
                    }
                }

                i++;
            }

            if (found)
                children.RemoveAt(i);
            return found;
        }

        public List<Node> GetAllChildren()
        {
            var c = new List<Node>();

            c.AddRange(children);
            foreach (var child in children)
            {
                c.AddRange(child.GetAllChildren());
            }

            return c;
        }

        public Node AddNode(string name)
        {
            var node = new Node();

            node.name = name;

            node.SetParent(this);

            return node;
        }

        public void AddNode(Node node)
        {
            node.SetParent(this);
        }
    }

    public class Scene
    {
        public string name;

        public List<Model> models = new List<Model>();

        public Node rootNode;

        public void Write(ModelWriter writer)
        {
            writer.WriteString("TSCN");
            writer.WriteNonSigString(name);

            rootNode.Write(writer);

            writer.Write(models.Count);
            foreach (var model in models)
            {
                model.Write(writer);
            }
        }

        public bool RemoveNode(string name)
        {
            if (rootNode.name == name)
            {
                rootNode = new Node();
                return true;
            }
            else
            {
                return rootNode.RemoveChild(name);
            }
        }

        public Node GetNode(string name)
        {
            if (rootNode.name == name)
                return rootNode;

            var nodes = rootNode.GetAllChildren();
            return (nodes.Find(x => x.name == name) ?? null)!;
        }
    }
}