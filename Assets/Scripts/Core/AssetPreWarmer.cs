using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// Generic Addressables pre-warm loader. Runs during splash screen so
    /// MainMenu / Level1 assets (textures, sprites, prefabs) are already
    /// cached before gameplay starts, preventing "pop-in" artifacts.
    /// </summary>
    public class AssetPreWarmer
    {
        /// <summary>
        /// Runs InitializeAsync + DownloadDependenciesAsync for every label
        /// in sequence. Reports progress via onProgress (0..1) after each label.
        /// </summary>
        public static IEnumerator PreWarmAsync(
            List<string> labels,
            Action<float> onProgress)
        {
#if !ENABLE_ADDRESSABLES
            // Compile guard: if Addressables is uninstalled, skip gracefully.
            onProgress?.Invoke(1f);
            yield break;
#endif

#if ENABLE_ADDRESSABLES
            // Initialize the Addressables system once.
            Exception initError = null;
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle initHandle =
                default(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle);
            try
            {
                initHandle = UnityEngine.AddressableAssets.Addressables.InitializeAsync(false);
            }
            catch (Exception e)
            {
                initError = e;
            }

            if (initError != null)
            {
                Debug.LogWarning("[AssetPreWarmer] Addressables.InitializeAsync failed (ignored): " + initError.Message);
                onProgress?.Invoke(1f);
                yield break;
            }

            yield return initHandle;

            float total = labels != null ? labels.Count : 0f;
            if (total <= 0f)
            {
                onProgress?.Invoke(1f);
                yield break;
            }

            float completed = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                float progressBefore = completed / total;

                // Get download size for this label's dependencies.
                Exception sizeError = null;
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<long> sizeHandle =
                    default(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<long>);
                try
                {
                    sizeHandle = UnityEngine.AddressableAssets.Addressables.GetDownloadSizeAsync(label);
                }
                catch (Exception e)
                {
                    sizeError = e;
                }

                if (sizeError == null)
                {
                    yield return sizeHandle;
                }
                else
                {
                    Debug.LogWarning("[AssetPreWarmer] GetDownloadSizeAsync failed for '" + label + "' (ignored): " + sizeError.Message);
                }

                // Download (or verify cache).
                Exception downloadError = null;
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle downloadHandle =
                    default(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle);
                try
                {
                    downloadHandle = UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync(label, true);
                }
                catch (Exception e)
                {
                    downloadError = e;
                }

                if (downloadError == null)
                {
                    while (!downloadHandle.IsDone)
                    {
                        float inner = downloadHandle.PercentComplete;
                        float overall = progressBefore + (inner / total);
                        onProgress?.Invoke(Mathf.Clamp01(overall));
                        yield return null;
                    }
                }
                else
                {
                    Debug.LogWarning("[AssetPreWarmer] DownloadDependenciesAsync failed for '" + label + "' (ignored): " + downloadError.Message);
                }

                completed++;
                float progressAfter = completed / total;
                onProgress?.Invoke(Mathf.Clamp01(progressAfter));
            }
#endif
        }

        /// <summary>Convenience facade: pre-warm a fixed set of group labels.</summary>
        public static IEnumerator PreWarmDefault(Action<float> onProgress)
        {
            var labels = new List<string> { "mainmenu", "level1" };
            yield return PreWarmAsync(labels, onProgress);
        }
    }
}
