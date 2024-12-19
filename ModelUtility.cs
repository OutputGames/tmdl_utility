using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Xml.Linq;
using Assimp;
using Assimp.Unmanaged;
using BfresLibrary;
using BfresLibrary.GX2;
using BfresLibrary.Helpers;
using BfresLibrary.Swizzling;
using Newtonsoft.Json;
using StbiSharp;
using Syroot.BinaryData;
using Syroot.Maths;
using Syroot.NintenTools.NSW.Bntx.GFX;
using ZstdSharp;
using ZstdSharp.Unsafe;
using static tmdl_utility.ModelUtility;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace tmdl_utility
{
    public partial class ModelUtility
    {

        public class ModelWriter : BinaryWriter
        {

            public ModelWriter(FileStream stream) : base(stream) { } 
            public ModelWriter(MemoryStream stream) : base(stream) { }

            public void WriteString(string s)
            {
                Write(System.Text.Encoding.UTF8.GetBytes(s));
            }

            public void WriteNonSigString(string s)
            {
                Write(s.Length);
                WriteString(s);
            }
        }

        public static byte[] ConvertBgraToRgba(byte[] bytes)
        {
            if (bytes == null)
                throw new Exception("Data block returned null. Make sure the parameters and image properties are correct!");

            for (int i = 0; i < bytes.Length; i += 4)
            {
                var temp = bytes[i];
                bytes[i] = bytes[i + 2];
                bytes[i + 2] = temp;

            }
            return bytes;
        }


        public class Vec4
        {
            public float X, Y, Z, W;

            public Vec4(float x, float y, float z, float w)
            {
                X = x;
                Y = y;
                Z = z;
                W = w;
            }

            public Vec3 ToVec3()
            {
                return new Vec3(X, Y, Z);
            }

            public Vec2 ToVec2()
            {
                return new Vec2(X, Y);
            }

            public float Magnitude
            {
                get { return MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2) + MathF.Pow(W, 2)); }

            }

            public Vec4 Normalized
            {
                get
                {
                    var mag = this.Magnitude;

                    if (mag < 0.0001f)
                    {
                        return this;
                    }

                    return new Vec4(X / mag, Y / mag, Z / mag, W / mag);
                }
            }
        }
        

        public class Vec3
        {
            public float X, Y, Z;

            public Vec3(float x,float y,float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public Vec3(float s)
            {
                X = s;
                Y = s;
                Z = s;
            }

            public Vec3() : this(0)
            {
                
            }

            public void Write(ModelWriter writer) {
                writer.Write(X);
                writer.Write(Y);
                writer.Write(Z);
            }


            public float Magnitude
            {
                get { return MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2)); }

            }

            public static Vec3 operator +(Vec3 v1, Vec3 v2)
            {
                return new Vec3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
            }

            public static Vec3 operator -(Vec3 v1, Vec3 v2)
            {
                return new Vec3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
            }

            public static Vec3 operator *(Vec3 v1, Vec3 v2)
            {
                return new Vec3(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
            }
            public static Vec3 operator /(Vec3 v1, Vec3 v2)
            {
                return new Vec3(v1.X/ v2.X, v1.Y / v2.Y, v1.Z / v2.Z);
            }
            public static Vec3 operator /(Vec3 v1, float v2)
            {
                return new Vec3(v1.X / v2, v1.Y / v2, v1.Z / v2);
            }

            public static Vec3 operator-(Vec3 v1)
            {
                return new Vec3(-v1.X, -v1.Y, -v1.Z);
            }
        }

        public class Vec2
        {
            public float X, Y;

            public Vec2(float x, float y)
            {
                X = x;
                Y = y;
            }
        }


        public class Mesh
        {
            public Vec3[] Vertices;
            public Vec3[] Normals;
            public Vec2[] UV0;
            public int[][] BoneIDs;
            public float[][] VertexWeights;

            public UInt16[] Indices;
            

            public int MaterialIndex;
        }

        public class Texture
        {
            public int width, height;
            public byte[] data;
            public int channelCount;
            public string name;

        }

        public class Material
        {
            public string name;
            public Dictionary<string,string> Textures = new Dictionary<string, string>();
        }

        public class Key<T>
        {
            public float timeStamp;

            private T value;
        }

        public class Bone
        {
            public string name;
            public Matrix4 transform;

            public List<Bone> children;
        }

        public class Skeleton
        {
            public Bone rootBone;
        }


        public class Animation
        {
            public class BoneInfo
            {
                public int Index;
                public List<Key<Vec3>> Positions;
                public List<Key<Vec4>> Rotations;
                public List<Key<Vec3>> Scales;
            }

            public Dictionary<string, BoneInfo> infoMap;
            public float duration;
            public int ticksPerSecond;
            
        }

        public class Model
        {
            public Mesh[] Meshes;
            public Texture[] Textures;
            public Material[] Materials;
            public Skeleton[] Skeletons;
            public Animation[] Animations;

            public float ModelScale = 1;

            public byte[] ToBytes()
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (ModelWriter writer = new ModelWriter(ms))
                    {

                        writer.Write(ModelScale);

                        writer.Write(Meshes.Length);
                        foreach (var mesh in Meshes)
                        {
                            writer.WriteString("TMSH");
                            writer.Write(mesh.Vertices.Length);

                            writer.WriteString("TVTX");
                            for (var i = 0; i < mesh.Vertices.Length; i++)
                            {
                                var meshVertex = mesh.Vertices[i];
                                writer.Write(meshVertex.X);
                                writer.Write(meshVertex.Y);
                                writer.Write(meshVertex.Z);
                                meshVertex = mesh.Normals[i];
                                writer.Write(meshVertex.X);
                                writer.Write(meshVertex.Y);
                                writer.Write(meshVertex.Z);
                                var _meshVertex = mesh.UV0[i];
                                writer.Write(_meshVertex.X);
                                writer.Write(_meshVertex.Y);

                                var boneIdx = mesh.BoneIDs[i];
                                writer.Write(boneIdx.Length);
                                for (int j = 0; j < boneIdx.Length; j++)
                                {
                                    writer.Write(boneIdx[j]);
                                }


                                var weights = mesh.VertexWeights[i];
                                writer.Write(weights.Length);
                                for (int j = 0; j < weights.Length; j++)
                                {
                                    writer.Write(weights[j]);
                                }
                            }


                            writer.WriteString("TIDX");
                            writer.Write(mesh.Indices.Length);
                            foreach (var meshIndex in mesh.Indices)
                            {
                                writer.Write(meshIndex);
                            }

                            writer.Write(mesh.MaterialIndex);
                        }

                        writer.Write(Textures.Length);
                        foreach (var texture in Textures)
                        {
                            writer.WriteString("TTEX");

                            writer.Write(texture.name.Length);
                            writer.WriteString(texture.name);
                            writer.Write(texture.width);
                            writer.Write(texture.height);
                            writer.Write(texture.channelCount);

                            //writer.Write(texture.data);
                            
                            
                            for (int y  = 0; y < texture.height; y++)
                            {
                                for (int x = 0; x < texture.width; x++)
                                {
                                    for (int i = 0; i < texture.channelCount; i++)
                                    {
                                        var idx = (y * texture.width + x) * texture.channelCount;
                                        writer.Write(texture.data[idx + i]);
                                    }
                                }
                            }
                            
                            
                            
                        }

                        writer.Write(Materials.Length);
                        foreach (var material in Materials)
                        {
                            writer.WriteString("TMAT");

                            writer.WriteNonSigString(material.name);

                            writer.Write(material.Textures.Count);
                            foreach (var (key, value) in material.Textures)
                            {
                                writer.WriteNonSigString(key);
                                writer.WriteNonSigString(value);
                            }
                        }

                        writer.Close();
                    }

                    return ms.ToArray();
                }
            }
        }

        public class Node {
            public string name;
            public Vec3 Position = new Vec3();
            public Vec3 Rotation = new Vec3();
            public Vec3 Scale = new Vec3();

            public List<int> Meshes;

            public List<Node> children = new List<Node>();

            public Bone GetBone() {
                var bone = new Bone();
                bone.name = name;

                foreach (var child in children)
                {
                    bone.children.Add(child.GetBone());
                }

                return bone;
            }

            public void Write(ModelWriter writer) {
                writer.WriteNonSigString(name);

                Position.Write(writer);
                Rotation.Write(writer);
                Scale.Write(writer);

                foreach (var idx in Meshes)
                {
                    writer.Write(idx);
                }

                writer.Write(children.Count);
                foreach (var child in children)
                {
                    child.Write(writer);
                }
            }

            public Node GetChild(string name) {
                foreach (var child in children)
                {
                    if (child.name == name)
                        return child;
                }

                return null;
            }

            public List<Node> GetAllChildren() {
                var c = new List<Node>();

                c.AddRange(children);
                foreach (var child in children)
                {
                    c.AddRange(child.GetAllChildren());
                }

                return c;
            }

            public Node AddNode(string name) {
                var node = new Node();

                node.name = name;

                children.Add(node);

                return node;
            }
        }

        public class Scene {
            public string name;

            public Node rootNode;

            public void Write(ModelWriter writer) {

                writer.WriteNonSigString(name);
                
                rootNode.Write(writer);

            }

        }

        public Node ProcessAiNode(Assimp.Node ai, bool isBone = false) {

            var node = new Node();

            node.name = ai.Name;

            node.Meshes = ai.MeshIndices;

            node.Position.X = ai.Transform[0, 3];
            node.Position.Y = ai.Transform[1, 3];
            node.Position.Z = ai.Transform[2, 3];

            Vec3[] cols = new Vec3[]
            {
                new Vec3(ai.Transform[0,0], ai.Transform[1,0], ai.Transform[2,0]),
                new Vec3(ai.Transform[0,2], ai.Transform[1,1], ai.Transform[2,1]),
                new Vec3(ai.Transform[0,1], ai.Transform[1,2], ai.Transform[2,2]),
            };

            node.Scale.X = cols[0].Magnitude;
            node.Scale.Y = cols[1].Magnitude;
            node.Scale.Z = cols[2].Magnitude;

            if (ai.Transform.GetDeterminant() < 0) node.Scale = -node.Scale;

            cols[0] /= node.Scale.X;
            cols[1] /= node.Scale.Y;
            cols[2] /= node.Scale.Z;

            //Matrix3x3 r = new Matrix3x3(cols[0].X, cols[0].Y, cols[0].Z, cols[1].X, cols[1].Y, cols[1].Z, cols[2].X, cols[2].Y, cols[2].Z);

            //node.Rotation =

            foreach (var child in ai.Children)
            {
                var ib = isBone;
                if (ai.Name == "Armature")
                    ib = true;

                node.children.Add(ProcessAiNode(child, ib));
            }

            return node;
        }

        public Vec4[] ReadVertexBuffer(VertexBuffer vtx, string name, ByteOrder order)
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
                        
                        data.Add(new Vec4(d.X, d.Y,d.Z, d.W));
                    }
                }
            }

            return data.ToArray();
        }

       
        public List<uint> GetDisplayFaces(List<uint> faces)
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
        
        public ModelUtility(UtilityInitInfo info)
        {
            if (info.Type == UtilityInitInfo.ExportType.Single)
            {
                if (info.Source.EndsWith("bfres") || info.Source.EndsWith("zs"))
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
                    var model = new Model();

                    #region Bfres Model Loading

                    var matList = new List<Material>();
                    var mlist = new List<Mesh>();
                    foreach (var (key, resfileModel) in resFile.Models)
                    {
                        int vct = 0;
                        int midx = matList.Count;
                        foreach (var (s, shape) in resfileModel.Shapes)
                        {
                            var vertexBuffer = resfileModel.VertexBuffers[shape.VertexBufferIndex];

                            var pos = ReadVertexBuffer(vertexBuffer, "_p0", resFile.ByteOrder);
                            var nrm = ReadVertexBuffer(vertexBuffer, "_n0", resFile.ByteOrder);
                            var uv0 = ReadVertexBuffer(vertexBuffer, "_u0", resFile.ByteOrder);

                            var shapeMesh = shape.Meshes[0];

                            var mesh = new Mesh();

                            mesh.Vertices = pos.ToVec3Array();
                            mesh.Normals = nrm.ToVec3Array();
                            mesh.UV0 = uv0.ToVec2Array();
                            mesh.Indices = new ushort[shapeMesh.IndexCount];

                            var idxs = GetDisplayFaces(shapeMesh.GetIndices().ToList());
                            for (int i = 0; i < shapeMesh.IndexCount; i++)
                            {
                                mesh.Indices[i] = (ushort)(idxs[i] + shapeMesh.FirstVertex);
                            }

                            vct += mesh.Vertices.Length;

                            mesh.MaterialIndex = shape.MaterialIndex + midx;


                            mlist.Add(mesh);
                        }

                        for (int j = 0; j < resfileModel.Materials.Count; j++)
                        {
                            var mat = resfileModel.Materials[j];
                            var material = new Material();

                            material.name = mat.Name;
                            foreach (var (s, sampler) in mat.Samplers)
                            {
                                material.Textures.Add(s, mat.TextureRefs[mat.Samplers.IndexOf(sampler)].Name);
                            }


                            matList.Add(material);
                        }

                        if (vct != resfileModel.TotalVertexCount)
                            throw new Exception($"Vertex counts do not match. {vct}, {resfileModel.TotalVertexCount}");
                    }
                    model.Meshes = mlist.ToArray();

                    #endregion

                    model.Textures = new Texture[resFile.Textures.Count];
                    if (resFile.IsPlatformSwitch)
                    {
                        model.ModelScale = 1;
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

                            model.Textures[ind] = tex;

                            ind++;
                        }
                    }
                    else
                    {
                        model.ModelScale = 0.1f;
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

                            model.Textures[ind] = tex;

                            ind++;
                        }
                        
                    }

                    model.Materials = matList.ToArray();

                    var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

                    outPath = Path.GetFullPath(outPath);

                    var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

                    outStream.WriteString("TMDL");
                    outStream.Write(model.ToBytes());

                    outStream.Close();

                    
                    var startInfo = new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\TomatoEditor\\bin\\Debug\\TomatoEditor.exe\"");

                    startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\TomatoEditor";
                    startInfo.ArgumentList.Add($"{outPath}");

                    var proc = System.Diagnostics.Process.Start(startInfo);
                    

                }
                else
                {
                    var importer = new AssimpContext();
                    //importer.SetConfig();

                    var maxBonesPerVertex = 4;

                    var scene = importer.ImportFile(info.Source,
                        PostProcessSteps.GenerateNormals | PostProcessSteps.GenerateUVCoords |
                        PostProcessSteps.FlipUVs |
                        PostProcessSteps.Triangulate);

                    Directory.CreateDirectory(info.Dest);

                    var mscene = new Scene();

                    mscene.name = scene.Name;

                    mscene.rootNode = ProcessAiNode(scene.RootNode);

                    var armatureNode = mscene.rootNode.AddNode("Armature");

                    var boneDict = new Dictionary<string, Assimp.Bone>();
;
                    var model = new Model();
                    model.Meshes = new Mesh[scene.MeshCount];

                    foreach (var sceneMesh in scene.Meshes)
                    {
                        var mesh = new Mesh();
                        mesh.Vertices = new Vec3[sceneMesh.VertexCount];
                        mesh.Normals = new Vec3[sceneMesh.VertexCount];
                        mesh.UV0 = new Vec2[sceneMesh.VertexCount];
                        mesh.VertexWeights = new float[sceneMesh.VertexCount][];
                        mesh.BoneIDs = new int[sceneMesh.VertexCount][];

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

                            mesh.Vertices[i] = new Vec3(vtx.X, vtx.Y,vtx.Z);
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
                            if (!boneDict.ContainsKey(bone.Name)) {
                                boneDict.Add(bone.Name, bone);
                            }
                        }

                        mesh.MaterialIndex = sceneMesh.MaterialIndex;

                        model.Meshes[scene.Meshes.IndexOf(sceneMesh)] = mesh;

                        
                    }

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

                    model.Materials = new Material[scene.MaterialCount];

                    foreach (var sceneMaterial in scene.Materials)
                    {
                        var material = new Material();

                        foreach (var allMaterialTexture in sceneMaterial.GetAllMaterialTextures())
                        {
                            material.Textures.Add(allMaterialTexture.TextureType.ToString(), model.Textures[allMaterialTexture.TextureIndex].name);
                        }

                        material.name = sceneMaterial.Name;

                        model.Materials[scene.Materials.IndexOf(sceneMaterial)] = material;
                    }

                    var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

                    outPath = Path.GetFullPath(outPath);

                    var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

                    outStream.WriteString("TMDL");
                    outStream.Write(model.ToBytes());

                    outStream.Close();

                    
                    var startInfo = new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\TomatoEditor\\bin\\Debug\\TomatoEditor.exe\"");

                    startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\TomatoEditor";
                    startInfo.ArgumentList.Add($"{outPath}");

                    var proc = System.Diagnostics.Process.Start(startInfo);
                        
                }
                }


        }
    }
}
