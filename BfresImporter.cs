using BfresLibrary;
using BfresLibrary.GX2;
using BfresLibrary.Helpers;
using BfresLibrary.Switch;
using Syroot.BinaryData;
using ZstdSharp;
using static tmdl_utility.ModelUtility;
using Bone = BfresLibrary.Bone;
using Model = BfresLibrary.Model;

namespace tmdl_utility;

public class BfresImporter
{
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

        var textures = new Texture[resFile.Textures.Count];
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
            animation.duration = anim.FrameCount;
            animation.ticksPerSecond = 60;

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
                ExtractAnimation(boneAnim, anim, out var channel, out var basePos, out var baseRot, out var baseScl);


                channel.Positions.Add(new Key<Vec3>(0, new Vec3(boneAnim.BaseData.Translate)));

                channel.Rotations.Add(new Key<Vec4>(0, new Vec4(boneAnim.BaseData.Rotate)));
                channel.Scales.Add(new Key<Vec3>(0, new Vec3(boneAnim.BaseData.Scale)));

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
        Texture[] textures, Scene scene,
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
                //vec4.X -= 1;
                //vec4.Y -= 1;
                //vec4.Z -= 1;
                //vec4.W -= 1;
                int[] bid = { -1, -1, -1, -1 };
                for (var i = 0; i < shape.VertexSkinCount; i++) bid[i] = (int)vec4[i];
                bids.Add(bid);
            }

            if (shape.SkinBoneIndices.Count > 1)
                for (var j = 0; j < bids.Count; j++)
                {
                    var b = bids[j];
                    for (var i = 0; i < shape.VertexSkinCount; i++)
                    {
                        var k = b[i];
                        var boneIndices = 0;
                        boneIndices = resfileModel.Skeleton.MatrixToBoneList[k];

                        var bone = resfileModel.Skeleton.Bones[boneIndices];
                        var ogBone = resfileModel.Skeleton.Bones[i];

                        b[i] = boneIndices;
                    }

                    bids[j] = b;
                }
            else
                for (var j = 0; j < bids.Count; j++)
                {
                    var b = bids[j];
                    for (var i = 0; i < b.Length; i++) b[i] = 0;

                    bids[j] = b;
                }


            mesh.BoneIDs = bids.ToArray();

            if (w0.Length == 0)
                w0 = new Vec4[mesh.BoneIDs.Length];
            for (var i = 0; i < mesh.BoneIDs.Length; i++)
            {
                var w = new Vec4(1, 0, 0, 0);
                w0[i] = w;
            }

            List<float[]> wghts = new();
            foreach (var vec4 in w0) wghts.Add(vec4.ToFltArray());
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
        model.Textures = textures;
        model.Skeleton = new ModelUtility.Skeleton(armatureNode.children[0]);

        foreach (var skeletonBone in model.Skeleton.bones)
        {
            var idx = model.Skeleton.bones.IndexOf(skeletonBone);
            var bone = resfileModel.Skeleton.Bones[idx];

            if (resfileModel.Skeleton.NumSmoothMatrices > 0 && bone.SmoothMatrixIndex > -1)
            {
                var mat = resfileModel.Skeleton.InverseModelMatrices;

                skeletonBone.offsetMatrix = mat[bone.SmoothMatrixIndex].ConvertMatrix3x4();

                //skeletonBone.offsetMatrix.DecomposeMatrix(out var translation, out var rotation, out var scale);

                //Matrix4x4.Invert(skeletonBone.offsetMatrix, out skeletonBone.offsetMatrix);

                //skeletonBone.offsetMatrix = Matrix4x4.Transpose(skeletonBone.offsetMatrix);
            }

            /*
            var m = MatrixExtensions.CalculateTransformMatrix(bone);
            Matrix4x4.Invert(m, out m);
            m = Matrix4x4.Transpose(m);
            */
            //skeletonBone.offsetMatrix = m;

            //skeletonBone.offsetMatrix = ModelUtility.Bone.CalculateOffsetMatrix(skeletonBone).Item2;
        }


        for (var i = 0; i < 4; i++)
        {
            var id = mlist[0].BoneIDs[0][i];
            if (id == -1)
                continue;

            Console.WriteLine(id);
            Console.WriteLine(resfileModel.Skeleton.Bones[id].Name);
        }

        model.Materials = matList.ToArray();

        scene.models.Add(model);
        return (nct, model, armatureNode);
    }

    private static void ExtractTextures(ResFile resFile, Texture[] textures)
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

                textures[ind] = tex;

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

                textures[ind] = tex;

                ind++;
            }
        }
    }

    private static void ExtractAnimation(BoneAnim boneAnim, SkeletalAnim anim, out Animation.NodeChannel channel,
        out Vec3 basePos,
        out Vec4 baseRot, out Vec3 baseScl)
    {
        channel = new Animation.NodeChannel();
        channel.NodeName = boneAnim.Name;

        basePos = new Vec3(boneAnim.BaseData.Translate);
        baseRot = new Vec4(boneAnim.BaseData.Rotate);
        baseScl = new Vec3(boneAnim.BaseData.Scale);

        // refactor --
        if (boneAnim.Curves.Count > 0)
        {
            for (var i1 = 0; i1 < boneAnim.Curves.Count; i1++)
            {
                var curve = boneAnim.Curves[i1];

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "PositionX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new(frame.Key, new Vec3(((HermiteKey)frame.Value).Value, 0, 0));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "PositionY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new(frame.Key, new Vec3(0, ((HermiteKey)frame.Value).Value, 0));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "PositionZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new(frame.Key, new Vec3(0, 0, ((HermiteKey)frame.Value).Value));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "RotationX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new(frame.Key, new Vec4(((HermiteKey)frame.Value).Value, 0, 0, 1));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "RotationY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new(frame.Key, new Vec4(0, ((HermiteKey)frame.Value).Value, 0, 1));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "RotationZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new(frame.Key, new Vec4(0, 0, ((HermiteKey)frame.Value).Value, 1));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "RotationW", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new(frame.Key, new Vec4(0, 0, 0, ((HermiteKey)frame.Value).Value));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "ScaleX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new(frame.Key, new Vec3(((HermiteKey)frame.Value).Value, 0, 0));
                        channel.AddScale(scaleKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "ScaleY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new(frame.Key, new Vec3(0, ((HermiteKey)frame.Value).Value, 0));
                        channel.AddScale(scaleKey);
                    }
                }

                {
                    var helper = CurveAnimHelper.FromCurve(curve, "ScaleZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new(frame.Key, new Vec3(0, 0, ((HermiteKey)frame.Value).Value));
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
}