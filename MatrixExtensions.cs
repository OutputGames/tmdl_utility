using System.Numerics;
using Syroot.Maths;
using Vector3 = System.Numerics.Vector3;

public static class MatrixExtensions
{
    public static void DecomposeMatrix(this Matrix4x4 matrix, out Vector3 translation, out Quaternion rotation,
        out Vector3 scale)
    {
        // Extract translation
        translation = new Vector3(matrix.M41, matrix.M42, matrix.M43);

        // Extract scale
        scale = new Vector3(
            new Vector3(matrix.M11, matrix.M12, matrix.M13).Length(),
            new Vector3(matrix.M21, matrix.M22, matrix.M23).Length(),
            new Vector3(matrix.M31, matrix.M32, matrix.M33).Length()
        );

        // Normalize the matrix to remove the scale from the rotation
        var normalizedMatrix = new Matrix4x4(
            matrix.M11 / scale.X, matrix.M12 / scale.X, matrix.M13 / scale.X, 0.0f,
            matrix.M21 / scale.Y, matrix.M22 / scale.Y, matrix.M23 / scale.Y, 0.0f,
            matrix.M31 / scale.Z, matrix.M32 / scale.Z, matrix.M33 / scale.Z, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

        // Extract rotation
        rotation = Quaternion.CreateFromRotationMatrix(normalizedMatrix);
    }

    public static Matrix4x4 ConvertMatrix3x4(this Matrix3x4 matrix)
    {
        return new Matrix4x4(
            matrix.M11, matrix.M12, matrix.M13, 0,
            matrix.M12, matrix.M22, matrix.M23, 0,
            matrix.M13, matrix.M32, matrix.M33, 0,
            matrix.M14, matrix.M24, matrix.M34, 1);
    }

    public class Matrices
    {
        public Matrix4x4 inverse = Matrix4x4.Identity;
        public Matrix4x4 transform = Matrix4x4.Identity;
    }
}