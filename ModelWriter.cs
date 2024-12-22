namespace tmdl_utility;

public partial class ModelUtility
{
    public class ModelWriter : BinaryWriter
    {
        public ModelWriter(FileStream stream) : base(stream)
        {
        }

        public ModelWriter(MemoryStream stream) : base(stream)
        {
        }

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
}