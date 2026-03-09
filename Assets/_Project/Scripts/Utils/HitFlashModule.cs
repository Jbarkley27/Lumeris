using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HitFlashModule : MonoBehaviour
{
    private Dictionary<MeshRenderer, Material[]> originalMaterials = new Dictionary<MeshRenderer, Material[]>();
    private Coroutine flashCoroutine;
    public FlashData flashData;

    [System.Serializable]
    public struct FlashData
    {
        public Material flashColor;
        public float flashDuration;

        public FlashData(Material flashColor, float duration)
        {
            this.flashColor = flashColor;
            flashDuration = duration;
        }
    }

    public void FlashAll()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            RestoreOriginalMaterials(); // Restore in case it was mid-flash
        }

        flashCoroutine = StartCoroutine(FlashCoroutine(flashData));
    }

    private IEnumerator FlashCoroutine(FlashData flashData)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

        originalMaterials.Clear();
        foreach (var rend in renderers)
        {
            originalMaterials[rend] = rend.materials;

            // Replace all materials with the flash material
            Material[] flashMats = new Material[rend.materials.Length];
            for (int i = 0; i < flashMats.Length; i++)
                flashMats[i] = flashData.flashColor;

            rend.materials = flashMats;
        }

        yield return new WaitForSeconds(flashData.flashDuration);

        RestoreOriginalMaterials();
        flashCoroutine = null;
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var pair in originalMaterials)
        {
            if (pair.Key != null)
                pair.Key.materials = pair.Value;
        }
        originalMaterials.Clear();
    }
}
