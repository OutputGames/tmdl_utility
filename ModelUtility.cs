using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Xml.Linq;
using Assimp;
using Assimp.Unmanaged;
using BfresLibrary;
using BfresLibrary.Helpers;
using Newtonsoft.Json;
using Syroot.BinaryData;
using ZstdSharp;
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
            public UInt16[] Indices;

            public int MaterialIndex;
        }

        public class Model
        {
            public Mesh[] Meshes;

            public byte[] ToBytes()
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (ModelWriter writer = new ModelWriter(ms))
                    {
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
                            }


                            writer.WriteString("TIDX");
                            writer.Write(mesh.Indices.Length);
                            foreach (var meshIndex in mesh.Indices)
                            {
                                writer.Write(meshIndex);
                            }

                            writer.Write(mesh.MaterialIndex);
                        }
                    }
                    return ms.ToArray();
                }
            }
         }

        public Vec4[] ReadVertexBuffer(VertexBuffer vtx, string name)
        {
            VertexBufferHelper helper = new VertexBufferHelper(vtx, ByteOrder.BigEndian);

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
                    foreach (var (key, resfileModel) in resFile.Models)
                    {
                        var model = new Model();
                        var mlist = new List<Mesh>();
                        foreach (var (s, shape) in resfileModel.Shapes)
                        {
                            var vertexBuffer = resfileModel.VertexBuffers[shape.VertexBufferIndex];

                            var pos = ReadVertexBuffer(vertexBuffer, "_p0");
                            var nrm = ReadVertexBuffer(vertexBuffer, "_n0");
                            var uv0 = ReadVertexBuffer(vertexBuffer, "_u0");

                            var shapeMesh = shape.Meshes[0];

                            var mesh = new Mesh();

                            mesh.Vertices = pos.ToVec3Array();
                            mesh.Normals = nrm.ToVec3Array();
                            mesh.UV0 = uv0.ToVec2Array();
                            mesh.Indices = new UInt16[shapeMesh.IndexCount];

                            var idxs = shapeMesh.GetIndices().ToArray();
                            for (int i = 0; i < shapeMesh.IndexCount; i++)
                            {
                                mesh.Indices[i] = (ushort)(idxs[i] + shapeMesh.FirstVertex);
                            }


                            mlist.Add(mesh);
                        }

                        model.Meshes = mlist.ToArray();

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

                       break;
                    }

                }
                    else
                    {
                        var importer = new AssimpContext();
                        //importer.SetConfig();

                        var scene = importer.ImportFile(info.Source,
                            PostProcessSteps.GenerateNormals | PostProcessSteps.GenerateUVCoords |
                            PostProcessSteps.FlipUVs |
                            PostProcessSteps.Triangulate);

                        Directory.CreateDirectory(info.Dest);

                        var model = new Model();
                        model.Meshes = new Mesh[scene.MeshCount];

                        foreach (var sceneMesh in scene.Meshes)
                        {
                            var mesh = new Mesh();
                            mesh.Vertices = new Vec3[sceneMesh.VertexCount];
                            mesh.Normals = new Vec3[sceneMesh.VertexCount];
                            mesh.UV0 = new Vec2[sceneMesh.VertexCount];
                            mesh.Indices = new UInt16[sceneMesh.FaceCount * 3];

                            for (var i = 0; i < sceneMesh.Vertices.Count; i++)
                            {
                                var vtx = sceneMesh.Vertices[i];
                                var nrm = sceneMesh.Normals[i];
                                var uv0 = sceneMesh.TextureCoordinateChannels[0][i];

                                mesh.Vertices[i] = new Vec3(vtx.X, vtx.Y,vtx.Z);
                                mesh.Normals[i] = new Vec3(nrm.X, nrm.Y, nrm.Z);
                                mesh.UV0[i] = new Vec2(uv0.X, uv0.Y);
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

                            mesh.MaterialIndex = sceneMesh.MaterialIndex;

                            model.Meshes[scene.Meshes.IndexOf(sceneMesh)] = mesh;
                        }

                        var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

                        outPath = Path.GetFullPath(outPath);

                        var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

                        outStream.WriteString("TMDL");
                        outStream.Write(model.ToBytes());

                        outStream.Close();

                        /*
                        var startInfo = new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\TomatoEditor\\bin\\Debug\\TomatoEditor.exe\"");

                        startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\TomatoEditor";
                        startInfo.ArgumentList.Add($"{outPath}");

                        var proc = System.Diagnostics.Process.Start(startInfo);
                        */
                    }
                }


        }
    }
}
