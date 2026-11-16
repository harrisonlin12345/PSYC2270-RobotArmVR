
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ColorAssigner : MonoBehaviour
{
    [Header("Assignment Settings")]
    public float assignmentDelay = 2f;

    [Header("Colors")]
    public Color redColor = Color.red;
    public Color blueColor = Color.blue;
    public Color greenColor = Color.green;

    private HashSet<GameObject> assignedBlocks =
        new HashSet<GameObject>();

    private Dictionary<GameObject, Coroutine> pendingBlocks =
        new Dictionary<GameObject, Coroutine>();

    private HashSet<GameObject> overlappingBlocks =
        new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        GameObject block = GetBlock(other);

        if (block == null)
            return;

        overlappingBlocks.Add(block);

        if (!assignedBlocks.Contains(block) &&
            !pendingBlocks.ContainsKey(block))
        {
            pendingBlocks[block] =
                StartCoroutine(AssignAfterDelay(block));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject block = GetBlock(other);

        if (block == null)
            return;

        overlappingBlocks.Remove(block);

        if (pendingBlocks.TryGetValue(
            block, out Coroutine routine))
        {
            StopCoroutine(routine);
            pendingBlocks.Remove(block);
        }
    }

    private IEnumerator AssignAfterDelay(GameObject block)
    {
        // Wait for the participant to release the block.
        XRGrabInteractable grab =
            block.GetComponent<XRGrabInteractable>();

        while (block != null &&
               overlappingBlocks.Contains(block) &&
               grab != null &&
               grab.isSelected)
        {
            yield return null;
        }

        if (block == null ||
            !overlappingBlocks.Contains(block))
        {
            pendingBlocks.Remove(block);
            yield break;
        }

        // Begin the two-second assignment timer.
        yield return new WaitForSeconds(assignmentDelay);

        if (block == null ||
            !overlappingBlocks.Contains(block))
        {
            pendingBlocks.Remove(block);
            yield break;
        }

        // Do not assign while the block is being held.
        if (grab != null && grab.isSelected)
        {
            pendingBlocks.Remove(block);
            yield break;
        }

        int randomIndex = Random.Range(0, 3);

        Color selectedColor;

        switch (randomIndex)
        {
            case 0:
                selectedColor = redColor;
                break;

            case 1:
                selectedColor = blueColor;
                break;

            default:
                selectedColor = greenColor;
                break;
        }

        Renderer blockRenderer =
            block.GetComponentInChildren<Renderer>();

        if (blockRenderer != null)
        {
            blockRenderer.material.color = selectedColor;
        }

        assignedBlocks.Add(block);
        pendingBlocks.Remove(block);

        Debug.Log(
            "ColorAssigner: Assigned " +
            GetColorName(randomIndex)
        );
    }

    private string GetColorName(int index)
    {
        switch (index)
        {
            case 0:
                return "RED";

            case 1:
                return "BLUE";

            default:
                return "GREEN";
        }
    }

    private GameObject GetBlock(Collider other)
    {
        XRGrabInteractable grab =
            other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            return null;

        return grab.gameObject;
    }
}
