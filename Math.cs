using System.Numerics;

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
        }

        public Vec4(System.Numerics.Vector4 v)
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

        public Vec4(System.Numerics.Quaternion v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public Vec4(Syroot.Maths.Vector4F v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public static Vec4 operator *(Vec4 value1, Quaternion value2)
        {
            Vec4 ans = new Vec4();

            float q1x = value1.X;
            float q1y = value1.Y;
            float q1z = value1.Z;
            float q1w = value1.W;

            float q2x = value2.X;
            float q2y = value2.Y;
            float q2z = value2.Z;
            float q2w = value2.W;

            // cross(av, bv)
            float cx = q1y * q2z - q1z * q2y;
            float cy = q1z * q2x - q1x * q2z;
            float cz = q1x * q2y - q1y * q2x;

            float dot = q1x * q2x + q1y * q2y + q1z * q2z;

            ans.X = q1x * q2w + q2x * q1w + cx;
            ans.Y = q1y * q2w + q2y * q1w + cy;
            ans.Z = q1z * q2w + q2z * q1w + cz;
            ans.W = q1w * q2w - dot;

            return ans;
        }

        public static implicit operator Quaternion(Vec4 d) => new Quaternion(d.X, d.Y, d.Z, d.W);
        public static implicit operator Vec4(Quaternion d) => new Vec4(d.X, d.Y, d.Z, d.W);

        public Vec4(Vec3 euler)
        {
            var yaw = euler.Y;
            var pitch = euler.X;
            var roll = euler.Z; 

            double cy = Math.Cos(yaw * 0.5);
            double sy = Math.Sin(yaw * 0.5);
            double cp = Math.Cos(pitch * 0.5);
            double sp = Math.Sin(pitch * 0.5);
            double cr = Math.Cos(roll * 0.5);
            double sr = Math.Sin(roll * 0.5);

            W = (float)(cr * cp * cy + sr * sp * sy);
            X = (float)(sr * cp * cy - cr * sp * sy);
            Y = (float)(cr * sp * cy + sr * cp * sy);
            Z = (float)(cr * cp * sy - sr * sp * cy);
        }


        public static Vec4 operator +(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.W+v2.W);
        }
        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return this.X;
                    case 1:
                        return this.Y;
                    case 2:
                        return this.Z;
                    case 3:
                        return this.W;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index), string.Format("Index must be between 0 and {0}.", (object)4));
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        this.X = value;
                        break;
                    case 1:
                        this.Y = value;
                        break;
                    case 2:
                        this.Z = value;
                        break;
                    case 3:
                        this.W = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index), string.Format("Index must be between 0 and {0}.", (object)4));
                }
            }
        }


        public Vec3 ToVec3()
        {
            return new Vec3(X, Y, Z);
        }

        public Vec2 ToVec2()
        {
            return new Vec2(X, Y);
        }

        public float Magnitude
        {
            get { return MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2) + MathF.Pow(W, 2)); }
        }

        public Vec4 Normalized
        {
            get
            {
                var mag = this.Magnitude;

                if (mag < 0.0001f)
                {
                    return this;
                }

                return new Vec4(X / mag, Y / mag, Z / mag, W / mag);
            }
        }

        public void Write(ModelWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(W);
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

        public Vec3(System.Numerics.Vector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public Vec3(Syroot.Maths.Vector3F v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public static implicit operator Vector3(Vec3 d) => new Vector3(d.X,d.Y,d.Z);
        public static implicit operator Vec3(Vector3 d) => new Vec3(d.X, d.Y, d.Z);

        public void Write(ModelWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }


        public float Magnitude
        {
            get { return MathF.Sqrt(MathF.Pow(X, 2) + MathF.Pow(Y, 2) + MathF.Pow(Z, 2)); }
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
    }

    public class Vec2
    {
        public float X, Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public class Key<T>
    {
        public float timeStamp;

        public T value;

        public float Time
        {
            get { return timeStamp; }
            set { timeStamp = value; }
        }

        public Key(float time, T val)
        {
            this.timeStamp = time;
            this.value = val;
        }
    }
}