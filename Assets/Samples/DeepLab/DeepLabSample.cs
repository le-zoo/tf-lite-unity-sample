﻿using TextureSource;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Samples.DeepLab
{
    [RequireComponent(typeof(VirtualTextureSource))]
    public class DeepLabSample : MonoBehaviour
    {
        [SerializeField]
        private RawImage cameraView = null;

        [SerializeField]
        private RawImage outputView = null;

        [SerializeField]
        private DeepLab.Options options = default;

        private DeepLab deepLab;

        private void Start()
        {
            deepLab = new DeepLab(options);
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
            deepLab?.Dispose();
        }

        private void OnTextureUpdate(Texture texture)
        {
            deepLab.Invoke(texture);
            cameraView.material = deepLab.transformMat;
            outputView.texture = deepLab.GetResultTexture();
        }
    }
}
