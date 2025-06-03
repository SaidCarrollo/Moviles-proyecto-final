using UnityEngine;
using UnityEngine.InputSystem;

using UnityEngine;
using UnityEngine.InputSystem;

// Assume ProjectilePowerType enum is defined elsewhere or add it here
// public enum ProjectilePowerType { Normal, ExplodeOnImpact, SplitOnTap, SpeedBoostOnTap, PierceThrough }

public class Projectile : MonoBehaviour
{
    [Header("Configuracion del Poder")]
    public ProjectilePowerType powerType = ProjectilePowerType.Normal;
    public bool powerRequiresTap = false;
    public GameObject effectOnActivatePrefab;
    public AudioClip soundOnActivate;

    [Header("Parametros Especificos del Poder")]
    public float explosionRadius = 2f;
    public float explosionForce = 500f;
    public LayerMask explodableLayers;
    public GameObject[] splitProjectilePrefabs;
    public int numberOfSplits = 3;
    public float splitSpreadAngle = 30f;
    public float speedBoostMultiplier = 1.5f;
    public int maxPierces = 1;
    private int currentPierces = 0;

    protected Rigidbody rb;
    protected bool isLaunched = false;
    protected bool powerActivated = false;
    protected AudioSource audioSource;

    // --- MODIFICATION START: Glide Control Reference ---
    protected ProjectileGlideControl glideControl;
    protected CameraFollowProjectile cameraFollower;
    // --- MODIFICATION END ---

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogError("El proyectil necesita un Rigidbody.", this);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // --- MODIFICATION START: Get GlideControl and CameraFollower ---
        glideControl = GetComponent<ProjectileGlideControl>();
        if (Camera.main != null)
        {
            cameraFollower = Camera.main.GetComponent<CameraFollowProjectile>();
        }
        // --- MODIFICATION END ---
    }

    public virtual void NotifyLaunched()
    {
        isLaunched = true;
        powerActivated = false;
        currentPierces = 0;
        // Glide control is activated by Slingshot after this, so no need to disable it here.
    }

    protected virtual void Update()
    {
        if (isLaunched && !powerActivated && powerRequiresTap)
        {
            // Prevent tap power activation if currently gliding and actively controlled by player,
            // unless you want tap powers during glide. For now, let's assume glide takes precedence.
            if (glideControl != null && glideControl.IsGliding) // Check IsGliding property
            {
                // return; // Uncomment if you want to disable tap powers while gliding
            }

            bool tapOccurred = false;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                tapOccurred = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapOccurred = true;
            }

            if (tapOccurred)
            {
                ActivatePower();
            }
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!isLaunched) return; // Only process collisions if launched

        // If it was gliding and hits something, potentially stop gliding
        bool shouldStopGlideAndFollow = false;

        if (powerRequiresTap && !powerActivated && powerType != ProjectilePowerType.ExplodeOnImpact)
        {
            // If it requires a tap that hasn't happened, and it's not an auto-explode on impact type,
            // the impact might just be a normal physical collision.
            // You might want it to stop gliding here or let it bounce if physics allows.
            // For simplicity, let's assume most impacts stop detailed glide control.
            if (glideControl != null && glideControl.IsGliding)
            {
                // If it's a minor collision and you want it to continue gliding, add more complex logic here.
                // For now, any collision while gliding might stop it.
                // shouldStopGlideAndFollow = true; // Or handle more selectively below
            }
        }

        // Logic for power activation on impact or handling pierce
        switch (powerType)
        {
            case ProjectilePowerType.ExplodeOnImpact:
                if (!powerActivated)
                {
                    ActivatePower(collision.contacts[0].point); // This will call DeactivateGlideAndFollowIfNeeded
                    shouldStopGlideAndFollow = true; // ActivatePower handles it, but good to note
                }
                break;
            case ProjectilePowerType.PierceThrough:
                HandlePierce(collision.gameObject); // HandlePierce will decide if it stops
                if (powerActivated) // If max pierces reached
                {
                    shouldStopGlideAndFollow = true;
                }
                break;
            case ProjectilePowerType.SplitOnTap: // Fallthrough if it hits something before tap
            case ProjectilePowerType.SpeedBoostOnTap: // Fallthrough
            case ProjectilePowerType.Normal:
            default:

                shouldStopGlideAndFollow = true;

                if (!powerActivated) // If normal impact without special power yet.
                {
                    // Destroy(gameObject, 0.1f); // Example: destroy after a small delay
                    // Or if you have a generic damage/destroy on impact for "Normal"
                }
                break;
        }

        if (shouldStopGlideAndFollow)
        {
            DeactivateGlideAndFollowIfNeeded();
        }
    }

    public virtual void ActivatePower(Vector3? activationPoint = null)
    {
        if (powerActivated || !isLaunched) return;
        powerActivated = true; // Mark as activated

        DeactivateGlideAndFollowIfNeeded(); // Stop gliding before activating power effects

        if (soundOnActivate != null) audioSource.PlayOneShot(soundOnActivate);
        if (effectOnActivatePrefab != null) Instantiate(effectOnActivatePrefab, activationPoint ?? transform.position, Quaternion.identity);

        switch (powerType)
        {
            case ProjectilePowerType.SplitOnTap:
                PerformSplit();
                Destroy(gameObject);
                break;
            case ProjectilePowerType.SpeedBoostOnTap:
                PerformSpeedBoost();
                // May or may not destroy itself after speed boost
                break;
            case ProjectilePowerType.ExplodeOnImpact:
                PerformExplosion(activationPoint ?? transform.position);
                Destroy(gameObject);
                break;
                // PierceThrough is handled in OnCollisionEnter/HandlePierce
                // Normal might just mean it relies on OnCollisionEnter to be destroyed/deactivated
        }
    }


    protected virtual void DeactivateGlideAndFollowIfNeeded()
    {
        if (glideControl != null && glideControl.IsGliding)
        {
            glideControl.DeactivateGlide();
        }
        if (cameraFollower != null && cameraFollower.target == this.transform)
        {
            cameraFollower.StopFollowing();
        }
    }


    protected virtual void PerformExplosion(Vector3 explosionCenter)
    {
        Collider[] colliders = Physics.OverlapSphere(explosionCenter, explosionRadius, explodableLayers);
        foreach (Collider hit in colliders)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                hitRb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius);
            }
        }
    }

    protected virtual void PerformSplit()
    {
        if (splitProjectilePrefabs == null || splitProjectilePrefabs.Length == 0) return;
        for (int i = 0; i < numberOfSplits; i++)
        {
            if (i >= splitProjectilePrefabs.Length) continue;
            float angle = (i - (numberOfSplits - 1) / 2.0f) * (splitSpreadAngle / (numberOfSplits > 1 ? numberOfSplits - 1 : 1));
            Quaternion rotation;
            Vector3 currentVelocity = rb != null ? rb.linearVelocity : transform.forward; // Use linearVelocity if available
            if (currentVelocity.magnitude < 0.1f)
            {
                rotation = Quaternion.AngleAxis(angle, transform.up) * transform.rotation;
            }
            else
            {
                Vector3 spreadAxis = Vector3.Cross(currentVelocity.normalized, Vector3.up);
                if (spreadAxis.sqrMagnitude < 0.01f) spreadAxis = Vector3.Cross(currentVelocity.normalized, Vector3.right); // Fallback if velocity is vertical
                if (spreadAxis.sqrMagnitude < 0.01f) spreadAxis = Vector3.up; // Further fallback

                rotation = Quaternion.AngleAxis(angle, spreadAxis.normalized) * Quaternion.LookRotation(currentVelocity.normalized);
            }

            GameObject splitInstance = Instantiate(splitProjectilePrefabs[i], transform.position, rotation);
            Projectile splitProjectileScript = splitInstance.GetComponent<Projectile>();
            Rigidbody splitRb = splitInstance.GetComponent<Rigidbody>();

            if (splitRb != null && rb != null)
            {
                splitRb.linearVelocity = rotation * Vector3.forward * currentVelocity.magnitude * 0.8f;
            }
            if (splitProjectileScript != null)
            {
                splitProjectileScript.NotifyLaunched();
            }
        }
    }

    protected virtual void PerformSpeedBoost()
    {
        if (rb != null)
        {
            // Use current velocity direction for boost
            rb.AddForce(rb.linearVelocity.normalized * speedBoostMultiplier * rb.mass, ForceMode.Impulse); // More consistent boost
        }
    }

    protected virtual void HandlePierce(GameObject collidedObject)
    {
        if (currentPierces < maxPierces)
        {
            currentPierces++;
            // Projectile continues, glide continues if active
        }
        else
        {
            powerActivated = true; // Max pierces reached, effectively "used up" this power aspect
            DeactivateGlideAndFollowIfNeeded(); // Stop gliding
            // Destroy(gameObject, 0.1f); // Optional: destroy after final pierce impact
        }
    }
}