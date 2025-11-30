using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature that applies the thermal vision post-processing effect.
/// Add this to your URP Renderer (PC_Renderer) in the Renderer Features list.
/// Only affects the main camera - PiP cameras are excluded.
/// </summary>
public class ThermalVisionFeature : ScriptableRendererFeature
{
    class ThermalVisionPass : ScriptableRenderPass
    {
        private Material material;

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public ThermalVisionPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        private bool ShouldSkipCamera(Camera cam)
        {
            // Skip if no camera
            if (cam == null) return true;

            // Skip if rendering to a RenderTexture (PiP cameras, reflection probes, etc.)
            if (cam.targetTexture != null) return true;

            // Skip if not the main camera
            if (cam != Camera.main) return true;

            // Skip overlay cameras
            var additionalData = cam.GetUniversalAdditionalCameraData();
            if (additionalData != null && additionalData.renderType == CameraRenderType.Overlay)
                return true;

            return false;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Skip if controller not active or effect disabled
            if (ThermalVisionController.Instance == null || !ThermalVisionController.Instance.IsEnabled)
                return;

            material = ThermalVisionController.Instance.ThermalMaterial;
            if (material == null)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();

            // Skip non-main cameras
            if (ShouldSkipCamera(cameraData.camera))
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var source = resourceData.activeColorTexture;

            // Create temp texture
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "_ThermalTemp";
            desc.clearBuffer = false;
            var tempTexture = renderGraph.CreateTexture(desc);

            // Pass 1: Source -> Temp (with thermal effect)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Thermal Vision", out var passData))
            {
                passData.source = source;
                passData.material = material;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Pass 2: Temp -> Source (copy back)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Thermal Vision CopyBack", out var passData))
            {
                passData.source = tempTexture;
                passData.material = null;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Legacy fallback - shouldn't be called with RenderGraph enabled
        }
    }

    private ThermalVisionPass pass;

    public override void Create()
    {
        pass = new ThermalVisionPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ThermalVisionController.Instance != null)
        {
            renderer.EnqueuePass(pass);
        }
    }
}
