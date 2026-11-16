
using System;
using System.Collections;
using UnityEngine;

public class RobotDeliveryController : MonoBehaviour
{
    public enum MotionMode
    {
        SlowStraight,
        SlowCurved,
        FastStraight,
        FastCurved
    }

    [Header("Experimental Condition")]
    public MotionMode motionMode = MotionMode.SlowStraight;

    [Header("Robot Joints")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("Block Settings")]
    public GameObject blockPrefab;
    public Transform gripperAttachPoint;
    public Vector3 blockLocalScale = new Vector3(5f, 5f, 5f);

    [Header("Joint Poses (degrees)")]
    public float[] pickupPose =
        { -90f, 20f, -150f, 0f, 20f, -90f };

    public float[] handoffPose =
        { 0f, -30f, 45f, 0f, 20f, 0f };

    [Header("Speed Settings")]
    public float slowSpeed = 15f;
    public float fastSpeed = 45f;
    public float acceleration = 60f;

    [Header("Curved Motion Settings")]
    public float curveAmplitude = 15f;

    [Header("Movement")]
    public float arrivalTolerance = 3f;
    public float movementTimeout = 60f;

    [Header("Drive Settings")]
    public float stiffness = 10000f;
    public float damping = 1000f;
    public float forceLimit = 10000f;

    [Header("Timing")]
    public float pickupPause = 0.5f;
    public float returnDelay = 1f;

    private float[] commandedAngles;
    private float[] commandedSpeeds;

    private GameObject heldBlock;
    private bool blockGrabbed = false;
    private bool handoffReady = false;

    private void Start()
    {
        if (joints == null || joints.Length != 6 ||
            pickupPose == null || pickupPose.Length != 6 ||
            handoffPose == null || handoffPose.Length != 6 ||
            blockPrefab == null || gripperAttachPoint == null)
        {
            Debug.LogError(
                "RobotDeliveryController: Missing references."
            );

            enabled = false;
            return;
        }

        commandedAngles = new float[6];
        commandedSpeeds = new float[6];

        for (int i = 0; i < 6; i++)
        {
            if (joints[i] == null ||
                joints[i].jointType !=
                    ArticulationJointType.RevoluteJoint)
            {
                Debug.LogError("Invalid robot joint: " + i);
                enabled = false;
                return;
            }

            var drive = joints[i].xDrive;
            drive.stiffness = stiffness;
            drive.damping = damping;
            drive.forceLimit = forceLimit;
            joints[i].xDrive = drive;

            commandedAngles[i] =
                joints[i].jointPosition[0] * Mathf.Rad2Deg;
        }

        StartCoroutine(DeliverySequence());
    }

    private IEnumerator DeliverySequence()
    {
        while (true)
        {
            bool reached = false;
            blockGrabbed = false;
            handoffReady = false;

            // 1. Move to pickup position.
            yield return MoveWithPattern(
                pickupPose,
                result => reached = result
            );

            if (!reached)
            {
                Debug.LogWarning(
                    "Robot could not reach pickup pose."
                );
                yield break;
            }

            // 2. Pause before creating a new block.
            yield return new WaitForSeconds(pickupPause);

            // 3. Spawn block inside gripper.
            SpawnBlockInGripper();

            if (heldBlock == null)
            {
                Debug.LogError("Failed to spawn block.");
                yield break;
            }

            // 4. Deliver block to participant.
            reached = false;

            yield return MoveWithPattern(
                handoffPose,
                result => reached = result
            );

            if (!reached)
            {
                Debug.LogWarning(
                    "Robot could not reach handoff pose."
                );
                yield break;
            }

            // 5. Allow participant to grab the block.
            handoffReady = true;

            Debug.Log(
                "Robot reached handoff pose. Waiting for grab."
            );

            // 6. Wait for participant to grab the block.
            yield return new WaitUntil(() => blockGrabbed);

            // 7. Wait one second before returning.
            yield return new WaitForSeconds(returnDelay);

            Debug.Log(
                "Returning to pickup position for next block."
            );

            // Loop restarts automatically.
        }
    }

    private IEnumerator MoveWithPattern(
        float[] destination,
        Action<bool> onComplete)
    {
        bool curved =
            motionMode == MotionMode.SlowCurved ||
            motionMode == MotionMode.FastCurved;

        float speed =
            motionMode == MotionMode.SlowStraight ||
            motionMode == MotionMode.SlowCurved
                ? slowSpeed
                : fastSpeed;

        if (!curved)
        {
            yield return MoveToPose(
                destination,
                speed,
                onComplete
            );

            yield break;
        }

        // Continuous curved motion.
        // No stopping at intermediate poses.

        float[] start =
            (float[])commandedAngles.Clone();

        float maxDistance = 0f;

        for (int i = 0; i < 6; i++)
        {
            maxDistance = Mathf.Max(
                maxDistance,
                Mathf.Abs(destination[i] - start[i])
            );
        }

        float duration = Mathf.Max(
            maxDistance / Mathf.Max(speed, 0.01f),
            0.1f
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            // Smooth acceleration and deceleration.
            float smoothT = Mathf.SmoothStep(
                0f, 1f, t
            );

            for (int i = 0; i < 6; i++)
            {
                float linear = Mathf.Lerp(
                    start[i],
                    destination[i],
                    smoothT
                );

                // Curved offset is zero at both endpoints.
                float offset =
                    Mathf.Sin(Mathf.PI * smoothT) *
                    curveAmplitude *
                    ((i % 2 == 0) ? 1f : -1f);

                float target = linear + offset;

                var drive = joints[i].xDrive;

                if (joints[i].twistLock ==
                    ArticulationDofLock.LimitedMotion)
                {
                    target = Mathf.Clamp(
                        target,
                        drive.lowerLimit,
                        drive.upperLimit
                    );
                }

                drive.target = target;
                joints[i].xDrive = drive;

                commandedAngles[i] = target;
            }

            yield return new WaitForFixedUpdate();
        }

        // Set exact final joint targets.
        for (int i = 0; i < 6; i++)
        {
            var drive = joints[i].xDrive;
            float target = destination[i];

            if (joints[i].twistLock ==
                ArticulationDofLock.LimitedMotion)
            {
                target = Mathf.Clamp(
                    target,
                    drive.lowerLimit,
                    drive.upperLimit
                );
            }

            drive.target = target;
            joints[i].xDrive = drive;

            commandedAngles[i] = target;
        }

        // Wait for physical joints to reach destination.
        float settleTime = 0f;

        while (!ActualJointsNear(destination) &&
               settleTime < movementTimeout)
        {
            settleTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        bool success = ActualJointsNear(destination);

        if (!success)
        {
            Debug.LogWarning(
                "Curved movement timed out."
            );
        }

        onComplete?.Invoke(success);
    }

    private IEnumerator MoveToPose(
        float[] pose,
        float speed,
        Action<bool> onComplete)
    {
        float elapsed = 0f;

        for (int i = 0; i < commandedSpeeds.Length; i++)
        {
            commandedSpeeds[i] = 0f;
        }

        while (elapsed < movementTimeout)
        {
            bool commandFinished = true;
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < 6; i++)
            {
                var drive = joints[i].xDrive;

                float target = pose[i];

                if (joints[i].twistLock ==
                    ArticulationDofLock.LimitedMotion)
                {
                    target = Mathf.Clamp(
                        target,
                        drive.lowerLimit,
                        drive.upperLimit
                    );
                }

                float remaining = Mathf.Abs(
                    target - commandedAngles[i]
                );

                float safeAcceleration =
                    Mathf.Max(acceleration, 0.01f);

                float desiredSpeed = Mathf.Min(
                    Mathf.Max(speed, 0.01f),
                    Mathf.Sqrt(
                        2f * safeAcceleration * remaining
                    )
                );

                commandedSpeeds[i] = Mathf.MoveTowards(
                    commandedSpeeds[i],
                    desiredSpeed,
                    safeAcceleration * dt
                );

                commandedAngles[i] = Mathf.MoveTowards(
                    commandedAngles[i],
                    target,
                    commandedSpeeds[i] * dt
                );

                drive.target = commandedAngles[i];
                joints[i].xDrive = drive;

                if (Mathf.Abs(
                    target - commandedAngles[i]) > 0.1f)
                {
                    commandFinished = false;
                }
            }

            if (commandFinished && ActualJointsNear(pose))
            {
                onComplete?.Invoke(true);
                yield break;
            }

            elapsed += dt;
            yield return new WaitForFixedUpdate();
        }

        Debug.LogWarning(
            "Straight movement timed out."
        );

        onComplete?.Invoke(false);
    }

    private bool ActualJointsNear(float[] pose)
    {
        for (int i = 0; i < 6; i++)
        {
            float target = pose[i];
            var drive = joints[i].xDrive;

            if (joints[i].twistLock ==
                ArticulationDofLock.LimitedMotion)
            {
                target = Mathf.Clamp(
                    target,
                    drive.lowerLimit,
                    drive.upperLimit
                );
            }

            float actual =
                joints[i].jointPosition[0] * Mathf.Rad2Deg;

            if (Mathf.Abs(actual - target) >
                arrivalTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnBlockInGripper()
    {
        if (heldBlock != null)
            return;

        heldBlock = Instantiate(
            blockPrefab,
            gripperAttachPoint.position,
            gripperAttachPoint.rotation,
            gripperAttachPoint
        );

        heldBlock.transform.localPosition = Vector3.zero;
        heldBlock.transform.localRotation = Quaternion.identity;
        heldBlock.transform.localScale = blockLocalScale;

        Rigidbody rb = heldBlock.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Colliders must remain enabled for XR grabbing.
        foreach (Collider col in
            heldBlock.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }

        // Connect the block to this robot.
        BlockGrabHandler grabHandler =
            heldBlock.GetComponent<BlockGrabHandler>();

        if (grabHandler != null)
        {
            grabHandler.Initialize(this);
        }
        else
        {
            Debug.LogError(
                "WhiteBlock prefab needs BlockGrabHandler."
            );
        }

        Debug.Log(
            "White block spawned in gripper."
        );
    }

    public void OnBlockGrabbed(GameObject block)
    {
        if (block != heldBlock)
            return;

        // Ignore grabs before reaching handoff pose.
        if (!handoffReady)
            return;

        blockGrabbed = true;
        handoffReady = false;
        heldBlock = null;

        Debug.Log(
            "Participant grabbed block. " +
            "Robot will return after 1 second."
        );
    }
}
