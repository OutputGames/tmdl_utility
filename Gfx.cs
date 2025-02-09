using System.Drawing;
using System.Drawing.Imaging;

namespace tmdl_utility;

public partial class ModelUtility
{
    public class Mesh
    {
        public int[][] BoneIDs;

        public ushort[] Indices;


        public int MaterialIndex;
        public string Name;
        public Vec3[] Normals;
        public Vec2[] UV0;
        public float[][] VertexWeights;
        public Vec3[] Vertices;
    }

    public class Texture
    {
        public int channelCount;
        public byte[] data;
        public string name;
        public int width, height;

        public void Export(string path)
        {
            var b = new Bitmap(width, height);

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var c = new byte[4];
                for (var i = 0; i < channelCount; i++)
                {
                    var data = this.data[
                        (x + y * width) * channelCount + i];
                    c[i] = data;
                }

                b.SetPixel(x, y, Color.FromArgb(c[3], c[0], c[1], c[2]));
            }

            b.Save(path, ImageFormat.Png);
        }

        public byte[] Export()
        {
            var b = new Bitmap(width, height);

            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var c = new byte[4];
                for (var i = 0; i < channelCount; i++)
                {
                    var data = this.data[(x + y * width) * channelCount + i];
                    c[i] = data;
                }

                b.SetPixel(x, y, Color.FromArgb(c[3], c[0], c[1], c[2]));
            }


            using (var ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public class Material
    {
        public string name;
        public Dictionary<string, string> Textures = new();
    }

    public class Model
    {
        public Animation[] Animations;
        public Material[] Materials;
        public Mesh[] Meshes;

        public float ModelScale = 1;
        public string Name;
        public Skeleton Skeleton;
        public Texture[] Textures;

        public void Write(ModelWriter writer)
        {
            writer.WriteString("TMDL");
            writer.Write(ModelScale);
            writer.WriteNonSigString(Name);

            writer.Write(Meshes.Length);
            foreach (var mesh in Meshes)
            {
                writer.WriteString("TMSH");

                writer.WriteNonSigString(mesh.Name);

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

                    if (mesh.BoneIDs.Length > 0)
                    {
                        var boneIdx = mesh.BoneIDs[i];
                        writer.Write(boneIdx.Length);
                        for (var j = 0; j < boneIdx.Length; j++) writer.Write(boneIdx[j]);

                        if (mesh.VertexWeights.Length > 0)
                        {
                            var weights = mesh.VertexWeights[i];
                            writer.Write(weights.Length);
                            for (var j = 0; j < weights.Length; j++) writer.Write(weights[j]);
                        }
                        else
                        {
                            writer.Write(4);
                            for (var j = 0; j < 4; j++) writer.Write(1.0f);
                        }
                    }
                    else
                    {
                        writer.Write(4);
                        for (var j = 0; j < 4; j++) writer.Write(-1);


                        writer.Write(4);
                        for (var j = 0; j < 4; j++) writer.Write(0);
                    }
                }


                writer.WriteString("TIDX");
                writer.Write(mesh.Indices.Length);
                foreach (var meshIndex in mesh.Indices) writer.Write(meshIndex);

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


                for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                for (var i = 0; i < texture.channelCount; i++)
                {
                    var idx = (y * texture.width + x) * texture.channelCount;
                    writer.Write(texture.data[idx + i]);
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

            writer.WriteString("TSKL");
            Skeleton.Write(writer);

            writer.Write(Animations.Length);
            foreach (var anim in Animations) anim.Write(writer);
        }
    }
}