
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class BlockGrabHandler : MonoBehaviour
{
    private RobotDeliveryController robot;
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    private bool hasBeenGrabbed = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    public void Initialize(RobotDeliveryController controller)
    {
        robot = controller;
        hasBeenGrabbed = false;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (hasBeenGrabbed)
            return;

        hasBeenGrabbed = true;

        // Detach while preserving world position and scale.
        transform.SetParent(null, true);

        // Restore physics and allow XR Grab Interactable
        // to control the block.
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (robot != null)
        {
            robot.OnBlockGrabbed(gameObject);
        }

        Debug.Log("Participant grabbed the white block.");
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }
    }
}
