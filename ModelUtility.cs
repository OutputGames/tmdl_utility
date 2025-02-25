//#define EXP_MDL

#if !(DEBUG)
#undef EXP_MDL
#endif

using System.Diagnostics;

namespace tmdl_utility;

public partial class ModelUtility
{
    public ModelUtility(UtilityInitInfo info)
    {
        if (info.Type == UtilityInitInfo.ExportType.Single)
        {
            Scene scn;
            if (info.Source.EndsWith("bfres") || info.Source.EndsWith("zs"))
                scn = BfresImporter.LoadBfres(info);
            else
                scn = AssimpImporter.LoadAssimp(info);

#if !(EXP_MDL)
            var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

            outPath = Path.GetFullPath(outPath);

            var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

            scn.Write(outStream);

            outStream.Close();


            var startInfo =
                new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\FeatureTesting\\bin\\Debug\\FeatureTesting.exe\"");

            startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\FeatureTesting";
            //startInfo.ArgumentList.Add("Model");
            startInfo.ArgumentList.Add($"{outPath}");

            var proc = Process.Start(startInfo);
#else
            scn.Export(info.Dest + Path.GetFileNameWithoutExtension(info.Source), "glb");
#endif
        }
    }
}