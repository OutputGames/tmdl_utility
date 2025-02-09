using System.Numerics;
using BfresLibrary;
using Syroot.Maths;
using tmdl_utility;
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

    public static void AI_DecomposeMatrix(this Matrix4x4 matrix, out Vector3 translation, out Quaternion rotation,
        out Vector3 scale)
    {
        // Extract translation
        translation = new Vector3(matrix.M14, matrix.M24, matrix.M34);

        // Extract scale
        scale = new Vector3(
            new Vector3(matrix.M11, matrix.M21, matrix.M31).Length(),
            new Vector3(matrix.M12, matrix.M22, matrix.M32).Length(),
            new Vector3(matrix.M13, matrix.M23, matrix.M33).Length()
        );

        // Normalize the matrix to remove the scale from the rotation
        var normalizedMatrix = new Matrix4x4(
            matrix.M11 / scale.X, matrix.M21 / scale.X, matrix.M31 / scale.X, 0.0f,
            matrix.M12 / scale.Y, matrix.M22 / scale.Y, matrix.M23 / scale.Y, 0.0f,
            matrix.M13 / scale.Z, matrix.M23 / scale.Z, matrix.M33 / scale.Z, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

        // Extract rotation
        rotation = Quaternion.CreateFromRotationMatrix(normalizedMatrix);
    }

    public static Matrix4x4 ConvertMatrix3x4(this Matrix3x4 mat)
    {
        return new Matrix4x4
        {
            M11 = mat.M11,
            M21 = mat.M12,
            M31 = mat.M13,
            M41 = mat.M14,
            M12 = mat.M21,
            M22 = mat.M22,
            M32 = mat.M23,
            M42 = mat.M24,
            M13 = mat.M31,
            M23 = mat.M32,
            M33 = mat.M33,
            M43 = mat.M34,
            M14 = 0,
            M24 = 0,
            M34 = 0,
            M44 = 0
        };
    }

    public static Matrix4x4 CalculateTransformMatrix(Bone bone)
    {
        var trans = Matrix4x4.CreateTranslation(new Vector3(bone.Position.X, bone.Position.Y, bone.Position.Z));
        var scale = Matrix4x4.CreateScale(new Vector3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z));

        var quat = Matrix4x4.Identity;

        var rot = new ModelUtility.Vec4(bone.Rotation);
        var eul = rot.ToEuler();

        if (bone.FlagsRotation == BoneFlagsRotation.EulerXYZ)
            quat = Matrix4x4.CreateFromQuaternion(
                ModelUtility.Vec4.FromEuler(eul));
        else
            quat = Matrix4x4.CreateFromQuaternion(new ModelUtility.Vec4(bone.Rotation.X, bone.Rotation.Y,
                bone.Rotation.Z, bone.Rotation.W));

        quat = Matrix4x4.CreateFromQuaternion(new ModelUtility.Vec4(bone.Rotation.X, bone.Rotation.Y,
            bone.Rotation.Z, bone.Rotation.W));

        return scale * quat * trans;
    }

    public static Matrix3x4 ConverMatrix3X4(this Matrix4x4 matrix)
    {
        return new Matrix3x4(matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23,
            matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34);
    }

    public static Matrices CalculateInverseMatrix(Bone bone, Skeleton skeleton)
    {
        var matrices = new Matrices();

        //Get parent transform for a smooth matrix
        if (bone.ParentIndex != -1)
        {
            var parent = skeleton.Bones[bone.ParentIndex];
            matrices.transform *= CalculateInverseMatrix(parent, skeleton).transform;
        }
        else
        {
            matrices.transform = Matrix4x4.Identity;
        }

        matrices.transform *= CalculateTransformMatrix(bone);

        Matrix4x4 Inverse;
        Matrix4x4.Invert(matrices.transform, out Inverse);

        matrices.inverse = Inverse;

        return matrices;
    }

    public class Matrices
    {
        public Matrix4x4 inverse = Matrix4x4.Identity;
        public Matrix4x4 transform = Matrix4x4.Identity;
    }
}