using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Xml.Linq;
using Assimp;
using Assimp.Unmanaged;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace tmdl_utility
{
    internal class ModelUtility
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
                        writer.Write((uint)Meshes.Length);
                        foreach (var mesh in Meshes)
                        {
                            writer.WriteString("TMSH");
                            writer.Write((uint)mesh.Vertices.Length);

                            writer.WriteString("TVTX");
                            foreach (var meshVertex in mesh.Vertices)
                            {
                                writer.Write(meshVertex.X);
                                writer.Write(meshVertex.Y);
                                writer.Write(meshVertex.Z);
                            }

                            writer.WriteString("TNRM");
                            foreach (var meshVertex in mesh.Normals)
                            {
                                writer.Write(meshVertex.X);
                                writer.Write(meshVertex.Y);
                                writer.Write(meshVertex.Z);
                            }

                            writer.WriteString("TUV0");
                            foreach (var meshVertex in mesh.UV0)
                            {
                                writer.Write(meshVertex.X);
                                writer.Write(meshVertex.Y);
                            }
                        }
                    }
                    return ms.ToArray();
                }
            }
         }

        public ModelUtility(UtilityInitInfo info)
        {
            if (info.Type == UtilityInitInfo.ExportType.Single)
            {
                if (info.Source.EndsWith("bfres") || info.Source.EndsWith("zs"))
                {
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

                        for (var i = 0; i < sceneMesh.Vertices.Count; i++)
                        {
                            var vtx = sceneMesh.Vertices[i];
                            var nrm = sceneMesh.Normals[i];
                            var uv0 = sceneMesh.TextureCoordinateChannels[0][i];

                            mesh.Vertices[i] = new Vec3(vtx.X, vtx.Y,vtx.Z);
                            mesh.Normals[i] = new Vec3(nrm.X, nrm.Y, nrm.Z);
                            mesh.UV0[i] = new Vec2(uv0.X, uv0.Y);
                        }

                        mesh.MaterialIndex = sceneMesh.MaterialIndex;

                        model.Meshes[scene.Meshes.IndexOf(sceneMesh)] = mesh;
                    }

                    var outStream = new ModelWriter(new FileStream(info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl", FileMode.OpenOrCreate));

                    outStream.WriteString("TMDL");
                    outStream.Write(model.ToBytes());

                    outStream.Close();
                }
            }


        }



    }
}
