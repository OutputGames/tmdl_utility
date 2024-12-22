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
        public ModelUtility(UtilityInitInfo info)
        {
            if (info.Type == UtilityInitInfo.ExportType.Single)
            {
                ModelUtility.Scene scn;
                if (info.Source.EndsWith("bfres") || info.Source.EndsWith("zs"))
                {
                    scn = BfresImporter.LoadBfres(info);
                }
                else
                {
                    scn = AssimpImporter.LoadAssimp(info);
                }

                bool export = true;

                if (!export)
                {
                    var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

                    outPath = Path.GetFullPath(outPath);

                    var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

                    scn.Write(outStream);

                    outStream.Close();
                }
                else
                {
                    scn.Export(info.Dest + "/TMDL_Squid/" + Path.GetFileNameWithoutExtension(info.Source), "fbx");
                }

            }
        }
    }
}
