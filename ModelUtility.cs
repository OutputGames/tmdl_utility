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

            var export = false;

            if (!export)
            {
                var outPath = info.Dest + Path.GetFileNameWithoutExtension(info.Source) + ".tmdl";

                outPath = Path.GetFullPath(outPath);

                var outStream = new ModelWriter(new FileStream(outPath, FileMode.OpenOrCreate));

                scn.Write(outStream);

                outStream.Close();

                var startInfo =
                    new ProcessStartInfo("\"D:\\Code\\ImportantRepos\\TomatoEditor\\bin\\Debug\\TomatoEditor.exe\"");

                startInfo.WorkingDirectory = "D:\\Code\\ImportantRepos\\TomatoEditor";
                startInfo.ArgumentList.Add($"{outPath}");

                var proc = Process.Start(startInfo);
            }
            else
            {
                scn.Export(info.Dest + Path.GetFileNameWithoutExtension(info.Source), "fbx");
            }
        }
    }
}