using System.Diagnostics;
using Aspose.ThreeD.Entities;
using Aspose.ThreeD.Shading;
using Matrix4x4 = System.Numerics.Matrix4x4;

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

            // Check if any models have animations - if so, use Assimp for complete export
            bool hasAnimations = models.Any(m => m.Animations != null && m.Animations.Length > 0);

            if (hasAnimations)
            {
                // Export everything (meshes, materials, and animations) using Assimp
                ExportCompleteSceneWithAssimp(path, format);
                
                // Still need to start viewer for compatibility with existing workflow
                StartViewer(path);
            }
            else
            {
                // No animations - use original Aspose export
                ExportWithAspose(path);
                StartViewer(path);
            }
        }

        private void ExportWithAspose(string path)
        {
            var ascn = new Aspose.ThreeD.Scene();

            foreach (var model in models)
            {
                var materials = new List<Aspose.ThreeD.Shading.Material>();
                foreach (var modelMaterial in model.Materials)
                {
                    var material = new PbrMaterial();

                    material.Name = modelMaterial.name;
                    foreach (var modelMaterialTexture in modelMaterial.Textures)
                    {
                        var realTex = "";

                        switch (modelMaterialTexture.Key)
                        {
                            case "_a0":
                                realTex = Aspose.ThreeD.Shading.Material.MapDiffuse;
                                break;
                            default:
                                continue;
                        }


                        var texture = new Aspose.ThreeD.Shading.Texture();
                        texture.Name = modelMaterialTexture.Value;

                        texture.WrapModeU = WrapMode.Mirror;
                        texture.WrapModeV = WrapMode.Mirror;
                        var tex = model.Textures.First(x => x.name == modelMaterialTexture.Value);

                        texture.Content = tex.Export();

                        material.SetTexture(realTex, texture);
                    }

                    //material.SpecularColor = new Vector3(102) / 255;
                    //material.DiffuseColor = new Vector3(255, 0, 0);
                    //material.AmbientColor = new Vector3(5) / 255;

                    materials.Add(material);
                }


                foreach (var modelMesh in model.Meshes)
                {
                    var mesh = new Aspose.ThreeD.Entities.Mesh();
                    mesh.Name = modelMesh.Name;
                    //mesh.MaterialIndex = 0;
                    var vertices = mesh.ControlPoints;

                    var normals =
                        mesh.CreateElement(VertexElementType.Normal, MappingMode.ControlPoint, ReferenceMode.Direct) as
                            VertexElementNormal;
                    var uv =
                        mesh.CreateElement(VertexElementType.UV, MappingMode.ControlPoint, ReferenceMode.Direct) as
                            VertexElementUV;

                    for (var i = 0; i < modelMesh.Vertices.Length; i++)
                    {
                        vertices.Add(modelMesh.Vertices[i]);
                        normals.Data.Add(modelMesh.Normals[i]);

                        var u = modelMesh.UV0[i];

                        u.Y = 1.0f - u.Y;

                        uv.Data.Add(u);
                    }

                    for (var i = 0; i < modelMesh.Indices.Length; i += 3)
                    {
                        var i1 = modelMesh.Indices[i + 0];
                        var i2 = modelMesh.Indices[i + 1];
                        var i3 = modelMesh.Indices[i + 2];

                        mesh.CreatePolygon(i1, i2, i3);
                    }

                    var node = new Aspose.ThreeD.Node();
                    node.Name = mesh.Name;
                    node.Entity = mesh;
                    node.Material = materials[modelMesh.MaterialIndex];

                    ascn.RootNode.AddChildNode(node);
                }
            }


            ascn.Save(path);
        }

        private void StartViewer(string path)
        {
            var assimpViewer = "C:/Program Files/Assimp/bin/x64/assimp_viewer.exe";
            var fbxViewer = "D:/Downloads/fbxreview.exe";

            var startInfo = new ProcessStartInfo(assimpViewer);

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

        /// <summary>
        /// Exports the complete scene including meshes, materials, node hierarchy, and animations using AssimpNet.
        /// This method is used when animations are present to ensure they are properly exported.
        /// </summary>
        /// <param name="path">The output file path (including extension)</param>
        /// <param name="format">The output format (e.g., "glb", "fbx", "dae")</param>
        private void ExportCompleteSceneWithAssimp(string path, string format)
        {
            // Create a new Assimp scene for export with complete model data
            var aiScene = new Assimp.Scene();

            // Convert root node to Assimp format
            aiScene.RootNode = rootNode.ToAiNode();

            // Export all models
            foreach (var model in models)
            {
                // Add meshes to the scene
                if (model.Meshes != null)
                {
                    foreach (var modelMesh in model.Meshes)
                    {
                        if (modelMesh.Vertices == null || modelMesh.Normals == null || 
                            modelMesh.UV0 == null || modelMesh.Indices == null)
                            continue;

                        var aiMesh = new Assimp.Mesh();
                        aiMesh.Name = modelMesh.Name;

                        // Add vertices
                        for (var i = 0; i < modelMesh.Vertices.Length; i++)
                        {
                            aiMesh.Vertices.Add(new System.Numerics.Vector3(
                                modelMesh.Vertices[i].X, 
                                modelMesh.Vertices[i].Y, 
                                modelMesh.Vertices[i].Z));
                            
                            if (i < modelMesh.Normals.Length)
                            {
                                aiMesh.Normals.Add(new System.Numerics.Vector3(
                                    modelMesh.Normals[i].X, 
                                    modelMesh.Normals[i].Y, 
                                    modelMesh.Normals[i].Z));
                            }
                            
                            if (i < modelMesh.UV0.Length)
                            {
                                aiMesh.TextureCoordinateChannels[0].Add(new System.Numerics.Vector3(
                                    modelMesh.UV0[i].X, 
                                    1.0f - modelMesh.UV0[i].Y,  // Flip Y coordinate
                                    0));
                            }
                        }

                        // Add faces - validate indices array length
                        if (modelMesh.Indices.Length % 3 == 0)
                        {
                            for (var i = 0; i < modelMesh.Indices.Length; i += 3)
                            {
                                var face = new Assimp.Face();
                                face.Indices.Add(modelMesh.Indices[i + 0]);
                                face.Indices.Add(modelMesh.Indices[i + 1]);
                                face.Indices.Add(modelMesh.Indices[i + 2]);
                                aiMesh.Faces.Add(face);
                            }
                        }

                        // Validate and set material index
                        if (model.Materials != null && modelMesh.MaterialIndex >= 0 && 
                            modelMesh.MaterialIndex < model.Materials.Length)
                        {
                            aiMesh.MaterialIndex = modelMesh.MaterialIndex;
                        }
                        
                        aiScene.Meshes.Add(aiMesh);
                    }
                }

                // Add materials
                if (model.Materials != null)
                {
                    foreach (var modelMaterial in model.Materials)
                    {
                        var aiMaterial = new Assimp.Material();
                        aiMaterial.Name = modelMaterial.name;
                        aiScene.Materials.Add(aiMaterial);
                    }
                }

                // Convert animations to Assimp format
                if (model.Animations != null)
                {
                    foreach (var modelAnimation in model.Animations)
                    {
                        var aiAnimation = new Assimp.Animation();
                        aiAnimation.Name = modelAnimation.name;
                        aiAnimation.DurationInTicks = modelAnimation.duration;
                        aiAnimation.TicksPerSecond = modelAnimation.ticksPerSecond;

                        // Add node animation channels
                        foreach (var (nodeName, channel) in modelAnimation.nodeChannels)
                        {
                            var aiChannel = new Assimp.NodeAnimationChannel();
                            aiChannel.NodeName = nodeName;

                            // Add position keys
                            foreach (var posKey in channel.Positions)
                            {
                                aiChannel.PositionKeys.Add(new Assimp.VectorKey(
                                    posKey.timeStamp,
                                    new System.Numerics.Vector3(posKey.value.X, posKey.value.Y, posKey.value.Z)));
                            }

                            // Add rotation keys
                            foreach (var rotKey in channel.Rotations)
                            {
                                aiChannel.RotationKeys.Add(new Assimp.QuaternionKey(
                                    rotKey.timeStamp,
                                    new System.Numerics.Quaternion(rotKey.value.X, rotKey.value.Y, rotKey.value.Z, rotKey.value.W)));
                            }

                            // Add scale keys
                            foreach (var scaleKey in channel.Scales)
                            {
                                aiChannel.ScalingKeys.Add(new Assimp.VectorKey(
                                    scaleKey.timeStamp,
                                    new System.Numerics.Vector3(scaleKey.value.X, scaleKey.value.Y, scaleKey.value.Z)));
                            }

                            aiAnimation.NodeAnimationChannels.Add(aiChannel);
                        }

                        aiScene.Animations.Add(aiAnimation);
                    }
                }
            }

            // Export using Assimp exporter
            var exporter = new Assimp.AssimpContext();
            try
            {
                exporter.ExportFile(aiScene, path, format);
                var totalAnimations = models.Sum(m => m.Animations?.Length ?? 0);
                Console.WriteLine($"Successfully exported scene with {totalAnimations} animation(s) to {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to export scene with animations: {ex.Message}");
                throw; // Re-throw to let caller handle the error
            }
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