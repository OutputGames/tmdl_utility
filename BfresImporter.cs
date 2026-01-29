using BfresLibrary;
using BfresLibrary.GX2;
using BfresLibrary.Helpers;
using BfresLibrary.Switch;
using CsYaz0;
using SarcLibrary;
using Syroot.BinaryData;
using ZstdSharp;
using static tmdl_utility.ModelUtility;
using Bone = BfresLibrary.Bone;
using Model = BfresLibrary.Model;

namespace tmdl_utility;

public class BfresImporter
{
    public enum TrackType
    {
        ScaleX = 0x4,
        ScaleY = 0x8,
        ScaleZ = 0xC,
        PositionX = 0x10,
        PositionY = 0x14,
        PositionZ = 0x18,
        RotationX = 0x20,
        RotationY = 0x24,
        RotationZ = 0x28,
        RotationW = 0x2C
    }

    public static Scene LoadBfres(UtilityInitInfo info)
    {
        var stream = new MemoryStream();
        if (info.Source.EndsWith(".zs"))
        {
            var src = File.ReadAllBytes(info.Source);
            using var decompressor = new Decompressor();
            var decompressed = decompressor.Unwrap(src).ToArray();
            stream.Write(decompressed);
        }
        else if (info.Source.EndsWith(".szs"))
        {
            var src = File.ReadAllBytes(info.Source);

            var decomp = Yaz0.Decompress(src);

            var sarc = Sarc.FromBinary(decomp);

            foreach (var (path, data) in sarc)
                if (path.EndsWith(".bfres"))
                    stream.Write(data);
                else if (path.EndsWith(".kcl")) Console.WriteLine("Found collision files!");
        }
        else
        {
            stream.Write(File.ReadAllBytes(info.Source));
        }

        var resFile = new ResFile(stream);
        var scene = new Scene();

        scene.name = resFile.Name;
        scene.rootNode = new Node("BfresRoot");

        var nct = -1;
        var mshCt = -1;

        var textures = new List<Texture>();
        ExtractTextures(resFile, textures);

        #region Bfres Model Loading

        foreach (var (key, resfileModel) in resFile.Models)
        {
            ModelUtility.Model mdl;
            Node arm;
            (nct, mdl, arm) = ExtractModel(resfileModel, nct, resFile, textures, scene, ref mshCt);

            var node = new Node("TMDL_" + key);
            mdl.Name = node.name;

            for (var i = 0; i < mdl.Meshes.Length; i++)
            {
                var mdlMesh = mdl.Meshes[i];

                var meshNode = new Node(mdlMesh.Name);
                meshNode.Meshes.Add(i);
                meshNode.SetParent(node);
            }

            arm.SetParent(node);

            node.SetParent(scene.rootNode);
        }

        #endregion

        List<Animation> animations = new();
        foreach (var (name, anim) in resFile.SkeletalAnims)
        {
            var animation = new Animation();

            animation.name = name;
            animation.ticksPerSecond = 30;
            animation.duration = anim.FrameCount;

            var jk = 0;
            foreach (var resFileModel in resFile.Models)
            {
                if (resFileModel.Value.Skeleton == anim.BindSkeleton)
                {
                    animation.assignedModel = jk;
                    break;
                }

                jk++;
            }

            if (animation.assignedModel == -1) animation.assignedModel = 0;

            foreach (var boneAnim in anim.BoneAnims)
            {
                ExtractAnimation(boneAnim, animation, out var channel);

                if (boneAnim.ApplyScaleOne)
                {
                    channel.Scales.Clear();
                    channel.Scales.Add(new Key<Vec3>(0, new Vec3(1)));
                }

                if (boneAnim.ApplyTranslateZero)
                {
                    channel.Positions.Clear();
                    channel.Positions.Add(new Key<Vec3>(0, new Vec3(0)));
                }

                if (boneAnim.ApplyRotateZero)
                {
                    channel.Rotations.Clear();
                    channel.Rotations.Add(new Key<Vec4>(0, new Vec4()));
                }

                if (boneAnim.ApplyIdentity)
                {
                    channel.Rotations.Clear();
                    channel.Rotations.Add(new Key<Vec4>(0, new Vec4()));
                    channel.Positions.Clear();
                    channel.Positions.Add(new Key<Vec3>(0, new Vec3(0)));
                    channel.Scales.Clear();
                    channel.Scales.Add(new Key<Vec3>(0, new Vec3(1)));
                }
                

                foreach (var sceneModel in scene.models)
                {
                    var bone = sceneModel.Skeleton.GetBone(boneAnim.Name);
                    if (bone != null)
                    {
                        channel.Bone = bone;
                        break;
                    }
                }


                if (channel.Positions.Count > 0 || channel.Rotations.Count > 0 || channel.Scales.Count > 0)
                {
                    if (anim.FlagsRotate == SkeletalAnimFlagsRotate.EulerXYZ)
                        foreach (var channelRotation in channel.Rotations)
                        {
                            var euler = channelRotation.value.ToEuler();

                            var val = Vec4.FromEuler(euler);
                            channelRotation.value = val;
                        }

                    animation.nodeChannels.Add(channel.NodeName, channel);
                }
                else
                {
                    Console.WriteLine($"Channel: {channel.NodeName} of {anim.Name} is invalid.");
                }
            }

            animations.Add(animation);
        }

        foreach (var sceneModel in scene.models)
        {
            List<Animation> anims = new();

            var idx = scene.models.IndexOf(sceneModel);
            foreach (var animation in animations)
                if (animation.assignedModel == idx)
                    anims.Add(animation);

            sceneModel.Animations = anims.ToArray();
        }

        return scene;
    }

    private static (int, ModelUtility.Model, Node) ExtractModel(Model resfileModel, int nct, ResFile resFile,
        List<Texture> textures, Scene scene,
        ref int mshCt)
    {
        var vct = 0;
        var model = new ModelUtility.Model();

        model.Name = resfileModel.Name;

        var matList = new List<ModelUtility.Material>();
        var mlist = new List<ModelUtility.Mesh>();

        var rootNode = new Node(resfileModel.Name);
        rootNode.id = nct++;

        var armatureNode = new Node("Armature");

        var boneDict = new Dictionary<Bone, Node>();
        foreach (var bone in resfileModel.Skeleton.BoneList)
        {
            var bNode = new Node(bone.Name);

            bNode.Position = new Vec3(bone.Position.X, bone.Position.Y, bone.Position.Z);
            if (bone.FlagsRotation != BoneFlagsRotation.EulerXYZ)
            {
                bNode.Rotation = new Vec4(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
            }
            else
            {
                var eul = new Vec4(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
                bNode.Rotation = Vec4.FromEuler(eul.ToEuler());
            }

            //bNode.Rotation = new Vec4(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);

            bNode.Scale = new Vec3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z);

            boneDict.Add(bone, bNode);
        }

        foreach (var (bone, node) in boneDict)
            if (bone.ParentIndex == -1)
                node.SetParent(armatureNode);
            else
                node.SetParent(boneDict.Values.ToList()[bone.ParentIndex]);


        foreach (var (s, shape) in resfileModel.Shapes)
        {
            var vertexBuffer = resfileModel.VertexBuffers[shape.VertexBufferIndex];

            var pos = ReadVertexBuffer(vertexBuffer, "_p0", resFile.ByteOrder);
            var nrm = ReadVertexBuffer(vertexBuffer, "_n0", resFile.ByteOrder);
            var uv0 = ReadVertexBuffer(vertexBuffer, "_u0", resFile.ByteOrder);
            var id0 = ReadVertexBuffer(vertexBuffer, "_i0", resFile.ByteOrder);
            var w0 = ReadVertexBuffer(vertexBuffer, "_w0", resFile.ByteOrder);

            var shapeMesh = shape.Meshes[0];

            var mesh = new ModelUtility.Mesh();
            mesh.Name = shape.Name;

            mesh.Vertices = pos.ToVec3Array();
            mesh.Normals = nrm.ToVec3Array();
            mesh.UV0 = uv0.ToVec2Array();

            List<int[]> bids = new();
            foreach (var vec4 in id0)
            {
                int[] bid = { -1, -1, -1, -1 };

                for (var i = 0; i < shape.VertexSkinCount; i++)
                {
                    bid[i] = (int)vec4[i];
                }

                bids.Add(bid);
            }


            if (shape.SkinBoneIndices.Count > 1)
                for (var j = 0; j < bids.Count; j++)
                {
                    var b = bids[j];
                    for (var i = 0; i < shape.VertexSkinCount; i++)
                    {
                        var k = b[i];
                        if (k < 0) continue;
                        
                        // Convert smooth matrix index to bone index
                        int boneIndex = resfileModel.Skeleton.MatrixToBoneList[k];

                        // If weight is zero, don't use this bone
                        if (w0[j][i] <= float.Epsilon) 
                            boneIndex = -1;

                        b[i] = boneIndex;
                    }

                    // Clear unused bone slots
                    for (int i = shape.VertexSkinCount; i < 4; i++) 
                        b[i] = -1;

                    bids[j] = b;
                }
            else
                for (var j = 0; j < bids.Count; j++)
                {
                    var b = bids[j];
                    // For rigid skinning, use the first bone in SkinBoneIndices
                    if (shape.SkinBoneIndices.Count > 0)
                        b[0] = shape.SkinBoneIndices[0];
                    else
                        b[0] = 0;
                    
                    for (var i = 1; i < b.Length; i++) 
                        b[i] = -1;

                    bids[j] = b;
                }


            mesh.BoneIDs = bids.ToArray();

            if (w0.Length == 0)
            {
                w0 = new Vec4[mesh.BoneIDs.Length];
                for (var i = 0; i < mesh.BoneIDs.Length; i++)
                {
                    var w = new Vec4(1, 0, 0, 0);
                    w0[i] = w;
                }
            }

            List<float[]> wghts = new();
            foreach (var vec4 in w0)
            {
                // Clear unused weights
                for (int i = shape.VertexSkinCount; i < 4; i++) 
                    vec4[i] = 0;

                // Normalize weights to sum to 1.0
                float totalWeight = 0;
                for (int i = 0; i < 4; i++)
                    totalWeight += vec4[i];
                
                if (totalWeight > 0.0001f)
                {
                    for (int i = 0; i < 4; i++)
                        vec4[i] /= totalWeight;
                }
                else
                {
                    // If no weights, assign 1.0 to first bone
                    vec4[0] = 1.0f;
                    for (int i = 1; i < 4; i++)
                        vec4[i] = 0;
                }

                wghts.Add(vec4.ToFltArray());
            }

            mesh.VertexWeights = wghts.ToArray();

            if (id0.Length == 0)
            {
                mesh.BoneIDs = new int[mesh.Vertices.Length][];
                mesh.VertexWeights = new float[mesh.Vertices.Length][];
                for (var i = 0; i < mesh.BoneIDs.Length; i++)
                {
                    mesh.BoneIDs[i] = new[] { 0, -1, -1, -1 };
                    mesh.VertexWeights[i] = new[] { 1.0f, 0, 0, 0 };
                }
            }

            mesh.Indices = new ushort[shapeMesh.IndexCount];

            var idxs = GetDisplayFaces(shapeMesh.GetIndices().ToList());
            for (var i = 0; i < shapeMesh.IndexCount; i++) mesh.Indices[i] = (ushort)(idxs[i] + shapeMesh.FirstVertex);

            vct += mesh.Vertices.Length;

            if (idxs.Count > 1938)
            {
                var idx = idxs[1938];
                Console.WriteLine(
                    $"1938 ({idx}): ({mesh.BoneIDs[(int)idx][0]}, {mesh.BoneIDs[(int)idx][1]}, {mesh.BoneIDs[(int)idx][2]}, {mesh.BoneIDs[(int)idx][3]})");
            }

            mesh.MaterialIndex = shape.MaterialIndex;
            mshCt++;

            mlist.Add(mesh);
        }

        for (var j = 0; j < resfileModel.Materials.Count; j++)
        {
            var mat = resfileModel.Materials[j];
            var material = new ModelUtility.Material();

            material.name = mat.Name;
            foreach (var (s, sampler) in mat.Samplers)
                material.Textures.Add(s, mat.TextureRefs[mat.Samplers.IndexOf(sampler)].Name);


            matList.Add(material);
        }

        if (vct != resfileModel.TotalVertexCount)
            throw new Exception($"Vertex counts do not match. {vct}, {resfileModel.TotalVertexCount}");

        model.Meshes = mlist.ToArray();
        model.Textures = textures.ToArray();
        model.Skeleton = new ModelUtility.Skeleton(resfileModel.Skeleton);

        foreach (var skeletonBone in model.Skeleton.bones)
        {
            var idx = model.Skeleton.bones.IndexOf(skeletonBone);
            var bone = resfileModel.Skeleton.Bones[idx];

            if (resfileModel.Skeleton.NumSmoothMatrices > 0 && bone.SmoothMatrixIndex > -1)
            {
                var mat = resfileModel.Skeleton.InverseModelMatrices[bone.SmoothMatrixIndex];


                //skeletonBone.offsetMatrix = resfileModel.Skeleton.InverseModelMatrices

                //skeletonBone.offsetMatrix.DecomposeMatrix(out var translation, out var rotation, out var scale);

                //Matrix4x4.Invert(skeletonBone.offsetMatrix, out skeletonBone.offsetMatrix);

                //skeletonBone.offsetMatrix = Matrix4x4.Transpose(skeletonBone.offsetMatrix);


                //Matrix4x4.Invert(skeletonBone.offsetMatrix, out skeletonBone.offsetMatrix);
            }

            var m = MatrixExtensions.CalculateInverseMatrix(bone, resfileModel.Skeleton);
            //Matrix4x4.Invert(m, out m);
            //m.inverse = Matrix4x4.Transpose(m.inverse);

            //skeletonBone.offsetMatrix = m;

            skeletonBone.offsetMatrix = m.inverse;


            //skeletonBone.offsetMatrix = ModelUtility.Bone.CalculateOffsetMatrix(skeletonBone).Item2;
        }


        model.Materials = matList.ToArray();

        scene.models.Add(model);
        return (nct, model, armatureNode);
    }

    private static void ExtractTextures(ResFile resFile, List<Texture> textures)
    {
        if (resFile.IsPlatformSwitch)
        {
            var ind = 0;
            foreach (SwitchTexture texture in resFile.Textures.Values)
            {
                var tex = new Texture();
                tex.width = (int)texture.Width;
                tex.height = (int)texture.Height;

                var cc = 4;

                tex.channelCount = cc;

                tex.name = texture.Name;

                var deswizzled = texture.GetDeswizzledData(0, 0);

                var sformat = texture.Format.ToString();
                sformat = sformat.Replace("T_", "");

                if (sformat == "R8_G8_B8_A8_SRGB") sformat = "RGBA8_SRGB";

                TextureUtility.TexFormat informat;
                Enum.TryParse(sformat.ToUpper(), out informat);

                TextureUtility.Decode(informat, deswizzled, (int)texture.Width, (int)texture.Height, out var imgData);

                tex.data = imgData;

                if (tex.data == null)
                    continue;

                textures.Add(tex);

                ind++;
            }
        }
        else
        {
            var ind = 0;
            foreach (BfresLibrary.WiiU.Texture texture in resFile.Textures.Values)
            {
                var tex = new Texture();
                tex.width = (int)texture.Width;
                tex.height = (int)texture.Height;

                var cc = 0;

                var nonChannel = GX2CompSel.Always0;

                if (texture.CompSelR != nonChannel)
                    cc++;
                if (texture.CompSelG != nonChannel)
                    cc++;
                if (texture.CompSelB != nonChannel)
                    cc++;
                if (texture.CompSelA != nonChannel)
                    cc++;

                tex.channelCount = cc;

                tex.name = texture.Name;

                var deswizzled = texture.GetDeswizzledData(0, 0);

                var sformat = texture.Format.ToString();
                sformat = sformat.Replace("T_", "");

                if (sformat == "R8_G8_B8_A8_SRGB") sformat = "RGBA8_SRGB";

                TextureUtility.TexFormat informat;
                Enum.TryParse(sformat.ToUpper(), out informat);

                TextureUtility.Decode(informat, deswizzled, (int)texture.Width, (int)texture.Height, out var imgData);

                tex.data = imgData;

                if (tex.data == null)
                    continue;

                textures.Add(tex);

                ind++;
            }
        }
    }

    private static void ExtractAnimation(BoneAnim boneAnim, Animation anim, out Animation.NodeChannel channel)
    {
        channel = new Animation.NodeChannel();
        channel.NodeName = boneAnim.Name;

        // Set base data as default keyframes at frame 0
        channel.AddPosition(new Key<Vec3>(0, new Vec3(boneAnim.BaseData.Translate)));
        channel.AddRotation(new Key<Vec4>(0, new Vec4(boneAnim.BaseData.Rotate)));
        channel.AddScale(new Key<Vec3>(0, new Vec3(boneAnim.BaseData.Scale)));

        // Extract animation curves
        if (boneAnim.Curves.Count > 0)
        {
            for (var i1 = 0; i1 < boneAnim.Curves.Count; i1++)
            {
                var curve = boneAnim.Curves[i1];

                var trackType = (TrackType)curve.AnimDataOffset;
                var trackTypeString = trackType.ToString();

                var index = 0;

                if (trackTypeString.EndsWith("X"))
                    index = 0;
                else if (trackTypeString.EndsWith("Y"))
                    index = 1;
                else if (trackTypeString.EndsWith("Z"))
                    index = 2;
                else if (trackTypeString.EndsWith("W"))
                    index = 3;

                // Extract curve keyframes
                for (int frameIdx = 0; frameIdx < curve.Frames.Length; frameIdx++)
                {
                    float frame = curve.Frames[frameIdx];
                    float value = curve.Keys[frameIdx, 0] * curve.Scale + curve.Offset;

                    if (trackTypeString.StartsWith("Rotation"))
                    {
                        var rotValue = new Vec4();
                        rotValue[index] = value;
                        Key<Vec4> rotKey = new Key<Vec4>(frame, rotValue);
                        channel.AddRotation(rotKey);
                    }
                    else if (trackTypeString.StartsWith("Position"))
                    {
                        var posValue = new Vec3();
                        posValue[index] = value;
                        Key<Vec3> posKey = new Key<Vec3>(frame, posValue);
                        channel.AddPosition(posKey);
                    }
                    else if (trackTypeString.StartsWith("Scale"))
                    {
                        var scaleValue = new Vec3();
                        scaleValue[index] = value;
                        Key<Vec3> scaleKey = new Key<Vec3>(frame, scaleValue);
                        channel.AddScale(scaleKey);
                    }
                }
            }

            // Sort keys by time
            channel.Positions.Sort((a, b) => a.Time.CompareTo(b.Time));
            channel.Rotations.Sort((a, b) => a.Time.CompareTo(b.Time));
            channel.Scales.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
    }

    public static Vec4[] ReadVertexBuffer(VertexBuffer vtx, string name, ByteOrder order)
    {
        var helper = new VertexBufferHelper(vtx, order);

        //Set each array first from the lib if exist. Then add the data all in one loop
        List<Vec4> data = new();

        foreach (var (key, att) in vtx.Attributes)
            if (att.Name == name)
            {
                var dat = helper[att.Name].Data;

                foreach (var d in dat) data.Add(new Vec4(d.X, d.Y, d.Z, d.W));
            }

        return data.ToArray();
    }


    public static List<uint> GetDisplayFaces(List<uint> faces)
    {
        var displayFaceSize = 0;
        var strip = 0x40;
        if (strip >> 4 == 4)
        {
            displayFaceSize = faces.Count;
            return faces;
        }

        var f = new List<uint>();

        var startDirection = 1;
        var p = 0;
        var f1 = faces[p++];
        var f2 = faces[p++];
        var faceDirection = startDirection;
        uint f3;
        do
        {
            f3 = faces[p++];
            if (f3 == 0xFFFF)
            {
                f1 = faces[p++];
                f2 = faces[p++];
                faceDirection = startDirection;
            }
            else
            {
                faceDirection *= -1;
                if (f1 != f2 && f2 != f3 && f3 != f1)
                {
                    if (faceDirection > 0)
                    {
                        f.Add(f3);
                        f.Add(f2);
                        f.Add(f1);
                    }
                    else
                    {
                        f.Add(f2);
                        f.Add(f3);
                        f.Add(f1);
                    }
                }

                f1 = f2;
                f2 = f3;
            }
        } while (p < faces.Count);

        displayFaceSize = f.Count;
        return f;
    }


    public class KeyGroup
    {
    }
}