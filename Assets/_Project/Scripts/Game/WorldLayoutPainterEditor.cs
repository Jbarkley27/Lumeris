#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector + scene controls for WorldLayoutPainter.
/// Controls:
/// Left Click = paint, Shift + Left Click = erase.
/// </summary>
[CustomEditor(typeof(WorldLayoutPainter))]
public class WorldLayoutPainterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DrawHeightPatternPresetHelp();

        WorldLayoutPainter painter = (WorldLayoutPainter)target;

        DrawPresetPaletteControls(painter);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene Paint Controls:\n" +
            "- Left Click: Paint\n" +
            "- Shift + Left Click: Erase\n" +
            "- Brush is square and centered on hovered cell.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload World Preview"))
            {
                painter.ReloadWorldPreview();
            }

            if (GUILayout.Button("Clear Entire Layout"))
            {
                if (painter.Layout == null)
                {
                    return;
                }

                bool confirm = EditorUtility.DisplayDialog(
                    "Clear Entire Layout",
                    "This will mark every cell as empty. Continue?",
                    "Clear",
                    "Cancel");

                if (confirm)
                {
                    Undo.RecordObject(painter.Layout, "Clear World Layout");
                    painter.ClearAllCells();
                }
            }
        }


        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Randomize Heights"))
            {
                if (painter.Layout != null)
                {
                    Undo.RecordObject(painter.Layout, "Randomize Layout Height Offsets");
                    painter.RandomizeHeightOffsetsInLayout();
                }
            }

            if (GUILayout.Button("Flatten Heights"))
            {
                if (painter.Layout != null)
                {
                    Undo.RecordObject(painter.Layout, "Flatten Layout Height Offsets");
                    painter.FlattenHeightOffsetsInLayout();
                }
            }
        }

    }






    /// <summary>
    /// Shows an explanation for the currently selected height preset.
    /// Unity does not provide per-option enum tooltips in the default popup,
    /// so this contextual help gives designers immediate meaning.
    /// </summary>
    private void DrawHeightPatternPresetHelp()
    {
        SerializedProperty presetProp = serializedObject.FindProperty("bulkHeightPreset");
        if (presetProp == null)
        {
            return;
        }

        WorldLayoutPainter.HeightPatternPreset selected =
            (WorldLayoutPainter.HeightPatternPreset)presetProp.enumValueIndex;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Height Pattern Help", EditorStyles.boldLabel);

        // Explain what the selected preset actually does.
        EditorGUILayout.HelpBox(GetHeightPatternDescription(selected), MessageType.None);

        // Explain technical terms in plain language.
        EditorGUILayout.HelpBox(
            "Glossary: Octaves = layered noise frequencies. " +
            "Jitter = small random variation. " +
            "Quantized steps = snapping smooth values into fixed levels.",
            MessageType.Info);
    }

    /// <summary>
    /// Human-readable explanation for each preset mode.
    /// </summary>
    private static string GetHeightPatternDescription(WorldLayoutPainter.HeightPatternPreset preset)
    {
        switch (preset)
        {
            case WorldLayoutPainter.HeightPatternPreset.FlatJitter:
                return "FlatJitter: Per-cell random offsets. Good for subtle breakup without large structures.";

            case WorldLayoutPainter.HeightPatternPreset.Terraces:
                return "Terraces: Noise is snapped into step levels, creating staircase/plateau forms.";

            case WorldLayoutPainter.HeightPatternPreset.BlobClusters:
                return "BlobClusters: Coarse noise + detail noise. Produces clumped raised/lowered islands.";

            case WorldLayoutPainter.HeightPatternPreset.RidgeBands:
                return "RidgeBands: Converts noise into stripe-like ridges for directional layered patterns.";

            case WorldLayoutPainter.HeightPatternPreset.EdgeFalloff:
                return "EdgeFalloff: Height follows distance from center. Use invert to make edge-high vs center-high.";

            case WorldLayoutPainter.HeightPatternPreset.MixedFractal:
                return "MixedFractal: Multiple octaves blended together plus jitter. Most natural/organic result.";

            default:
                return "Unknown preset.";
        }
    }




    /// <summary>
    /// Inspector controls for quick preset workflow:
    /// select preset, apply to brush, and save current brush back into preset.
    /// </summary>
    private void DrawPresetPaletteControls(WorldLayoutPainter painter)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset Palette Quick Actions", EditorStyles.boldLabel);

        if (painter.PresetCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "No brush presets configured.\nUse the context menu or button below to create starter presets.",
                MessageType.Warning);

            if (GUILayout.Button("Create Default Presets"))
            {
                Undo.RecordObject(painter, "Create Default Brush Presets");
                painter.CreateDefaultBrushPresets();
                EditorUtility.SetDirty(painter);
            }

            return;
        }

        string[] names = painter.GetPresetNames();

        int popupIndex = painter.SelectedPresetIndex;
        if (popupIndex < 0)
        {
            popupIndex = 0;
        }

        int nextIndex = EditorGUILayout.Popup("Selected Preset", popupIndex, names);

        if (nextIndex != painter.SelectedPresetIndex)
        {
            Undo.RecordObject(painter, "Select Brush Preset");
            painter.SelectPreset(nextIndex);
            EditorUtility.SetDirty(painter);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Preset To Brush"))
            {
                Undo.RecordObject(painter, "Apply Brush Preset");
                painter.ApplyPresetToBrush(nextIndex);
                EditorUtility.SetDirty(painter);
            }

            if (GUILayout.Button("Save Brush To Preset"))
            {
                Undo.RecordObject(painter, "Save Brush To Preset");
                painter.SaveBrushToPreset(nextIndex);
                EditorUtility.SetDirty(painter);
            }
        }
    }


    private void OnSceneGUI()
    {
        WorldLayoutPainter painter = (WorldLayoutPainter)target;

        if (painter == null || !painter.EnableScenePainting || painter.Layout == null)
        {
            return;
        }

        Event evt = Event.current;

        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        Plane paintPlane = new Plane(Vector3.up, new Vector3(0f, painter.Layout.worldOrigin.y, 0f));

        if (!paintPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 worldPoint = ray.GetPoint(enter);

        if (!painter.TryWorldToCell(worldPoint, out Vector2Int centerCell))
        {
            return;
        }

        DrawBrushPreview(painter, centerCell);

        // Paint/erase with mouse when not using alt-camera orbit.
        bool isPaintEvent =
            (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) &&
            evt.button == 0 &&
            !evt.alt;

        if (!isPaintEvent)
        {
            return;
        }

        bool erase = evt.shift;

        Undo.RecordObject(painter.Layout, erase ? "Erase Layout Cells" : "Paint Layout Cells");
        painter.ApplyBrushAtCell(centerCell, erase);
        evt.Use();
    }

    /// <summary>
    /// Draws a flat preview of current brush footprint on the world plane.
    /// </summary>
    private static void DrawBrushPreview(WorldLayoutPainter painter, Vector2Int centerCell)
    {
        float cellSize = painter.Layout.cellSize;
        int radius = painter.BrushRadius;

        Color fillColor = new Color(0.2f, 0.8f, 1f, 0.08f);
        Color lineColor = new Color(0.2f, 0.8f, 1f, 0.95f);

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + dx, centerCell.y + dy);

                if (!painter.Layout.IsInBounds(cell.x, cell.y))
                {
                    continue;
                }

                Vector3 c = painter.CellToWorld(cell);

                Vector3[] verts = new Vector3[]
                {
                    c + new Vector3(-cellSize * 0.5f, 0f, -cellSize * 0.5f),
                    c + new Vector3(-cellSize * 0.5f, 0f,  cellSize * 0.5f),
                    c + new Vector3( cellSize * 0.5f, 0f,  cellSize * 0.5f),
                    c + new Vector3( cellSize * 0.5f, 0f, -cellSize * 0.5f),
                };

                Handles.DrawSolidRectangleWithOutline(verts, fillColor, lineColor);
            }
        }
    }
}
#endif
