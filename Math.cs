using Syroot.Maths;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace tmdl_utility;

public partial class ModelUtility
{
    public class Vec4
    {
        public float X, Y, Z, W;

        public Vec4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Vec4()
        {
            W = 1;
        }

        public Vec4(Vector4 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public Vec4(float s)
        {
            X = s;
            Y = s;
            Z = s;
            W = 1.0f;
        }

        public Vec4(Quaternion v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public Vec4(Vector4F v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return X;
                    case 1:
                        return Y;
                    case 2:
                        return Z;
                    case 3:
                        return W;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index),
                            string.Format("Index must be between 0 and {0}.", 4));
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                    case 3:
                        W = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index),
                            string.Format("Index must be between 0 and {0}.", 4));
                }
            }
        }

        public float Magnitude => MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2) + MathF.Pow(W, 2));

        public Vec4 Normalized
        {
            get
            {
                var mag = Magnitude;

                if (mag < 0.0001f) return this;

                return new Vec4(X / mag, Y / mag, Z / mag, W / mag);
            }
        }

        public static Vec4 FromEuler(Vec3 euler)
        {
            var xRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, euler.X);
            var yRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, euler.Y);
            var zRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, euler.Z);

            var q = zRotation * yRotation * xRotation;

            if (q.W < 0)
                q *= -1;

            //return xRotation * yRotation * zRotation;
            return new Vec4(q);
        }

        public static Vec4 operator *(Vec4 value1, Quaternion value2)
        {
            var ans = new Vec4();

            var q1x = value1.X;
            var q1y = value1.Y;
            var q1z = value1.Z;
            var q1w = value1.W;

            var q2x = value2.X;
            var q2y = value2.Y;
            var q2z = value2.Z;
            var q2w = value2.W;

            // cross(av, bv)
            var cx = q1y * q2z - q1z * q2y;
            var cy = q1z * q2x - q1x * q2z;
            var cz = q1x * q2y - q1y * q2x;

            var dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static implicit operator Quaternion(Vec4 d)
        {
            return new Quaternion(d.X, d.Y, d.Z, d.W);
        }

        public static implicit operator Vec4(Quaternion d)
        {
            return new Vec4(d.X, d.Y, d.Z, d.W);
        }


        public static Vec4 operator +(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.W + v2.W);
        }


        public Vec3 ToVec3()
        {
            return new Vec3(X, Y, Z);
        }

        public Vec2 ToVec2()
        {
            return new Vec2(X, Y);
        }

        public void Write(ModelWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(W);
        }

        public Vec3 ToEuler()
        {
            var mat = Matrix4x4.CreateFromQuaternion(this);
            float x, y, z;
            y = (float)Math.Asin(double.Clamp(mat.M13, -1, 1));

            if (Math.Abs(mat.M13) < 0.99999)
            {
                x = (float)Math.Atan2(-mat.M23, mat.M33);
                z = (float)Math.Atan2(-mat.M12, mat.M11);
            }
            else
            {
                x = (float)Math.Atan2(mat.M32, mat.M22);
                z = 0;
            }

            return new Vector3(x, y, z) * -1;
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z},{W})";
        }
    }

    public class Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3(float s)
        {
            X = s;
            Y = s;
            Z = s;
        }

        public Vec3() : this(0)
        {
        }

        public Vec3(Vector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public Vec3(Vector3F v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }


        public float Magnitude => MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2));

        public static implicit operator Vector3(Vec3 d)
        {
            return new Vector3(d.X, d.Y, d.Z);
        }

        public static implicit operator Aspose.ThreeD.Utilities.Vector4(Vec3 d)
        {
            return new Aspose.ThreeD.Utilities.Vector4(d.X, d.Y, d.Z, 0);
        }

        public static implicit operator Vec3(Vector3 d)
        {
            return new Vec3(d.X, d.Y, d.Z);
        }


        public void Write(ModelWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }

        public static Vec3 operator +(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }

        public static Vec3 operator -(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }

        public static Vec3 operator *(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
        }

        public static Vec3 operator *(Vec3 v1, float v2)
        {
            return new Vec3(v1.X * v2, v1.Y * v2, v1.Z * v2);
        }


        public static Vec3 operator /(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.X / v2.X, v1.Y / v2.Y, v1.Z / v2.Z);
        }

        public static Vec3 operator /(Vec3 v1, float v2)
        {
            return new Vec3(v1.X / v2, v1.Y / v2, v1.Z / v2);
        }

        public static Vec3 operator -(Vec3 v1)
        {
            return new Vec3(-v1.X, -v1.Y, -v1.Z);
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }
    }

    public class Vec2
    {
        public float X, Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static implicit operator Vector2(Vec2 d)
        {
            return new Vector2(d.X, d.Y);
        }

        public static implicit operator Vector3(Vec2 d)
        {
            return new Vector3(d.X, d.Y, 0);
        }

        public static implicit operator Vec2(Vector2 d)
        {
            return new Vec2(d.X, d.Y);
        }

        public static implicit operator Aspose.ThreeD.Utilities.Vector4(Vec2 d)
        {
            return new Aspose.ThreeD.Utilities.Vector4(d.X, d.Y, 0, 0);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }
}