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