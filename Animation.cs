namespace tmdl_utility;

public partial class ModelUtility
{
    public class Bone
    {
        public string name;
        public Vec3 Position = new Vec3();
        public Vec4 Rotation = new Vec4();
        public Vec3 Scale = new Vec3(1);
        public Node node;
        public int id = -1;


        public Bone parent;
        public List<Bone> children = new List<Bone>();

        public void SetParent(Bone b)
        {
            if (parent != null)
                parent.children.Remove(this);
            parent = b;
            parent.children.Add(this);
        }

        public void AddChild(Bone b)
        {
            b.SetParent(this);
        }


        public void Write(ModelWriter writer)
        {
            writer.WriteNonSigString(name);

            writer.Write(id);

            Position.Write(writer);
            Rotation.Write(writer);
            Scale.Write(writer);
        }

        public Bone(Node node)
        {
            this.name = node.name;
            this.id = node.id;
            this.node = node;

            this.Position = node.Position;
            this.Rotation = node.Rotation;
            this.Scale = node.Scale;

            node.IsBone = true;
        }

        public Bone()
        {
        }
    }

    public class Skeleton
    {
        public List<Bone> bones = new List<Bone>();
        public string rootName;

        public void Write(ModelWriter writer)
        {
            writer.Write(bones.Count);

            foreach (var bone in bones)
            {
                bone.Write(writer);
            }

            writer.WriteNonSigString(rootName);
        }

        public Bone GetBone(string name)
        {
            foreach (var bone in bones)
            {
                if (bone.name == name)
                    return bone;
            }

            return null;
        }

        public Node ToNode()
        {
            var rootNode = new Node(bones[0]);

            return rootNode;
        }

        public Skeleton(Node rootNode)
        {
            var nodes = rootNode.GetAllChildren();
            rootName = rootNode.name;

            var rootBone = new Bone(rootNode);
            rootBone.id = bones.Count;
            bones.Add(rootBone);

            foreach (var node in nodes)
            {
                var b = new Bone(node);

                b.id = bones.Count;

                bones.Add(b);
            }

            foreach (var node in nodes)
            {
                var bone = GetBone(node.name);
                if (node.parent != null)
                {
                    var parent = GetBone(node.parent.name);
                    bone.SetParent(parent);
                }
            }
        }
    }

    public class Animation
    {
        public class NodeChannel
        {
            public string NodeName;
            public Bone Bone;

            public List<Key<Vec3>> Positions = new List<Key<Vec3>>();
            public List<Key<Vec4>> Rotations = new List<Key<Vec4>>();
            public List<Key<Vec3>> Scales = new List<Key<Vec3>>();

            private const float KeyToleration = 0.01f;

            public void AddPosition(Key<Vec3> p)
            {
                foreach (var position in Positions)
                {
                    if (Math.Abs(position.timeStamp - p.timeStamp) < KeyToleration)
                    {
                        position.value += p.value;
                        return;
                    }
                }

                Positions.Add(p);
            }

            public void AddRotation(Key<Vec4> r)
            {
                foreach (var rotation in Rotations)
                {
                    if (Math.Abs(rotation.timeStamp - r.timeStamp) < KeyToleration)
                    {
                        rotation.value += r.value;
                        return;
                    }
                }

                Rotations.Add(r);
            }

            public void AddScale(Key<Vec3> s)
            {
                foreach (var scale in Scales)
                {
                    if (Math.Abs(scale.timeStamp - s.timeStamp) < KeyToleration)
                    {
                        scale.value += s.value;
                        return;
                    }
                }

                Scales.Add(s);
            }
        }

        public Dictionary<string, NodeChannel> nodeChannels = new Dictionary<string, NodeChannel>();
        public float duration;
        public int ticksPerSecond;
        public string name;

        public Animation()
        {
        }

        public Animation(Assimp.Animation anim)
        {
            name = anim.Name;
            duration = (float)anim.DurationInTicks;
            ticksPerSecond = (int)anim.TicksPerSecond;

            foreach (var ch in anim.NodeAnimationChannels)
            {
                var channel = new NodeChannel();
                channel.NodeName = ch.NodeName;

                foreach (var pkey in ch.PositionKeys)
                {
                    channel.Positions.Add(new Key<Vec3>((float)pkey.Time, new Vec3(pkey.Value)));
                }

                foreach (var pkey in ch.RotationKeys)
                {
                    channel.Rotations.Add(new Key<Vec4>((float)pkey.Time, new Vec4(pkey.Value)));
                }

                foreach (var pkey in ch.ScalingKeys)
                {
                    channel.Scales.Add(new Key<Vec3>((float)pkey.Time, new Vec3(pkey.Value)));
                }

                nodeChannels.Add(channel.NodeName, channel);
            }
        }

        public void ApplySkeleton(Skeleton skeleton)
        {
            foreach (var (name, channel) in nodeChannels)
            {
                channel.Bone = skeleton.GetBone(name);
            }
        }


        public void Write(ModelWriter writer)
        {
            writer.WriteString("TANM");

            writer.WriteNonSigString(name);
            writer.Write(duration);
            writer.Write(ticksPerSecond);

            var channelCount = nodeChannels.Count;
            foreach (var keyValuePair in nodeChannels)
            {
                if (keyValuePair.Value.Bone == null)
                    channelCount--;
            }


            writer.Write(channelCount);
            foreach (var (name, channel) in nodeChannels)
            {
                if (channel.Bone == null)
                    continue;

                writer.WriteNonSigString(channel.NodeName);
                writer.Write(channel.Bone.id);

                writer.Write(channel.Positions.Count);
                foreach (var pkey in channel.Positions)
                {
                    writer.Write(pkey.timeStamp);

                    pkey.value.Write(writer);
                }

                writer.Write(channel.Rotations.Count);
                foreach (var pkey in channel.Rotations)
                {
                    writer.Write(pkey.timeStamp);

                    pkey.value.Write(writer);
                }

                writer.Write(channel.Scales.Count);
                foreach (var pkey in channel.Scales)
                {
                    writer.Write(pkey.timeStamp);

                    pkey.value.Write(writer);
                }
            }
        }
    }
}