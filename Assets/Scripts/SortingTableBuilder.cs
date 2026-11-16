
using UnityEngine;

public class SortingTableBuilder : MonoBehaviour
{
    [Header("Table Dimensions")]
    public float tableWidth = 1.4f;
    public float tableDepth = 0.8f;
    public float tableHeight = 0.8f;
    public float tabletopThickness = 0.06f;

    [Header("Zone Settings")]
    public float zoneThickness = 0.012f;
    public float zoneGap = 0.025f;

    [Header("Color Assignment")]
    public float assignmentDelay = 2f;
    public float triggerHeight = 0.15f;

    [Header("Materials")]
    public Color tableColor =
        new Color(0.3f, 0.3f, 0.3f);

    public Color assignerColor = Color.yellow;
    public Color redColor = Color.red;
    public Color blueColor = Color.blue;
    public Color greenColor = Color.green;

    private GameObject tableRoot;

    private void Start()
    {
        BuildTable();
    }

    private void BuildTable()
    {
        // Prevent duplicate tables.
        if (tableRoot != null)
            Destroy(tableRoot);

        tableRoot = new GameObject("SortingTable");
        tableRoot.transform.SetParent(transform, false);

        // Main tabletop.
        CreateCube(
            "Tabletop",
            new Vector3(0f, tableHeight, 0f),
            new Vector3(
                tableWidth,
                tabletopThickness,
                tableDepth
            ),
            tableColor
        );

        // Four table legs.
        float legX = tableWidth / 2f - 0.08f;
        float legZ = tableDepth / 2f - 0.08f;

        CreateLeg(-legX, -legZ);
        CreateLeg(legX, -legZ);
        CreateLeg(-legX, legZ);
        CreateLeg(legX, legZ);

        // Top surface height.
        float surfaceY =
            tableHeight +
            tabletopThickness / 2f +
            zoneThickness / 2f +
            0.001f;

        float margin = 0.05f;

        float usableWidth =
            tableWidth - 2f * margin;

        float usableDepth =
            tableDepth - 2f * margin;

        // --------------------------------
        // YELLOW COLOR ASSIGNER
        // --------------------------------

        float assignerDepth =
            usableDepth / 2f - zoneGap / 2f;

        float assignerZ =
            usableDepth / 4f + zoneGap / 4f;

        GameObject assigner = CreateCube(
            "ColorAssigner",
            new Vector3(
                0f,
                surfaceY,
                assignerZ
            ),
            new Vector3(
                usableWidth,
                zoneThickness,
                assignerDepth
            ),
            assignerColor
        );

        // Create invisible trigger above assigner.
        CreateAssignmentTrigger(
            assigner,
            usableWidth,
            assignerDepth
        );

        // --------------------------------
        // RED, BLUE, GREEN SORTING ZONES
        // --------------------------------

        float sortingDepth =
            usableDepth / 2f - zoneGap / 2f;

        float sortingZ =
            -usableDepth / 4f - zoneGap / 4f;

        float sortingWidth =
            (usableWidth - 2f * zoneGap) / 3f;

        float spacing =
            sortingWidth + zoneGap;

        CreateCube(
            "RedZone",
            new Vector3(
                -spacing,
                surfaceY,
                sortingZ
            ),
            new Vector3(
                sortingWidth,
                zoneThickness,
                sortingDepth
            ),
            redColor
        );

        CreateCube(
            "BlueZone",
            new Vector3(
                0f,
                surfaceY,
                sortingZ
            ),
            new Vector3(
                sortingWidth,
                zoneThickness,
                sortingDepth
            ),
            blueColor
        );

        CreateCube(
            "GreenZone",
            new Vector3(
                spacing,
                surfaceY,
                sortingZ
            ),
            new Vector3(
                sortingWidth,
                zoneThickness,
                sortingDepth
            ),
            greenColor
        );

        Debug.Log(
            "Sorting table created with color assigner."
        );
    }

    private void CreateAssignmentTrigger(
        GameObject assigner,
        float width,
        float depth)
    {
        GameObject trigger =
            new GameObject("AssignmentTrigger");

        // Parent to the table root rather than
        // the thin assigner surface to avoid
        // inherited nonuniform scaling.
        trigger.transform.SetParent(
            tableRoot.transform, false
        );

        float surfaceTop =
            assigner.transform.localPosition.y +
            zoneThickness / 2f;

        trigger.transform.localPosition =
            new Vector3(
                assigner.transform.localPosition.x,
                surfaceTop + triggerHeight / 2f,
                assigner.transform.localPosition.z
            );

        trigger.transform.localScale =
            Vector3.one;

        BoxCollider collider =
            trigger.AddComponent<BoxCollider>();

        collider.isTrigger = true;

        collider.size = new Vector3(
            width,
            triggerHeight,
            depth
        );

        // Attach the assignment behavior.
        ColorAssigner colorAssigner =
            trigger.AddComponent<ColorAssigner>();

        colorAssigner.assignmentDelay =
            assignmentDelay;

        colorAssigner.redColor = redColor;
        colorAssigner.blueColor = blueColor;
        colorAssigner.greenColor = greenColor;
    }

    private void CreateLeg(float x, float z)
    {
        CreateCube(
            "TableLeg",
            new Vector3(
                x,
                tableHeight / 2f,
                z
            ),
            new Vector3(
                0.08f,
                tableHeight,
                0.08f
            ),
            tableColor
        );
    }

    private GameObject CreateCube(
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        GameObject obj =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        obj.name = objectName;

        obj.transform.SetParent(
            tableRoot.transform, false
        );

        obj.transform.localPosition =
            localPosition;

        obj.transform.localScale =
            localScale;

        Renderer renderer =
            obj.GetComponent<Renderer>();

        Shader shader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material =
            new Material(shader);

        material.color = color;

        renderer.material = material;

        return obj;
    }
}
