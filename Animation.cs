using System.Numerics;

namespace tmdl_utility;

public partial class ModelUtility
{
    public class Key<T>
    {
        public float timeStamp;

        public T value;

        public Key(float time, T val)
        {
            timeStamp = time;
            value = val;
        }

        public float Time
        {
            get => timeStamp;
            set => timeStamp = value;
        }
    }

    public class Bone
    {
        public List<Bone> children = new();
        public int id = -1;
        public string name;
        public Node node;
        public Matrix4x4 offsetMatrix = Matrix4x4.Identity;


        public Bone parent;
        public Vec3 Position = new();
        public Vec4 Rotation = new(0, 0, 0, 1);
        public Vec3 Scale = new(1);

        public Bone(Node node)
        {
            name = node.name;
            id = node.id;
            this.node = node;

            Position = node.Position;
            Rotation = node.Rotation;
            Scale = node.Scale;

            node.IsBone = true;
        }

        public Bone()
        {
        }

        public static Matrix4x4 CalculateTransformMatrix(Bone bone)
        {
            return Matrix4x4.CreateScale(bone.Scale) *
                   Matrix4x4.CreateFromQuaternion(bone.Rotation) *
                   Matrix4x4.CreateTranslation(bone.Scale);
        }

        public static (Matrix4x4, Matrix4x4) CalculateOffsetMatrix(Bone bone)
        {
            var mat = Matrix4x4.Identity;

            if (bone.parent != null) mat *= CalculateOffsetMatrix(bone.parent).Item1;

            mat = Matrix4x4.Multiply(CalculateTransformMatrix(bone), mat);

            Matrix4x4.Invert(mat, out var inverse);

            return (mat, inverse);
        }

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

            for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
                writer.Write(offsetMatrix[i, j]);

            writer.Write(children.Count);
            foreach (var child in children) writer.WriteNonSigString(child.name);
        }
    }

    public class Skeleton
    {
        public List<Bone> bones = new();
        public string rootName;

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


        public void Write(ModelWriter writer)
        {
            writer.Write(bones.Count);

            foreach (var bone in bones) bone.Write(writer);

            writer.WriteNonSigString(rootName);
        }

        public Bone GetBone(string name)
        {
            foreach (var bone in bones)
                if (bone.name == name)
                    return bone;

            return null;
        }

        public Node ToNode()
        {
            var rootNode = new Node(bones[0]);

            return rootNode;
        }
    }

    public class Animation
    {
        public int assignedModel = -1;
        public float duration;
        public string name;

        public Dictionary<string, NodeChannel> nodeChannels = new();
        public int ticksPerSecond;

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
                    channel.Positions.Add(new Key<Vec3>((float)pkey.Time, new Vec3(pkey.Value)));

                foreach (var pkey in ch.RotationKeys)
                    channel.Rotations.Add(new Key<Vec4>((float)pkey.Time, new Vec4(pkey.Value)));

                foreach (var pkey in ch.ScalingKeys)
                    channel.Scales.Add(new Key<Vec3>((float)pkey.Time, new Vec3(pkey.Value)));

                nodeChannels.Add(channel.NodeName, channel);
            }
        }

        public void ApplySkeleton(Skeleton skeleton)
        {
            foreach (var (name, channel) in nodeChannels) channel.Bone = skeleton.GetBone(name);
        }


        public void Write(ModelWriter writer)
        {
            writer.WriteString("TANM");

            writer.WriteNonSigString(name);
            writer.Write(duration);
            writer.Write(ticksPerSecond);

            var channelCount = nodeChannels.Count;
            foreach (var keyValuePair in nodeChannels)
                if (keyValuePair.Value.Bone == null)
                    channelCount--;


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

        public class NodeChannel
        {
            private const float KeyToleration = 0.01f;
            public Bone Bone;
            public string NodeName;

            public List<Key<Vec3>> Positions = new();
            public List<Key<Vec4>> Rotations = new();
            public List<Key<Vec3>> Scales = new();

            public void AddPosition(Key<Vec3> p)
            {
                foreach (var position in Positions)
                    if (Math.Abs(position.timeStamp - p.timeStamp) < KeyToleration)
                    {
                        position.value += p.value;
                        return;
                    }

                Positions.Add(p);
            }

            public void AddRotation(Key<Vec4> r)
            {
                foreach (var rotation in Rotations)
                    if (Math.Abs(rotation.timeStamp - r.timeStamp) < KeyToleration)
                    {
                        rotation.value *= r.value;
                        return;
                    }

                Rotations.Add(r);
            }

            public void AddScale(Key<Vec3> s)
            {
                foreach (var scale in Scales)
                    if (Math.Abs(scale.timeStamp - s.timeStamp) < KeyToleration)
                    {
                        scale.value += s.value;
                        return;
                    }

                Scales.Add(s);
            }
        }
    }
}