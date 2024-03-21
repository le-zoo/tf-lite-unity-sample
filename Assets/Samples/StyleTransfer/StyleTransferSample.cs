﻿using TensorFlowLite;
using TextureSource;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Samples.StyleTransfer
{
    [RequireComponent(typeof(VirtualTextureSource))]
    public class StyleTransferSample : MonoBehaviour
    {
        [SerializeField, FilePopup("*.tflite")]
        private string predictionFileName = "style_predict_quantized_256.tflite";

        [SerializeField, FilePopup("*.tflite")]
        private string transferFileName = "style_transfer_quantized_dynamic.tflite";

        [SerializeField]
        private Texture2D styleImage = null;

        [SerializeField]
        private RawImage preview = null;

        [SerializeField]
        private ComputeShader compute = null;

        private StyleTransfer styleTransfer;
        private float[] styleBottleneck;

        private void Start()
        {
            // Predict style bottleneck;
            using (var predict = new StylePredict(predictionFileName))
            {
                predict.Invoke(styleImage);
                styleBottleneck = predict.GetStyleBottleneck();
            }

            styleTransfer = new StyleTransfer(transferFileName, styleBottleneck, compute);

            if (TryGetComponent(out VirtualTextureSource source))
            {
                source.OnTexture.AddListener(OnTextureUpdate);
            }
        }

        private void OnDestroy()
        {
            if (TryGetComponent(out VirtualTextureSource source))
            {
                source.OnTexture.RemoveListener(OnTextureUpdate);
            }
            styleTransfer?.Dispose();
        }

        private void OnTextureUpdate(Texture texture)
        {
            styleTransfer.Invoke(texture);
            preview.texture = styleTransfer.GetResultTexture();
        }
    }
}
