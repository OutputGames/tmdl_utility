namespace tmdl_utility;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length <= 1)
        {
            Console.WriteLine("Enter a model path.");

            var p = Console.ReadLine();
            args.SetValue(p, 1);
        }

        var initInfo = new UtilityInitInfo(args);

        var util = new ModelUtility(initInfo);
    }
}