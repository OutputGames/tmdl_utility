using System.Diagnostics;
using System.Numerics;
using Assimp;

namespace tmdl_utility;

public partial class ModelUtility
{
    public class Node
    {
        public List<Node> children = new();

        public int id = -1;
        public bool IsBone;

        public List<int> Meshes = new();
        public string name;
        public Node parent;
        public Vec3 Position = new();
        public Vec4 Rotation = new(0, 0, 0, 1);
        public Vec3 Scale = new(1);

        public Node(string name = "Node")
        {
            this.name = name;
        }

        public Node(Bone bone)
        {
            name = bone.name;
            IsBone = true;
            Position = bone.Position;
            Rotation = bone.Rotation;
            Scale = bone.Scale;

            foreach (var boneChild in bone.children)
            {
                var n = new Node(boneChild);
                n.SetParent(this);
            }
        }

        public static Matrix4x4 CalculateTransformMatrix(Node bone)
        {
            return Matrix4x4.CreateScale(bone.Scale) *
                   Matrix4x4.CreateFromQuaternion(bone.Rotation) *
                   Matrix4x4.CreateTranslation(bone.Scale);
        }

        public Assimp.Node ToAiNode()
        {
            var ai = new Assimp.Node(name);

            ai.Transform = CalculateTransformMatrix(this);
            ai.MeshIndices.AddRange(Meshes);

            foreach (var child in children) ai.Children.Add(child.ToAiNode());


            return ai;
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
            foreach (var idx in Meshes) writer.Write(idx);

            writer.Write(IsBone ? 0 : 1);

            writer.Write(children.Count);
            foreach (var child in children) child.Write(writer);
        }

        public Node GetChild(string name)
        {
            foreach (var child in children)
                if (child.name == name)
                    return child;

            return null;
        }

        public bool RemoveChild(string name)
        {
            var i = 0;
            var found = false;
            foreach (var child in children)
            {
                if (child.name == name)
                {
                    found = true;
                    break;
                }

                found = child.RemoveChild(name);
                if (found) return true;

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
            foreach (var child in children) c.AddRange(child.GetAllChildren());

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
        public List<Model> models = new();
        public string name;

        public Node rootNode;

        public void Write(ModelWriter writer)
        {
            writer.WriteString("TSCN");
            writer.WriteNonSigString(name);

            rootNode.Write(writer);

            writer.Write(models.Count);
            foreach (var model in models) model.Write(writer);
        }

        public void Export(string path, string format)
        {
            path += "." + format;

            var ctx = new AssimpContext();

            var ascn = new Assimp.Scene();

            ascn.Name = name;
            ascn.RootNode = rootNode.ToAiNode();

            foreach (var model in models)
            {
                foreach (var modelMesh in model.Meshes)
                {
                    var mesh = new Assimp.Mesh();
                    mesh.Name = modelMesh.Name;
                    mesh.MaterialIndex = 0;
                    for (var i = 0; i < modelMesh.Vertices.Length; i++)
                    {
                        mesh.Vertices.Add(modelMesh.Vertices[i]);
                        mesh.Normals.Add(modelMesh.Normals[i]);
                        //mesh.TextureCoordinateChannels[0].Add(modelMesh.UV0[i]);
                    }

                    var idxs = new List<int>();

                    foreach (var modelMeshIndex in modelMesh.Indices) idxs.Add(modelMeshIndex);

                    mesh.SetIndices(idxs.ToArray(), 3);

                    var bones = new List<Bone>();

                    foreach (var modelMeshBoneID in modelMesh.BoneIDs)
                    foreach (var i in modelMeshBoneID)
                    {
                        if (i == -1)
                            continue;

                        var bone = model.Skeleton.bones[i];

                        if (!bones.Contains(bone)) bones.Add(bone);
                    }

                    foreach (var skeleBone in bones)
                    {
                        var bone = new Assimp.Bone();

                        bone.Name = skeleBone.name;
                        bone.OffsetMatrix = skeleBone.offsetMatrix;

                        mesh.Bones.Add(bone);
                    }

                    for (var i = 0; i < modelMesh.BoneIDs.Length; i++)
                    {
                        var boneIds = modelMesh.BoneIDs[i];
                        var weights = modelMesh.VertexWeights[i];

                        for (var j = 0; j < weights.Length; j++)
                        {
                            var boneId = boneIds[j];
                            var weight = weights[j];

                            if (boneId == -1)
                                continue;

                            var realBone = model.Skeleton.bones[boneId];
                            var bone = mesh.Bones.Find(b => b.Name == realBone.name);

                            if (weight > 1) throw new Exception();

                            bone.VertexWeights.Add(new VertexWeight(i, weight));
                        }
                    }

                    ascn.Meshes.Add(mesh);
                }

                foreach (var modelAnimation in model.Animations)
                {
                    var animation = new Assimp.Animation();

                    animation.DurationInTicks = modelAnimation.duration;
                    animation.TicksPerSecond = modelAnimation.ticksPerSecond;
                    animation.Name = modelAnimation.name;

                    foreach (var (key, nodeChannel) in modelAnimation.nodeChannels)
                    {
                        var channel = new NodeAnimationChannel();
                        channel.NodeName = nodeChannel.NodeName;

                        var interpolation = AnimationInterpolation.CubicSpline;

                        foreach (var nodeChannelPosition in nodeChannel.Positions)
                            channel.PositionKeys.Add(new VectorKey(nodeChannelPosition.Time,
                                nodeChannelPosition.value, interpolation));

                        foreach (var nodeChannelRotation in nodeChannel.Rotations)
                            channel.RotationKeys.Add(new QuaternionKey(nodeChannelRotation.Time,
                                nodeChannelRotation.value, interpolation));

                        foreach (var nodeChannelScale in nodeChannel.Scales)
                            channel.PositionKeys.Add(new VectorKey(nodeChannelScale.Time, nodeChannelScale.value,
                                interpolation));


                        animation.NodeAnimationChannels.Add(channel);
                    }

                    ascn.Animations.Add(animation);
                }
            }


            {
                var mat = new Assimp.Material();

                mat.Name = "DefaultName";
                mat.ShadingMode = ShadingMode.CookTorrance;

                ascn.Materials.Add(mat);
            }

            /*
                foreach (var modelTexture in model.Textures)
                {
                    var texels = new List<Texel>();

                    for (var x = 0; x < modelTexture.width; x++)
                    for (var y = 0; y < modelTexture.height; y++)
                    {
                        var texel = new Texel();

                        for (var i = 0; i < modelTexture.channelCount; i++)
                        {
                            var data = modelTexture.data[
                                (x + y * modelTexture.width) * modelTexture.channelCount + i];
                            if (i == 0)
                                texel.R = data;
                            else if (i == 1)
                                texel.G = data;
                            else if (i == 2)
                                texel.B = data;
                            else if (i == 3)
                                texel.A = data;
                        }

                        texels.Add(texel);
                    }

                    var texture = new EmbeddedTexture(modelTexture.width, modelTexture.height, texels.ToArray(),
                        ConvertToFilePath(modelTexture.name));

                    ascn.Textures.Add(texture);
                }

                string ConvertToFilePath(string t)
                {
                    var p = t + ".png";

                    return p;
                }

                foreach (var modelMaterial in model.Materials)
                {
                    var material = new Assimp.Material();

                    material.Name = modelMaterial.name;

                    TextureSlot ConvertToTextureSlot(string p, string type)
                    {
                        var textureType = TextureType.None;
                        var fpath = ConvertToFilePath(p);

                        Dictionary<string, TextureType> samplerTypes = new()
                        {
                            { "_a0", TextureType.Diffuse },
                            { "_r0", TextureType.Roughness },
                            { "_op0", TextureType.Opacity },
                            { "a0", TextureType.Diffuse },
                            { "o0", TextureType.Opacity },
                            { "_o0", TextureType.Opacity },
                            { "_e0", TextureType.Emissive },
                            { "_n0", TextureType.Normals },
                            { "_ao0", TextureType.Ambient },
                            { "_m0", TextureType.Reflection },
                            { "n0", TextureType.Normals }
                            //{"sampler0", TextureType.Emissive},
                            //{"_fm0", TextureType.Emissive},
                        };

                        if (samplerTypes.ContainsKey(type)) textureType = samplerTypes[type];

                        Texture tex = null;
                        foreach (var modelTexture in model.Textures)
                            if (modelTexture.name == p)
                            {
                                tex = modelTexture;
                                break;
                            }

                        if (tex != null) tex.Export(Path.GetDirectoryName(path) + "\\" + fpath);

                        return new TextureSlot(fpath, textureType, 0, TextureMapping.FromUV, 0, 1, TextureOperation.Add,
                            TextureWrapMode.Clamp, TextureWrapMode.Clamp, 0);
                    }

                    foreach (var (key, value) in modelMaterial.Textures)
                        material.AddMaterialTexture(ConvertToTextureSlot(value, key));

                    ascn.Materials.Add(material);
                }
                */

            ctx.ExportFile(ascn, path, format);

            var startInfo = new ProcessStartInfo("\"C:\\Program Files\\Assimp\\bin\\x64\\assimp_viewer.exe\"");

            Process[] runningProcesses = Process.GetProcesses();
            foreach (var process in runningProcesses)
                if (process.ProcessName == Path.GetFileNameWithoutExtension(startInfo.FileName) &&
                    process.MainModule != null &&
                    string.Compare(process.MainModule.FileName, startInfo.FileName,
                        StringComparison.InvariantCultureIgnoreCase) == 0)
                    process.Kill();

            startInfo.ArgumentList.Add($"{path}");

            var proc = Process.Start(startInfo);
        }


        public bool RemoveNode(string name)
        {
            if (rootNode.name == name)
            {
                rootNode = new Node();
                return true;
            }

            return rootNode.RemoveChild(name);
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