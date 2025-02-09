namespace tmdl_utility;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length <= 1)
        {
            args = new string[3];
            args.SetValue("Single", 0);

            Console.WriteLine("Enter a model path.");

            var p = Console.ReadLine();
            args.SetValue(p, 1);

            Console.WriteLine("Enter an out path.");

            var _p = Console.ReadLine();
            args.SetValue(_p, 2);
        }

        var initInfo = new UtilityInitInfo(args);

        var util = new ModelUtility(initInfo);
    }
}