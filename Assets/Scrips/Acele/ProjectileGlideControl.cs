using UnityEngine;

public class ProjectileGlideControl : MonoBehaviour
{
    [Header("Glide Configuration")]
    public float glideMoveSpeed = 10f;
    public float maxYPosition = 15f;
    public float minYPosition = -5f;
    public float rotationSmoothness = 5f;

    private Rigidbody rb;
    private bool isGliding = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ProjectileGlideControl requires a Rigidbody on the projectile.", this);
            enabled = false; // Disable script if no Rigidbody
            return;
        }
        enabled = false; // Start disabled, Slingshot will enable it via ActivateGlide
    }

    public void ActivateGlide(Vector3 launchVelocity)
    {
        if (rb == null) return;

        this.enabled = true; // Enable the script to run FixedUpdate
        isGliding = true;
        rb.useGravity = false; // Disable gravity while gliding
        rb.linearVelocity = launchVelocity; // Set initial velocity from launch

        if (launchVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(launchVelocity.normalized);
        }
        Debug.Log(gameObject.name + " Glide Control Activated with velocity: " + launchVelocity);
    }

    void FixedUpdate()
    {
        if (!isGliding || rb == null)
        {
            return;
        }

        // Accelerometer input for vertical movement
        // Input.acceleration.y is typically for portrait tilt (top of phone up/down)
        float tiltInput = Input.acceleration.y;

        // Target vertical speed based on tilt
        float targetVerticalSpeed = tiltInput * glideMoveSpeed;

        // Apply the vertical speed, maintain horizontal velocity
        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(currentVelocity.x, targetVerticalSpeed, currentVelocity.z);

        // Clamp Y position
        Vector3 currentPosition = rb.position;
        float newY = Mathf.Clamp(currentPosition.y, minYPosition, maxYPosition);

        // Only apply position change if it's different, to avoid jitter if already at clamp boundary and velocity points outside
        if (Mathf.Abs(currentPosition.y - newY) > 0.001f || rb.linearVelocity.y * (newY - currentPosition.y) >= 0)
        { // if newY is different OR velocity is trying to move it further into clamped zone
            rb.MovePosition(new Vector3(currentPosition.x, newY, currentPosition.z));
        }


        // Keep projectile oriented towards its velocity vector
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSmoothness));
        }
    }

    public void DeactivateGlide()
    {
        if (!isGliding) return; // Avoid redundant calls

        isGliding = false;
        this.enabled = false; // Disable this script's FixedUpdate
        if (rb != null)
        {
            rb.useGravity = true; // Restore gravity (or its original state if you stored it)
        }
        Debug.Log(gameObject.name + " Glide Control Deactivated");
    }

    // Public property to check if gliding (useful for Projectile.cs)
    public bool IsGliding => isGliding;
}