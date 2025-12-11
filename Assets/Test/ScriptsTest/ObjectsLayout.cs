using UnityEngine;

public class ObjectsLayout : MonoBehaviour
{
    public enum LayoutDirection { Horizontal, Vertical }
    public enum Alignment { Start, Center, End }

    [Header("Layout Settings")]
    public LayoutDirection direction = LayoutDirection.Horizontal;
    public Alignment alignment = Alignment.Center;

    [Header("Padding Settings")]
    public float horizontalPadding = 1.0f; // Left/Right spacing between items
    public float verticalPadding = 1.0f;   // Up/Down spacing between items
    public float depthPadding = 1.0f;      // Forward/Back spacing between lines

    [Min(1)]
    public int itemsPerLine = 1; // Number of items per row/column

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;

        // Calculate sizes for each child
        float[] widths = new float[childCount];
        float[] heights = new float[childCount];
        for (int i = 0; i < childCount; i++)
        {
            var child = transform.GetChild(i);
            var renderer = child.GetComponent<Renderer>();
            float width = 1f, height = 1f;
            if (renderer != null)
            {
                width = renderer.bounds.size.x;
                height = renderer.bounds.size.y;
            }
            widths[i] = width;
            heights[i] = height;
        }

        if (direction == LayoutDirection.Horizontal)
        {
            // For horizontal layout: X for items in line, Z for new lines, Y stays same
            int lines = Mathf.CeilToInt((float)childCount / itemsPerLine);

            // Calculate width for each line
            float[] lineWidths = new float[lines];
            for (int line = 0; line < lines; line++)
            {
                int startIdx = line * itemsPerLine;
                int endIdx = Mathf.Min(startIdx + itemsPerLine, childCount);
                float lineWidth = 0f;

                for (int i = startIdx; i < endIdx; i++)
                {
                    lineWidth += widths[i];
                    if (i < endIdx - 1) lineWidth += horizontalPadding; // Add horizontal padding between items in line
                }
                lineWidths[line] = lineWidth;
            }

            // Find the maximum line width for alignment
            float maxLineWidth = 0f;
            for (int line = 0; line < lines; line++)
            {
                maxLineWidth = Mathf.Max(maxLineWidth, lineWidths[line]);
            }

            // Calculate total depth (Z axis)
            float totalDepth = 0f;
            for (int i = 0; i < childCount; i++)
            {
                totalDepth = Mathf.Max(totalDepth, heights[i]); // Use heights as depth
            }
            totalDepth = totalDepth * lines + depthPadding * (lines - 1);

            // Determine starting offset based on alignment
            float startX = 0f, startY = 0f, startZ = 0f;
            switch (alignment)
            {
                case Alignment.Center:
                    startX = -maxLineWidth / 2f;
                    startY = 0f; // Keep Y at center level
                    startZ = -totalDepth / 2f;
                    break;
                case Alignment.End:
                    startX = -maxLineWidth;
                    startY = 0f;
                    startZ = -totalDepth;
                    break;
                case Alignment.Start:
                default:
                    startX = 0f;
                    startY = 0f;
                    startZ = 0f;
                    break;
            }

            // Position children: X for horizontal, Z for lines, Y stays same
            float z = startZ;
            for (int line = 0; line < lines; line++)
            {
                int startIdx = line * itemsPerLine;
                int endIdx = Mathf.Min(startIdx + itemsPerLine, childCount);

                // Calculate line starting X based on alignment
                float lineStartX = startX;
                if (alignment == Alignment.Center)
                {
                    lineStartX = -lineWidths[line] / 2f;
                }
                else if (alignment == Alignment.End)
                {
                    lineStartX = -lineWidths[line];
                }

                float x = lineStartX;
                float maxHeightInLine = 0f;

                // Find max height in this line for Z spacing
                for (int i = startIdx; i < endIdx; i++)
                {
                    maxHeightInLine = Mathf.Max(maxHeightInLine, heights[i]);
                }

                // Position items in this line
                for (int i = startIdx; i < endIdx; i++)
                {
                    var child = transform.GetChild(i);
                    child.localPosition = new Vector3(x + widths[i] / 2f, startY, z + maxHeightInLine / 2f);
                    x += widths[i] + horizontalPadding;
                }

                z += maxHeightInLine + depthPadding;
            }
        }
        else // Vertical
        {
            int rows = Mathf.Min(itemsPerLine, childCount);
            int cols = Mathf.CeilToInt((float)childCount / itemsPerLine);

            // Calculate max width per column and max height per row
            float[] colWidths = new float[cols];
            float[] rowHeights = new float[rows];
            for (int i = 0; i < childCount; i++)
            {
                int col = i / itemsPerLine;
                int row = i % itemsPerLine;
                colWidths[col] = Mathf.Max(colWidths[col], widths[i]);
                rowHeights[row] = Mathf.Max(rowHeights[row], heights[i]);
            }

            // Calculate total width and height
            float totalWidth = 0f;
            for (int c = 0; c < cols; c++)
                totalWidth += colWidths[c];
            totalWidth += horizontalPadding * (cols - 1);

            float totalHeight = 0f;
            for (int r = 0; r < rows; r++)
                totalHeight += rowHeights[r];
            totalHeight += verticalPadding * (rows - 1);

            // Determine starting offset based on alignment
            float startX = 0f, startY = 0f;
            switch (alignment)
            {
                case Alignment.Center:
                    startX = -totalWidth / 2f;
                    startY = -totalHeight / 2f;
                    break;
                case Alignment.End:
                    startX = -totalWidth;
                    startY = -totalHeight;
                    break;
                case Alignment.Start:
                default:
                    startX = 0f;
                    startY = 0f;
                    break;
            }

            // Position children in grid
            int iChild = 0;
            float x = startX;
            for (int col = 0; col < cols; col++)
            {
                float y = startY;
                for (int row = 0; row < rows && iChild < childCount; row++, iChild++)
                {
                    var child = transform.GetChild(iChild);
                    float width = colWidths[col];
                    float height = rowHeights[row];

                    float xOffset = 0f;
                    switch (alignment)
                    {
                        case Alignment.Center:
                            xOffset = (colWidths[col] - widths[iChild]) / 2f;
                            break;
                        case Alignment.End:
                            xOffset = (colWidths[col] - widths[iChild]);
                            break;
                        case Alignment.Start:
                        default:
                            xOffset = 0f;
                            break;
                    }
                    child.localPosition = new Vector3(x + xOffset + widths[iChild] / 2f, y + height / 2f, 0);
                    y += height + verticalPadding;
                }
                x += colWidths[col] + horizontalPadding;
            }
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        ApplyLayout();
    }
}