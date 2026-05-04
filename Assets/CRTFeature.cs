using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full-screen CRT pass (pixelation/scanlines/warp) for URP.
/// Add this as a Renderer Feature on your UniversalRendererData,
/// assign a Material that uses the "Hidden/CRT_Simple" shader.
/// </summary>
public class CRTFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Material using the Hidden/CRT_Simple shader.")]
        public Material crtMaterial;

        [Tooltip("When to run the pass. AfterRenderingPostProcessing recommended.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [Tooltip("Master switch for the feature at runtime.")]
        public bool isEnabled = true;
    }

    class CRTPass : ScriptableRenderPass
    {
        private readonly string _profilerTag = "CRT Blit";
        private Material _material;

        // Source/Temp/
        private RenderTargetIdentifier _source;
        private RenderTargetHandle _tempTex;

        public CRTPass(Material material, RenderPassEvent evt)
        {
            _material = material;
            renderPassEvent = evt;
            _tempTex.Init("_CRT_TempTex");
        }

        public void Setup(in RenderTargetIdentifier src)
        {
            _source = src;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Match the camera target
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(_tempTex.id, desc, FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            // Skip Scene View if desired (uncomment next two lines)
            // if (renderingData.cameraData.isSceneViewCamera)
            //     return;

            var cmd = CommandBufferPool.Get(_profilerTag);

            // Blit source -> temp with CRT material, then temp -> source
            Blit(cmd, _source, _tempTex.Identifier(), _material, 0);
            Blit(cmd, _tempTex.Identifier(), _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) return;
            cmd.ReleaseTemporaryRT(_tempTex.id);
        }
    }

    public Settings settings = new Settings();

    private CRTPass _pass;

    /// <summary> Toggle from code: PostFXOrchestrator can call this. </summary>
    public void SetEnabled(bool on) => settings.isEnabled = on;

    public override void Create()
    {
        _pass = new CRTPass(settings.crtMaterial, settings.renderPassEvent);
        // Keep the feature object name stable for lookups (optional)
        if (string.IsNullOrEmpty(name)) name = "CRTFeature";
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!settings.isEnabled) return;
        if (settings.crtMaterial == null) return;

        // (Re)sync material if it changed in the inspector
        if (_pass == null || _pass.renderPassEvent != settings.renderPassEvent)
        {
            _pass = new CRTPass(settings.crtMaterial, settings.renderPassEvent);
        }

        // Set up with the current camera color target
        _pass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(_pass);
    }

#if UNITY_EDITOR
    // Ensure changes in the inspector take immediate effect
    private void OnValidate()
    {
        if (_pass != null)
        {
            _pass = new CRTPass(settings.crtMaterial, settings.renderPassEvent);
        }
    }
#endif
}
