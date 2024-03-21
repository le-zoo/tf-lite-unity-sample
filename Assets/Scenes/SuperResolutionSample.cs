using TensorFlowLite;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Samples.SuperResolution
{
    public class SuperResolutionSample : MonoBehaviour
    {
        [SerializeField, FilePopup("*.tflite")] string fileName = "";
        [SerializeField] Texture2D inputTex;
        [SerializeField] RawImage outputImage;
        [SerializeField] ComputeShader compute;

        SuperResolution superResolution;

        void Start()
        {
            superResolution = new SuperResolution(fileName, compute);
            superResolution.Invoke(inputTex);

            // Create a new Texture2D to hold the result
            Texture2D resultTex = ToTexture2D(superResolution.GetResult());

            // Here you can replace the inputTex reference with the resultTex
            // Note: This does not 'change' the original texture but rather replaces the reference
            // You might need to update any materials or objects that used the original inputTex
            inputTex = resultTex;

            // Optionally display the result on an outputImage for debugging
            outputImage.texture = resultTex;
        }

        Texture2D ToTexture2D(RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            superResolution?.Dispose();
        }

    }
}
