using Assimp;
using StbiSharp;

namespace tmdl_utility;

public class AssimpImporter
{
    public static ModelUtility.Scene LoadAssimp(UtilityInitInfo info)
    {
        var importer = new AssimpContext();
        //importer.SetConfig();


        var maxBonesPerVertex = 4;

        var scene = importer.ImportFile(info.Source,
            PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.FlipUVs |
            PostProcessSteps.Triangulate);

        Directory.CreateDirectory(info.Dest);

        //importer.ExportFile(scene, info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".fbx", "fbx");

        var mscene = new ModelUtility.Scene();

        mscene.name = scene.Name;

        mscene.rootNode = new ModelUtility.Node();
        mscene.rootNode.name = Path.GetFileNameWithoutExtension(info.Source);

        mscene.rootNode.AddChild(ProcessAiNode(scene.RootNode));


        #region Assimp Models

        var boneDict = new Dictionary<string, BoneInfo>();

        var model = new ModelUtility.Model();
        model.Meshes = new ModelUtility.Mesh[scene.MeshCount];
        model.Name = "TMDL_" + scene.Name;

        var modelNode = new ModelUtility.Node(model.Name);
        modelNode.SetParent(mscene.rootNode);

        foreach (var sceneMesh in scene.Meshes)
        {
            var mesh = new ModelUtility.Mesh();
            mesh.Vertices = new ModelUtility.Vec3[sceneMesh.VertexCount];
            mesh.Normals = new ModelUtility.Vec3[sceneMesh.VertexCount];
            mesh.UV0 = new ModelUtility.Vec2[sceneMesh.VertexCount];
            mesh.VertexWeights = new float[sceneMesh.VertexCount][];
            mesh.BoneIDs = new int[sceneMesh.VertexCount][];

            mesh.Name = sceneMesh.Name;

            mesh.Indices = new ushort[sceneMesh.FaceCount * 3];

            var boneIndices = new List<int>[sceneMesh.VertexCount];
            var boneWeights = new List<float>[sceneMesh.VertexCount];

            for (var i = 0; i < sceneMesh.VertexCount; i++)
            {
                boneIndices[i] = new List<int> { -1, -1, -1, -1 };
                boneWeights[i] = new List<float> { 0, 0, 0, 0 };
            }


            foreach (var bone in sceneMesh.Bones)
            {
                var boneName = bone.Name;

                var boneId = -1;

                if (!boneDict.ContainsKey(boneName))
                {
                    var boneInfo = new BoneInfo();

                    boneInfo.id = boneDict.Count;

                    boneDict.Add(boneName, boneInfo);

                    boneId = boneInfo.id;
                }
                else
                {
                    boneId = boneDict[boneName].id;
                }


                foreach (var (vertexId, weight) in bone.VertexWeights)
                {
                    if (boneIndices[vertexId].Count > 4)
                        continue;

                    if (vertexId == 40)
                        Console.Write("");

                    if (weight <= 0.01)
                        continue;
                    for (var i = 0; i < 4; ++i)
                        if (boneIndices[vertexId][i] < 0)
                        {
                            boneIndices[vertexId][i] = boneId;
                            boneWeights[vertexId][i] = weight;
                            break;
                        }
                }
            }

            /*
            for (var i = 0; i < boneWeights.Length; i++)
            {
                var weight = boneWeights[i];
                float totalWeight = weight.Sum();
                for (int j = 0; j < weight.Count; j++)
                {
                    boneWeights[i][j] /= totalWeight;
                }
            }
            */

            for (var i = 0; i < sceneMesh.Vertices.Count; i++)
            {
                var vtx = sceneMesh.Vertices[i];
                var nrm = sceneMesh.Normals[i];
                var uv0 = sceneMesh.TextureCoordinateChannels[0][i];

                mesh.Vertices[i] = new ModelUtility.Vec3(vtx.X, vtx.Y, vtx.Z);
                mesh.Normals[i] = new ModelUtility.Vec3(nrm.X, nrm.Y, nrm.Z);
                mesh.UV0[i] = new ModelUtility.Vec2(uv0.X, uv0.Y);
                mesh.BoneIDs[i] = boneIndices[i].ToArray();
                mesh.VertexWeights[i] = boneWeights[i].ToArray();
            }

            var iidx = 0;
            foreach (var sceneMeshFace in sceneMesh.Faces)
            foreach (var index in sceneMeshFace.Indices)
            {
                mesh.Indices[iidx] = (ushort)index;

                iidx++;
            }

            mesh.MaterialIndex = sceneMesh.MaterialIndex;

            model.Meshes[scene.Meshes.IndexOf(sceneMesh)] = mesh;

            var meshNode = new ModelUtility.Node(mesh.Name);
            meshNode.Meshes.Add(scene.Meshes.IndexOf(sceneMesh));
            meshNode.SetParent(modelNode);
        }

        model.Skeleton = new ModelUtility.Skeleton(mscene.GetNode(boneDict.Keys.ToList()[0]));
        foreach (var sceneMesh in scene.Meshes)
        foreach (var bone in sceneMesh.Bones)
        {
            var b = model.Skeleton.GetBone(bone.Name);
            if (b != null) b.offsetMatrix = bone.OffsetMatrix;
        }

        foreach (var skeletonBone in model.Skeleton.bones) mscene.RemoveNode(skeletonBone.name);

        var armatureNode = mscene.GetNode("Armature");
        armatureNode.IsBone = true;

        var snode = model.Skeleton.ToNode();

        armatureNode.AddChild(snode);
        armatureNode.SetParent(modelNode);

        model.Textures = new ModelUtility.Texture[scene.TextureCount];

        for (var i = 0; i < scene.Textures.Count; i++)
        {
            var tex = scene.Textures[i];
            var texture = new ModelUtility.Texture();
            texture.width = tex.Width;
            texture.height = tex.Height;
            //texture.name = tex.Filename;

            if (tex.IsCompressed)
            {
                using (var memoryStream = new MemoryStream())
                {
                    memoryStream.Write(tex.CompressedData);
                    var image = Stbi.LoadFromMemory(memoryStream, 4);

                    texture.width = image.Width;
                    texture.height = image.Height;
                    texture.data = image.Data.ToArray();
                    texture.channelCount = image.NumChannels;

                    // Use image.Width, image.Height,
                    // image.NumChannels, and image.Data.
                }
            }
            else
            {
                texture.channelCount = 4;
                var dat = new List<byte>();

                for (var x = 0; x < texture.width; x++)
                for (var y = 0; y < texture.height; y++)
                {
                    var texel = tex.NonCompressedData[x * texture.width + y];
                    dat.AddRange(new List<byte> { texel.R, texel.G, texel.B, texel.A }.AsReadOnly());
                }

                texture.data = dat.ToArray();
            }

            model.Textures[i] = texture;
        }

        model.Materials = new ModelUtility.Material[scene.MaterialCount];

        foreach (var sceneMaterial in scene.Materials)
        {
            var material = new ModelUtility.Material();

            if (model.Textures.Length > 0)
            {
                var texs = sceneMaterial.GetAllMaterialTextures();

                foreach (var allMaterialTexture in texs)
                    material.Textures.Add(allMaterialTexture.TextureType.ToString(),
                        model.Textures[allMaterialTexture.TextureIndex].name);
            }

            material.name = sceneMaterial.Name;

            model.Materials[scene.Materials.IndexOf(sceneMaterial)] = material;
        }

        model.Animations = new ModelUtility.Animation[scene.AnimationCount];

        foreach (var anim in scene.Animations)
        {
            var animation = new ModelUtility.Animation(anim);

            animation.ApplySkeleton(model.Skeleton);

            model.Animations[scene.Animations.IndexOf(anim)] = animation;
        }

        mscene.models.Add(model);

        #endregion

        return mscene;
    }

    public static ModelUtility.Node ProcessAiNode(Node ai, int count = -1, bool isBone = false)
    {
        var node = new ModelUtility.Node();

        node.name = ai.Name;
        node.id = count++;

        node.Meshes = ai.MeshIndices;

        ai.Transform.AI_DecomposeMatrix(out var translation, out var rotation, out var scale);

        node.Position = new ModelUtility.Vec3(translation);
        node.Rotation = new ModelUtility.Vec4(rotation);
        node.Scale = new ModelUtility.Vec3(scale);

        foreach (var child in ai.Children)
        {
            var ib = isBone;
            if (ai.Name == "Armature")
                ib = true;

            var n = ProcessAiNode(child, count, ib);

            n.SetParent(node);
        }

        return node;
    }

    public class BoneInfo
    {
        public int id;
    }
}