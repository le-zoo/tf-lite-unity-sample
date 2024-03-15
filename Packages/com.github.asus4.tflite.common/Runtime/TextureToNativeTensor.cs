using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using DataType = TensorFlowLite.Interpreter.DataType;

namespace TensorFlowLite
{
    /// <summary>
    /// Converts Texture to Tensor with arbitrary matrix transformation
    /// then return it as a NativeArray<byte> (NHWC layout)
    /// </summary>
    public abstract class TextureToNativeTensor : IDisposable
    {
        [Serializable]
        public class Options
        {
            public ComputeShader compute = null;
            public int kernel = 0;
            public int width = 0;
            public int height = 0;
            public int channels = 0;
            public DataType inputType = DataType.Float32;
        }

        protected static readonly Lazy<ComputeShader> DefaultComputeShaderFloat32 = new(()
            => Resources.Load<ComputeShader>("com.github.asus4.tflite.common/TextureToNativeTensorFloat32"));

        private static readonly int _InputTex = Shader.PropertyToID("_InputTex");
        private static readonly int _OutputTex = Shader.PropertyToID("_OutputTex");
        private static readonly int _OutputTensor = Shader.PropertyToID("_OutputTensor");
        private static readonly int _OutputSize = Shader.PropertyToID("_OutputSize");
        private static readonly int _TransformMatrix = Shader.PropertyToID("_TransformMatrix");

        private static readonly Matrix4x4 PopMatrix = Matrix4x4.Translate(new Vector3(0.5f, 0.5f, 0));
        private static readonly Matrix4x4 PushMatrix = Matrix4x4.Translate(new Vector3(-0.5f, -0.5f, 0));

        private readonly ComputeShader compute;
        private readonly int kernel;
        private readonly int width;
        private readonly int height;
        private readonly int channels;

        private readonly RenderTexture texture;
        private readonly GraphicsBuffer tensorBuffer;
        protected NativeArray<byte> tensor;

        public RenderTexture Texture => texture;
        public Matrix4x4 TransformMatrix { get; private set; } = Matrix4x4.identity;

        protected TextureToNativeTensor(int stride, Options options)
        {
            compute = options.compute != null
                ? options.compute
                : DefaultComputeShaderFloat32.Value;
            kernel = options.kernel;
            width = options.width;
            height = options.height;
            channels = options.channels;

            Assert.IsTrue(kernel >= 0, $"Kernel must be set");
            Assert.IsTrue(width > 0, $"Width must be greater than 0");
            Assert.IsTrue(height > 0, $"Height must be greater than 0");
            Assert.IsTrue(channels > 0 && channels <= 4, $"Channels must be 1 to 4");

            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                useMipMap = false,
                depthBufferBits = 0,
            };
            texture = new RenderTexture(desc);
            texture.Create();

            int length = width * height * channels;
            tensorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, length, stride);
            tensor = new NativeArray<byte>(length * stride, Allocator.Persistent);

            // Set constant values
            compute.SetInts(_OutputSize, width, height);
            compute.SetBuffer(kernel, _OutputTensor, tensorBuffer);
            compute.SetTexture(kernel, _OutputTex, texture, 0);
        }

        public virtual void Dispose()
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            tensorBuffer.Dispose();
        }

        public virtual NativeArray<byte> Transform(Texture input, in Matrix4x4 t)
        {
            TransformMatrix = t;
            compute.SetTexture(kernel, _InputTex, input, 0);
            compute.SetMatrix(_TransformMatrix, t);
            compute.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

            // TODO: Implement async version
            var request = AsyncGPUReadback.RequestIntoNativeArray(ref tensor, tensorBuffer, (request) =>
            {
                if (request.hasError)
                {
                    Debug.LogError("GPU readback error detected.");
                    return;
                }
            });
            request.WaitForCompletion();
            return tensor;
        }

        public NativeArray<byte> Transform(Texture input, AspectMode aspectMode)
        {
            return Transform(input, GetAspectScaledMatrix(input, aspectMode));
        }

        public Matrix4x4 GetAspectScaledMatrix(Texture input, AspectMode aspectMode)
        {
            float srcAspect = (float)input.width / input.height;
            float dstAspect = (float)width / height;
            Vector2 scale = GetAspectScale(srcAspect, dstAspect, aspectMode);
            return PopMatrix * Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1)) * PushMatrix;
        }

        public static Vector2 GetAspectScale(float srcAspect, float dstAspect, AspectMode mode)
        {
            bool isSrcWider = srcAspect > dstAspect;
            return (mode, isSrcWider) switch
            {
                (AspectMode.None, _) => new Vector2(1, 1),
                (AspectMode.Fit, true) => new Vector2(1, srcAspect / dstAspect),
                (AspectMode.Fit, false) => new Vector2(dstAspect / srcAspect, 1),
                (AspectMode.Fill, true) => new Vector2(dstAspect / srcAspect, 1),
                (AspectMode.Fill, false) => new Vector2(1, srcAspect / dstAspect),
                _ => throw new Exception("Unknown aspect mode"),
            };
        }

        public static TextureToNativeTensor Create(Options options)
        {
            return options.inputType switch
            {
                DataType.Float32 => new TextureToNativeTensorFloat32(options),
                DataType.UInt8 => new TextureToNativeTensorUInt8(options),
                _ => throw new NotImplementedException(
                    $"input type {options.inputType} is not implemented yet. Create our own TextureToNativeTensor class and override it."),
            };
        }
    }

    /// <summary>
    /// For Float32
    /// </summary>
    public sealed class TextureToNativeTensorFloat32 : TextureToNativeTensor
    {
        public TextureToNativeTensorFloat32(Options options)
            : base(UnsafeUtility.SizeOf<float>(), options)
        { }
    }

    /// <summary>
    /// For UInt8
    /// 
    /// Note:
    /// Run compute shader with Float32 then convert to UInt8(byte) in C#
    /// Because ComputeBuffer doesn't support UInt8 type
    /// </summary>
    public sealed class TextureToNativeTensorUInt8 : TextureToNativeTensor
    {
        private NativeArray<byte> tensorInt8;

        public TextureToNativeTensorUInt8(Options options)
            : base(UnsafeUtility.SizeOf<uint>(), options)
        {
            int length = options.width * options.height * options.channels;
            tensorInt8 = new NativeArray<byte>(length, Allocator.Persistent);
        }

        public override void Dispose()
        {
            base.Dispose();
            tensorInt8.Dispose();
        }

        public override NativeArray<byte> Transform(Texture input, in Matrix4x4 t)
        {
            NativeArray<byte> tensor = base.Transform(input, t);
            // Reinterpret (byte * 4) as float
            NativeSlice<float> tensorF32 = tensor.Slice().SliceConvert<float>();

            // TODO: implement in Burst
            for (int i = 0; i < tensorInt8.Length; i++)
            {
                float n = tensorF32[i] * 255f;
                tensorInt8[i] = (byte)n;
            }
            return tensorInt8;
        }
    }
}
