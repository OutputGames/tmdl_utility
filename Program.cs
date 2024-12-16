



namespace tmdl_utility
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var initInfo = new UtilityInitInfo(args);

            var util = new ModelUtility(initInfo);
        }
    }
}
