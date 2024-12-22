using static tmdl_utility.ModelUtility;
using Assimp;
using StbiSharp;
using System.Diagnostics;
using System.Numerics;

namespace tmdl_utility;

public class AssimpImporter
{
    public static ModelUtility.Scene LoadAssimp(UtilityInitInfo info)
    {
        var importer = new AssimpContext();
        //importer.SetConfig();

        var maxBonesPerVertex = 4;

        var scene = importer.ImportFile(info.Source,
            PostProcessSteps.GenerateNormals | PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.FlipUVs |
            PostProcessSteps.Triangulate);

        Directory.CreateDirectory(info.Dest);

        var mscene = new ModelUtility.Scene();

        mscene.name = scene.Name;

        mscene.rootNode = new ModelUtility.Node();
        mscene.rootNode.name = Path.GetFileNameWithoutExtension(info.Source);

        mscene.rootNode.AddChild(ProcessAiNode(scene.RootNode));



        #region Assimp Models

        var boneDict = new Dictionary<string, Assimp.Bone>();

        var model = new Model();
        model.Meshes = new ModelUtility.Mesh[scene.MeshCount];
        model.Name = "TMDL_"+scene.Name;

        var modelNode = new ModelUtility.Node(model.Name);
        modelNode.SetParent(mscene.rootNode);

        foreach (var sceneMesh in scene.Meshes)
        {
            var mesh = new ModelUtility.Mesh();
            mesh.Vertices = new Vec3[sceneMesh.VertexCount];
            mesh.Normals = new Vec3[sceneMesh.VertexCount];
            mesh.UV0 = new Vec2[sceneMesh.VertexCount];
            mesh.VertexWeights = new float[sceneMesh.VertexCount][];
            mesh.BoneIDs = new int[sceneMesh.VertexCount][];

            mesh.Name = sceneMesh.Name;

            mesh.Indices = new UInt16[sceneMesh.FaceCount * 3];

            var boneIndices = new List<int>[sceneMesh.VertexCount];
            var boneWeights = new List<float>[sceneMesh.VertexCount];

            for (int i = 0; i < sceneMesh.VertexCount; i++)
            {
                boneIndices[i] = new List<int>();
                boneWeights[i] = new List<float>();
            }


            foreach (var bone in sceneMesh.Bones)
            {
                int boneId = sceneMesh.Bones.IndexOf(bone);
                foreach (var (vertexId, weight) in bone.VertexWeights)
                {
                    if (boneIndices[vertexId].Count >= 4)
                        continue;

                    boneIndices[vertexId].Add(boneId);
                    boneWeights[vertexId].Add(weight);
                }
            }

            for (var i = 0; i < boneWeights.Length; i++)
            {
                var weight = boneWeights[i];
                float totalWeight = weight.Sum();
                for (int j = 0; j < weight.Count; j++)
                {
                    boneWeights[i][j] /= totalWeight;
                }
            }

            for (var i = 0; i < sceneMesh.Vertices.Count; i++)
            {
                var vtx = sceneMesh.Vertices[i];
                var nrm = sceneMesh.Normals[i];
                var uv0 = sceneMesh.TextureCoordinateChannels[0][i];

                mesh.Vertices[i] = new Vec3(vtx.X, vtx.Y, vtx.Z);
                mesh.Normals[i] = new Vec3(nrm.X, nrm.Y, nrm.Z);
                mesh.UV0[i] = new Vec2(uv0.X, uv0.Y);
                mesh.BoneIDs[i] = boneIndices[i].ToArray();
                mesh.VertexWeights[i] = boneWeights[i].ToArray();
            }

            int iidx = 0;
            foreach (var sceneMeshFace in sceneMesh.Faces)
            {
                foreach (var index in sceneMeshFace.Indices)
                {
                    mesh.Indices[iidx] = (ushort)index;

                    iidx++;
                }
            }

            foreach (var bone in sceneMesh.Bones)
            {
                if (!boneDict.ContainsKey(bone.Name))
                {
                    boneDict.Add(bone.Name, bone);
                }
            }

            mesh.MaterialIndex = sceneMesh.MaterialIndex;

            model.Meshes[scene.Meshes.IndexOf(sceneMesh)] = mesh;

            var meshNode = new ModelUtility.Node(mesh.Name);
            meshNode.Meshes.Add(scene.Meshes.IndexOf(sceneMesh));
            meshNode.SetParent(modelNode);
        }

        model.Skeleton = new Skeleton(mscene.GetNode(boneDict.Keys.ToList()[0]));

        foreach (var skeletonBone in model.Skeleton.bones)
        {
            mscene.RemoveNode(skeletonBone.name);
        }
        mscene.RemoveNode("Armature");

        var snode = model.Skeleton.ToNode();

        var armatureNode = modelNode.AddNode("Armature");
        armatureNode.IsBone = true;
        armatureNode.AddNode(snode);

        model.Textures = new Texture[scene.TextureCount];

        for (var i = 0; i < scene.Textures.Count; i++)
        {
            var tex = scene.Textures[i];
            var texture = new Texture();
            texture.width = tex.Width;
            texture.height = tex.Height;
            texture.name = tex.Filename;

            if (tex.IsCompressed)
            {
                using (var memoryStream = new MemoryStream())
                {
                    memoryStream.Write(tex.CompressedData);
                    StbiImage image = Stbi.LoadFromMemory(memoryStream, 4);

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
                List<byte> dat = new List<byte>();

                for (int x = 0; x < texture.width; x++)
                {
                    for (int y = 0; y < texture.height; y++)
                    {
                        var texel = tex.NonCompressedData[x * texture.width + y];
                        dat.AddRange(new List<byte> { texel.R, texel.G, texel.B, texel.A }.AsReadOnly());
                    }
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
                {
                    material.Textures.Add(allMaterialTexture.TextureType.ToString(),
                        model.Textures[allMaterialTexture.TextureIndex].name);
                }
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

    public static ModelUtility.Node ProcessAiNode(Assimp.Node ai, int count = -1, bool isBone = false)
    {

        var node = new ModelUtility.Node();

        node.name = ai.Name;
        node.id = count++;

        node.Meshes = ai.MeshIndices;
        
        ai.Transform.DecomposeMatrix(out var translation, out var rotation , out var scale );

        node.Position = new Vec3(translation);
        node.Rotation = new Vec4(rotation);
        node.Scale = new Vec3(scale);

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

}