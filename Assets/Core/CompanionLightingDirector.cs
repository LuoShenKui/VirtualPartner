using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 统一整理卧室内的灯光氛围，让室内更适合陪伴互动。
    /// </summary>
    [ExecuteAlways]
    public class CompanionLightingDirector : MonoBehaviour
    {
        private void OnEnable()
        {
            ApplyLighting();
        }

        private void OnValidate()
        {
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            foreach (var light in FindObjectsByType<Light>())
            {
                if (light == null || light.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (light.type == LightType.Directional)
                {
                    light.intensity = 0.42f;
                    light.color = new Color(1f, 0.93f, 0.84f, 1f);
                    light.shadows = LightShadows.Soft;
                    continue;
                }

                if (light.type == LightType.Point)
                {
                    var lowerName = light.name.ToLowerInvariant();
                    if (lowerName.Contains("(1)") || lowerName.Contains("(2)"))
                    {
                        light.intensity = 0.65f;
                        light.range = 6.5f;
                        light.color = new Color(1f, 0.78f, 0.58f, 1f);
                    }
                    else
                    {
                        light.intensity = 0.42f;
                        light.range = 4.8f;
                        light.color = new Color(1f, 0.72f, 0.52f, 1f);
                    }
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.43f, 0.38f, 0.34f, 1f);
            RenderSettings.reflectionIntensity = 0.7f;
        }
    }
}
