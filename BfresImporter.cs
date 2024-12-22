using static tmdl_utility.ModelUtility;
using BfresLibrary;
using System.Diagnostics;
using BfresLibrary.GX2;
using ZstdSharp;
using BfresLibrary.Helpers;
using Syroot.BinaryData;
using Bone = BfresLibrary.Bone;
using Model = BfresLibrary.Model;

namespace tmdl_utility;

public class BfresImporter
{
    public static void LoadBfres(UtilityInitInfo info)
    {
        MemoryStream stream = new MemoryStream();
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

        List<Animation> animations = new List<Animation>();
        foreach (var (name, anim) in resFile.SkeletalAnims)
        {
            var animation = new Animation();

            animation.name = name;
            animation.duration = anim.FrameCount;
            animation.ticksPerSecond = 60;

            foreach (var boneAnim in anim.BoneAnims)
            {
                ExtractAnimation(boneAnim, anim, out var channel, out var basePos, out var baseRot, out var baseScl);

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
                    animation.nodeChannels.Add(channel.NodeName, channel);
                }
            }

            animations.Add(animation);
        }

        foreach (var sceneModel in scene.models)
        {
            sceneModel.Animations = animations.ToArray();
        }

        var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

        outPath = Path.GetFullPath(outPath);

        var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

        scene.Write(outStream);

        outStream.Close();


        var startInfo = new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\TomatoEditor\\bin\\Debug\\TomatoEditor.exe\"");

        startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\TomatoEditor";
        startInfo.ArgumentList.Add($"{outPath}");

        var proc = System.Diagnostics.Process.Start(startInfo);
    }

    private static (int, ModelUtility.Model, Node) ExtractModel(Model resfileModel, int nct, ResFile resFile, Texture[] textures, Scene scene,
        ref int mshCt)
    {
        int vct = 0;
        var model = new ModelUtility.Model();

        model.Name = resfileModel.Name;

        var matList = new List<ModelUtility.Material>();
        var mlist = new List<ModelUtility.Mesh>();

        var rootNode = new Node(resfileModel.Name);
        rootNode.id = nct++;

        var armatureNode = new Node("Armature");

        var boneDict = new Dictionary<BfresLibrary.Bone, Node>();
        foreach (var bone in resfileModel.Skeleton.BoneList)
        {
            var bNode = new Node(bone.Name);

            bNode.Position = new Vec3(bone.Position.X, bone.Position.Y, bone.Position.Z);
            bNode.Rotation = new Vec4(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
            bNode.Scale = new Vec3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z);

            boneDict.Add(bone, bNode);
        }

        foreach (var (bone, node) in boneDict)
        {
            if (bone.ParentIndex == -1)
            {
                node.SetParent(armatureNode);
            }
            else
            {
                node.SetParent(boneDict.Values.ToList()[bone.ParentIndex]);
            }
        }

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
            List<int[]> bids = new List<int[]>();
            foreach (var vec4 in id0)
            {
                bids.Add(vec4.ToArray());
            }
            mesh.BoneIDs = bids.ToArray();

            List<float[]> wghts = new List<float[]>();
            foreach (var vec4 in w0)
            {
                wghts.Add(vec4.ToFltArray());
            }
            mesh.VertexWeights = wghts.ToArray();

            mesh.Indices = new ushort[shapeMesh.IndexCount];

            var idxs = GetDisplayFaces(shapeMesh.GetIndices().ToList());
            for (int i = 0; i < shapeMesh.IndexCount; i++)
            {
                mesh.Indices[i] = (ushort)(idxs[i] + shapeMesh.FirstVertex);
            }

            vct += mesh.Vertices.Length;

            mesh.MaterialIndex = shape.MaterialIndex;
            mshCt++;

            mlist.Add(mesh);
        }

        for (int j = 0; j < resfileModel.Materials.Count; j++)
        {
            var mat = resfileModel.Materials[j];
            var material = new ModelUtility.Material();

            material.name = mat.Name;
            foreach (var (s, sampler) in mat.Samplers)
            {
                material.Textures.Add(s, mat.TextureRefs[mat.Samplers.IndexOf(sampler)].Name);
            }


            matList.Add(material);
        }

        if (vct != resfileModel.TotalVertexCount)
            throw new Exception($"Vertex counts do not match. {vct}, {resfileModel.TotalVertexCount}");

        model.Meshes = mlist.ToArray();
        model.Textures = textures;
        model.Skeleton = new ModelUtility.Skeleton(armatureNode);
        model.Materials = matList.ToArray();

        scene.models.Add(model);
        return (nct, model, armatureNode);
    }

    private static void ExtractTextures(ResFile resFile, Texture[] textures)
    {
        if (resFile.IsPlatformSwitch)
        {
            int ind = 0;
            foreach (BfresLibrary.Switch.SwitchTexture texture in resFile.Textures.Values)
            {

                var tex = new Texture();
                tex.width = (int)texture.Width;
                tex.height = (int)texture.Height;

                var cc = 4;

                tex.channelCount = cc;

                tex.name = texture.Name;

                var deswizzled = texture.GetDeswizzledData(0, 0);

                string sformat = texture.Format.ToString();
                sformat = sformat.Replace("T_", "");

                if (sformat == "R8_G8_B8_A8_SRGB")
                {
                    sformat = "RGBA8_SRGB";
                }

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
            int ind = 0;
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

                string sformat = texture.Format.ToString();
                sformat = sformat.Replace("T_", "");

                if (sformat == "R8_G8_B8_A8_SRGB")
                {
                    sformat = "RGBA8_SRGB";
                }

                TextureUtility.TexFormat informat;
                Enum.TryParse(sformat.ToUpper(), out informat);

                TextureUtility.Decode(informat, deswizzled, (int)texture.Width, (int)texture.Height, out var imgData);

                tex.data = imgData;

                textures[ind] = tex;

                ind++;
            }

        }
    }

    private static void ExtractAnimation(BoneAnim boneAnim, SkeletalAnim anim, out Animation.NodeChannel channel, out Vec3 basePos,
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
                AnimCurve curve = boneAnim.Curves[i1];

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "PositionX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new Key<Vec3>((float)frame.Key, new Vec3(((HermiteKey)frame.Value).Value, 0, 0));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "PositionY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new Key<Vec3>((float)frame.Key, new Vec3(0, ((HermiteKey)frame.Value).Value, 0));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "PositionZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> posKey = new Key<Vec3>((float)frame.Key, new Vec3(0, 0, ((HermiteKey)frame.Value).Value));
                        channel.AddPosition(posKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "RotationX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new Key<Vec4>((float)frame.Key, new Vec4(((HermiteKey)frame.Value).Value, 0, 0, 0));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "RotationY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new Key<Vec4>((float)frame.Key, new Vec4(0, ((HermiteKey)frame.Value).Value, 0, 0));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "RotationZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new Key<Vec4>((float)frame.Key, new Vec4(0, 0, ((HermiteKey)frame.Value).Value, 0));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "RotationW", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec4> rotKey = new Key<Vec4>((float)frame.Key, new Vec4(0, 0, 0, ((HermiteKey)frame.Value).Value));
                        channel.AddRotation(rotKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "ScaleX", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new Key<Vec3>((float)frame.Key, new Vec3(((HermiteKey)frame.Value).Value, 0, 0));
                        channel.AddScale(scaleKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "ScaleY", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new Key<Vec3>((float)frame.Key, new Vec3(0, ((HermiteKey)frame.Value).Value, 0));
                        channel.AddScale(scaleKey);
                    }
                }

                {
                    CurveAnimHelper helper = CurveAnimHelper.FromCurve(curve, "ScaleZ", false);
                    foreach (KeyValuePair<float, object> frame in helper.KeyFrames)
                    {
                        Key<Vec3> scaleKey = new Key<Vec3>((float)frame.Key, new Vec3(0, 0, ((HermiteKey)frame.Value).Value));
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
        VertexBufferHelper helper = new VertexBufferHelper(vtx, order);

        //Set each array first from the lib if exist. Then add the data all in one loop
        List<Vec4> data = new List<Vec4>();

        foreach (var (key, att) in vtx.Attributes)
        {
            if (att.Name == name)
            {
                var dat = helper[att.Name].Data;

                foreach (var d in dat)
                {

                    data.Add(new Vec4(d.X, d.Y, d.Z, d.W));
                }
            }
        }

        return data.ToArray();
    }


    public static List<uint> GetDisplayFaces(List<uint> faces)
    {
        var displayFaceSize = 0;
        var strip = 0x40;
        if ((strip >> 4) == 4)
        {
            displayFaceSize = faces.Count;
            return faces;
        }
        else
        {
            List<uint> f = new List<uint>();

            int startDirection = 1;
            int p = 0;
            uint f1 = faces[p++];
            uint f2 = faces[p++];
            int faceDirection = startDirection;
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
                    if ((f1 != f2) && (f2 != f3) && (f3 != f1))
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

}