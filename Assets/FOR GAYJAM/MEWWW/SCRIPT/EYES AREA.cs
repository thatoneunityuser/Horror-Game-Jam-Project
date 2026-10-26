using UnityEngine;

public class EYESAREA : MonoBehaviour
{
    [Header("Area Bounds")]
    [Tooltip("Size of the area in world units (Width, Height)")]
    public Vector2 areaSize = new Vector2(10f, 6f);
    public Vector2 areaOffset = Vector2.zero;

    public Bounds GetBounds()
    {
        Vector3 center = (Vector2)transform.position + areaOffset;
        return new Bounds(center, new Vector3(areaSize.x, areaSize.y, 1f));
    }

    public Vector2 ClampToBounds(Vector2 position, float eyePadding = 0.5f)
    {
        Bounds b = GetBounds();
        float minX = b.min.x + eyePadding;
        float maxX = b.max.x - eyePadding;
        float minY = b.min.y + eyePadding;
        float maxY = b.max.y - eyePadding;

        // In case area is smaller than padding
        if (minX > maxX) minX = maxX = b.center.x;
        if (minY > maxY) minY = maxY = b.center.y;

        return new Vector2(
            Mathf.Clamp(position.x, minX, maxX),
            Mathf.Clamp(position.y, minY, maxY)
        );
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Bounds b = GetBounds();
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
