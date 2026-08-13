using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThisWillFloat.R70Migration
{
    /// <summary>
    /// Keeps the R70 review scene on its dedicated long-range shadow profile
    /// without permanently replacing the project's normal PC/Mobile URP assets.
    /// </summary>
    [ExecuteAlways]
    public sealed class R70SceneRenderProfile : MonoBehaviour
    {
        public UniversalRenderPipelineAsset pipelineAsset;
        private RenderPipelineAsset previousPipeline;

        private void OnEnable() => ApplyProfile();

        public void ApplyProfile()
        {
            if (pipelineAsset == null) return;
            if (QualitySettings.renderPipeline != pipelineAsset)
                previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = pipelineAsset;
        }

        private void OnDisable()
        {
            if (pipelineAsset != null && QualitySettings.renderPipeline == pipelineAsset)
                QualitySettings.renderPipeline = previousPipeline;
        }
    }
}
