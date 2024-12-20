﻿using BfresLibrary.Swizzling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DirectXTexNet;

namespace tmdl_utility
{
    public class TextureUtility
    {
        //DXGI formats are first, then ASTC formats after
        public enum TexFormat : uint
        {
            UNKNOWN = 0,
            RGBA32_TYPELESS = 1,
            RGBA32_FLOAT = 2,
            RGBA32_UINT = 3,
            RGBA32_SINT = 4,
            RGB32_TYPELESS = 5,
            RGB32_FLOAT = 6,
            RGB32_UINT = 7,
            RGB32_SINT = 8,
            RGBA16_TYPELESS = 9,
            RGBA16_FLOAT = 10,
            RGBA16_UNORM = 11,
            RGBA16_UINT = 12,
            RGBA16_SNORM = 13,
            RGBA16_SINT = 14,
            RG32_TYPELESS = 15,
            RG32_FLOAT = 16,
            RG32_UINT = 17,
            RG32_SINT = 18,
            R32G8X24_TYPELESS = 19,
            D32_FLOAT_S8X24_UINT = 20,
            R32_FLOAT_X8X24_TYPELESS = 21,
            X32_TYPELESS_G8X24_UINT = 22,
            RGB10A2_TYPELESS = 23,
            RGBB10A2_UNORM = 24,
            RGB10A2_UINT = 25,
            RG11B10_FLOAT = 26,
            RGBA8_TYPELESS = 27,
            RGBA8_UNORM = 28,
            RGBA8_SRGB = 29,
            RGBA8_UINT = 30,
            RGBA8_SNORM = 31,
            RGBA8_SINT = 32,
            RG16_TYPELESS = 33,
            RG16_FLOAT = 34,
            RG16_UNORM = 35,
            RG16_UINT = 36,
            RG16_SNORM = 37,
            RG16_SINT = 38,
            R32_TYPELESS = 39,
            D32_FLOAT = 40,
            R32_FLOAT = 41,
            R32_UINT = 42,
            R32_SINT = 43,
            R24G8_TYPELESS = 44,
            D24_UNORM_S8_UINT = 45,
            R24_UNORM_X8_TYPELESS = 46,
            X24_TYPELESS_G8_UINT = 47,
            RG8_TYPELESS = 48,
            RG8_UNORM = 49,
            RG8_UINT = 50,
            RG8_SNORM = 51,
            RG8_SINT = 52,
            R16_TYPELESS = 53,
            R16_FLOAT = 54,
            D16_UNORM = 55,
            R16_UNORM = 56,
            R16_UINT = 57,
            R16_SNORM = 58,
            R16_SINT = 59,
            R8_TYPELESS = 60,
            R8_UNORM = 61,
            R8_UINT = 62,
            R8_SNORM = 63,
            R8_SINT = 64,
            A8_UNORM = 65,
            R1_UNORM = 66,
            RGB9E5_SHAREDEXP = 67,
            RGBG8_UNORM = 68,
            GRGB8_UNORM = 69,
            BC1_TYPELESS = 70,
            BC1_UNORM = 71,
            BC1_SRGB = 72,
            BC2_TYPELESS = 73,
            BC2_UNORM = 74,
            BC2_SRGB = 75,
            BC3_TYPELESS = 76,
            BC3_UNORM = 77,
            BC3_SRGB = 78,
            BC4_TYPELESS = 79,
            BC4_UNORM = 80,
            BC4_SNORM = 81,
            BC5_TYPELESS = 82,
            BC5_UNORM = 83,
            BC5_SNORM = 84,
            BGR565_UNORM = 85,
            BGR5A1_UNORM = 86,
            BGRA8_UNORM = 87,
            BGRX8_UNORM = 88,
            RGB10_XR_BIAS_A2_UNORM = 89,
            BGRA8_TYPELESS = 90,
            BGRA8_SRGB = 91,
            BGRX8_TYPELESS = 92,
            BGRX8_UNORM_SRGB = 93,
            BC6H_TYPELESS = 94,
            BC6H_UF16 = 95,
            BC6H_SF16 = 96,
            BC7_TYPELESS = 97,
            BC7_UNORM = 98,
            BC7_SRGB = 99,
            AYUV = 100,
            Y410 = 101,
            Y416 = 102,
            NV12 = 103,
            P010 = 104,
            P016 = 105,
            Format_420_OPAQUE = 106,
            YUY2 = 107,
            Y210 = 108,
            Y216 = 109,
            NV11 = 110,
            AI44 = 111,
            IA44 = 112,
            P8 = 113,
            A8P8 = 114,
            BGRA4_UNORM = 115,
            P208 = 130,
            V208 = 131,
            V408 = 132,
            R32G8X24_FLOAT = 133,
            ASTC_4x4_UNORM = 134,
            ASTC_4x4_SRGB = 135,
            ASTC_5x4_UNORM = 138,
            ASTC_5x4_SRGB = 139,
            ASTC_5x5_UNORM = 142,
            ASTC_5x5_SRGB = 143,
            ASTC_6x5_UNORM = 146,
            ASTC_6x5_SRGB = 147,
            ASTC_6x6_UNORM = 150,
            ASTC_6x6_SRGB = 151,
            ASTC_8x5_UNORM = 154,
            ASTC_8x5_SRGB = 155,
            ASTC_8x6_UNORM = 158,
            ASTC_8x6_SRGB = 159,
            ASTC_8x8_UNORM = 162,
            ASTC_8x8_SRGB = 163,
            ASTC_10x5_UNORM = 166,
            ASTC_10x5_SRGB = 167,
            ASTC_10x6_UNORM = 170,
            ASTC_10x6_SRGB = 171,
            ASTC_10x8_UNORM = 174,
            ASTC_10x8_SRGB = 175,
            ASTC_10x10_UNORM = 178,
            ASTC_10x10_SRGB = 179,
            ASTC_12x10_UNORM = 182,
            ASTC_12x10_SRGB = 183,
            ASTC_12x12_UNORM = 186,
            ASTC_12x12_SRGB = 187,

            ETC1_SRGB = 229,
            ETC1_UNORM = 230,
            ETC1_A4 = 231,
            L4 = 232,
            LA4 = 233,
            L8 = 234,
            LA8 = 235,
            HIL08 = 236,
            A4 = 237,
            RG4_UNORM = 238,

            I4 = 239,
            I8 = 240,
            IA4 = 241,
            IA8 = 242,
            R5G5B5A3_UNORM = 244,
            RGBA32 = 245,
            C4 = 246,
            C8 = 247,
            C14X2 = 248,
            CMPR = 249,
            RGB565_UNORM = 250,
            RGB5A3_UNORM = 251,
            RGB5A1_UNORM = 252,
            RGB5_UNORM = 253,
            RGBA4_UNORM,
            RGB8_UNORM,
            RGB8_SRGB,
        }

        public static unsafe byte[] Convert(Byte[] data, int width, int height, DXGI_FORMAT inputFormat, DXGI_FORMAT outputFormat)
        {
            long inputRowPitch;
            long inputSlicePitch;
            TexHelper.Instance.ComputePitch(inputFormat, width, height, out inputRowPitch, out inputSlicePitch, CP_FLAGS.NONE);

            if (data.Length == inputSlicePitch)
            {
                byte* buf;
                buf = (byte*)Marshal.AllocHGlobal((int)inputSlicePitch);
                Marshal.Copy(data, 0, (IntPtr)buf, (int)inputSlicePitch);

                DirectXTexNet.Image inputImage = new DirectXTexNet.Image(
                    width, height, inputFormat, inputRowPitch,
                    inputSlicePitch, (IntPtr)buf, null);

                TexMetadata texMetadata = new TexMetadata(width, height, 1, 1, 1, 0, 0,
                    inputFormat, TEX_DIMENSION.TEXTURE2D);

                ScratchImage scratchImage = TexHelper.Instance.InitializeTemporary(
                    new DirectXTexNet.Image[] { inputImage }, texMetadata, null);

                var convFlags = TEX_FILTER_FLAGS.DEFAULT;

                if (inputFormat == DXGI_FORMAT.B8G8R8A8_UNORM_SRGB ||
                 inputFormat == DXGI_FORMAT.B8G8R8X8_UNORM_SRGB ||
                 inputFormat == DXGI_FORMAT.R8G8B8A8_UNORM_SRGB)
                {
                    convFlags |= TEX_FILTER_FLAGS.SRGB;
                }

                using (var decomp = scratchImage.Convert(0, outputFormat, convFlags, 0.5f))
                {
                    long outRowPitch;
                    long outSlicePitch;
                    TexHelper.Instance.ComputePitch(outputFormat, width, height, out outRowPitch, out outSlicePitch, CP_FLAGS.NONE);

                    byte[] result = new byte[outSlicePitch];
                    Marshal.Copy(decomp.GetImage(0).Pixels, result, 0, result.Length);

                    inputImage = null;
                    scratchImage.Dispose();


                    return result;
                }
            }
            return null;
        }

        public static unsafe byte[] DecodePixelBlock(Byte[] data, int width, int height, DXGI_FORMAT format, float AlphaRef = 0.5f)
        {
            if (format == DXGI_FORMAT.R8G8B8A8_UNORM)
            {
                byte[] result = new byte[data.Length];
                Array.Copy(data, result, data.Length);
                return result;
            }

            return Convert(data, width, height, (DXGI_FORMAT)format, DXGI_FORMAT.R8G8B8A8_UNORM);
        }
        public static unsafe byte[] DecompressBlock(Byte[] data, int width, int height, DXGI_FORMAT format)
        {
            long inputRowPitch;
            long inputSlicePitch;
            TexHelper.Instance.ComputePitch((DXGI_FORMAT)format, width, height, out inputRowPitch, out inputSlicePitch, CP_FLAGS.NONE);

            DXGI_FORMAT FormatDecompressed;

            if (format.ToString().Contains("SRGB"))
                FormatDecompressed = DXGI_FORMAT.R8G8B8A8_UNORM_SRGB;
            else
                FormatDecompressed = DXGI_FORMAT.R8G8B8A8_UNORM;

            byte* buf;
            buf = (byte*)Marshal.AllocHGlobal((int)inputSlicePitch);
            Marshal.Copy(data, 0, (IntPtr)buf, (int)inputSlicePitch);

            DirectXTexNet.Image inputImage = new DirectXTexNet.Image(
                width, height, (DXGI_FORMAT)format, inputRowPitch,
                inputSlicePitch, (IntPtr)buf, null);

            TexMetadata texMetadata = new TexMetadata(width, height, 1, 1, 1, 0, 0,
                (DXGI_FORMAT)format, TEX_DIMENSION.TEXTURE2D);

            ScratchImage scratchImage = TexHelper.Instance.InitializeTemporary(
                new DirectXTexNet.Image[] { inputImage }, texMetadata, null);

            using (var decomp = scratchImage.Decompress(0, FormatDecompressed))
            {
                byte[] result = new byte[4 * width * height];
                Marshal.Copy(decomp.GetImage(0).Pixels, result, 0, result.Length);

                inputImage = null;
                scratchImage.Dispose();

                return result;
            }
        }

        public static bool Decode(TexFormat format, byte[] input, int width, int height, out byte[] output)
        {
            if (format.ToString().StartsWith("BC"))
                output = DecompressBlock(input, width, height, (DXGI_FORMAT)format);
            else
                output = DecodePixelBlock(input, width, height, (DXGI_FORMAT)format);

            return output != null;
        }

    }
}
