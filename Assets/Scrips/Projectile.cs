using UnityEngine;
using UnityEngine.InputSystem; 

public class Projectile : MonoBehaviour
{
    [Header("Configuracion del Poder")]
    public ProjectilePowerType powerType = ProjectilePowerType.Normal;
    public bool powerRequiresTap = false; 
    public GameObject effectOnActivatePrefab; 
    public AudioClip soundOnActivate;    

    [Header("Parametros Especificos del Poder")]
    // Para ExplodeOnImpact
    public float explosionRadius = 2f;
    public float explosionForce = 500f;
    public LayerMask explodableLayers; 

    // Para SplitOnTap
    public GameObject[] splitProjectilePrefabs; 
    public int numberOfSplits = 3;
    public float splitSpreadAngle = 30f; 

    // Para SpeedBoostOnTap
    public float speedBoostMultiplier = 1.5f;

    // Para PierceThrough
    public int maxPierces = 1;
    private int currentPierces = 0;


    // Estado interno
    protected Rigidbody rb;
    protected bool isLaunched = false;
    protected bool powerActivated = false;
    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogError("El proyectil necesita un Rigidbody.", this);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    public virtual void NotifyLaunched()
    {
        isLaunched = true;
        powerActivated = false; 
        currentPierces = 0;     
    }

    protected virtual void Update()
    {
        if (isLaunched && !powerActivated && powerRequiresTap)
        {
            // Escuchar un toque en pantalla para activar el poder
            bool tapOccurred = false;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                tapOccurred = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) // Fallback para Editor
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
        if (!isLaunched || (powerRequiresTap && !powerActivated)) // Si requiere tap y no se ha activado, el impacto no hace nada especial
        {
            // Si un poder de toque no se usa, podria tener un efecto de impacto normal o ninguno.
            // Si es ExplodeOnImpact, Se debe activarse aqu�.
        }

        if (powerActivated && powerType != ProjectilePowerType.PierceThrough) return; 


        // Logica de impacto basada en el tipo de poder
        switch (powerType)
        {
            case ProjectilePowerType.ExplodeOnImpact:
                if (!powerActivated) // Solo explotar una vez
                {
                    ActivatePower(collision.contacts[0].point);
                }
                break;
            case ProjectilePowerType.PierceThrough:
                HandlePierce(collision.gameObject);
                break;
            case ProjectilePowerType.Normal:
            default:
                // Logica de impacto normal (dañar el objeto, destruirse, etc.)
                // Debug.Log(gameObject.name + " impacto con " + collision.gameObject.name);
                // Desactivar el proyectil o destruirlo despues de un impacto normal
                // gameObject.SetActive(false); para futuro object pooling
                break;
        }
    }

    // Metodo principal para activar el poder. Puede ser llamado por toque o por impacto.
    public virtual void ActivatePower(Vector3? activationPoint = null)
    {
        if (powerActivated || !isLaunched) return; // No activar si ya se uso o no ha sido lanzado

        // Debug.Log("Activando poder: " + powerType);
        powerActivated = true; // Marcar como activado para que no se repita

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

                break;
            case ProjectilePowerType.ExplodeOnImpact:
                PerformExplosion(activationPoint ?? transform.position);
                Destroy(gameObject); 
                break;
        }
    }

    protected virtual void PerformExplosion(Vector3 explosionCenter)
    {
        // Debug.Log("BOOM en " + explosionCenter);
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
        // Debug.Log("SPLIT!");
        if (splitProjectilePrefabs == null || splitProjectilePrefabs.Length == 0) return;

        for (int i = 0; i < numberOfSplits; i++)
        {
            if (i >= splitProjectilePrefabs.Length) continue; // No instanciar m�s de los prefabs disponibles

            float angle = (i - (numberOfSplits - 1) / 2.0f) * (splitSpreadAngle / (numberOfSplits > 1 ? numberOfSplits - 1 : 1));
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up) * transform.rotation; // Asume split en el plano XY
            if (rb.linearVelocity.magnitude < 0.1f)
            { // Si la velocidad es muy baja, usa la direcci�n del proyectil
                rotation = Quaternion.AngleAxis(angle, transform.up) * transform.rotation;
            }
            else
            {
                rotation = Quaternion.AngleAxis(angle, Vector3.Cross(rb.linearVelocity.normalized, Vector3.up)) * Quaternion.LookRotation(rb.linearVelocity.normalized);
            }


            GameObject splitInstance = Instantiate(splitProjectilePrefabs[i], transform.position, rotation);
            Projectile splitProjectileScript = splitInstance.GetComponent<Projectile>();
            Rigidbody splitRb = splitInstance.GetComponent<Rigidbody>();

            if (splitRb != null && rb != null)
            {
                splitRb.linearVelocity = rotation * Vector3.forward * rb.linearVelocity.magnitude * 0.8f; // Hereda algo de velocidad
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
            rb.AddForce(rb.linearVelocity.normalized * speedBoostMultiplier, ForceMode.VelocityChange); // Impulso mas directo
        }
    }

    protected virtual void HandlePierce(GameObject collidedObject)
    {
        //lOGICA DE ATRAVESADO SI ES QUE SE USA
        if (currentPierces < maxPierces)
        {

            currentPierces++;

        }
        else
        {
            // Debug.Log("Maximo de perforaciones alcanzado. Impacto final.");
            powerActivated = true; // Ya no puede perforar m�s
            // Destroy(gameObject, 0.1f); // Se destruye despu�s de un peque�o delay
        }
    }
}