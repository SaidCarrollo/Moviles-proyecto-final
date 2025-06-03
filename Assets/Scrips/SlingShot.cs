using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections; 

public class Slingshot : MonoBehaviour
{
    [Header("Configuración General")]
    public float maxStretch = 3.0f;
    public float launchForceMultiplier = 100f;
    public int totalProjectiles = 5; // Número total de lanzamientos permitidos
    public float timeToPrepareNext = 0.5f;
    // NUEVA VARIABLE: Retraso para activar el planeo
    [Header("Configuración de Planeo")]
    public float glideActivationDelay = 3.0f; // Segundos antes de que se active el planeo

    [Header("Referencias Esenciales")]
    public Transform anchorPoint;       // Punto de anclaje de la resortera
    public Transform spawnPoint;        // Punto donde aparece el nuevo proyectil

    [Header("Gestión de Tipos de Proyectiles")]
    public GameObject[] projectilePrefabs_TypeSequence; // Asigna tus diferentes prefabs de proyectil aquí
    private int currentProjectileTypeIndex = 0;         // Índice para ciclar por projectilePrefabs_TypeSequence

    [Header("Bandas Visuales (Opcional)")]
    public LineRenderer bandLeft;
    public LineRenderer bandRight;

    // Variables de estado del input
    private bool isDragging = false;
    private bool primaryInputStartedThisFrame = false;
    private bool primaryInputIsHeld = false;
    private Vector2 currentInputScreenPosition;

    // Variables del proyectil actual
    private GameObject currentProjectile;
    private Rigidbody currentProjectileRb;
    private SpringJoint currentSpringJoint;
    private int projectilesRemaining_TotalLaunches; // Contador para el total de lanzamientos

    // Referencia al collider de este objeto (para iniciar el arrastre)
    private Collider objectCollider;

    // Referencia al script de la cámara para seguir al proyectil
    private CameraFollowProjectile cameraFollowScript;

    void Start()
    {
        objectCollider = GetComponent<Collider>();
        if (objectCollider == null)
        {
            Debug.LogWarning("Slingshot GameObject no tiene un Collider. La detección de inicio de arrastre podría no funcionar como se espera si se depende de un clic/toque directo sobre la resortera.");
        }

        // Validaciones críticas
        if (projectilePrefabs_TypeSequence == null || projectilePrefabs_TypeSequence.Length == 0)
        {
            Debug.LogError("¡ERROR! 'Projectile Prefabs_Type Sequence' no está asignado o está vacío en el Inspector del Slingshot.");
            enabled = false; return;
        }
        if (anchorPoint == null)
        {
            Debug.LogError("¡ERROR! El Anchor Point no está asignado en el Inspector.");
            enabled = false; return;
        }
        if (anchorPoint.GetComponent<Rigidbody>() == null)
        {
            Debug.LogError("¡ERROR! El 'anchorPoint' DEBE tener un componente Rigidbody (puede ser Kinematic).");
            enabled = false; return;
        }
        if (spawnPoint == null)
        {
            Debug.LogWarning("Spawn Point no asignado. Usando anchorPoint.position como punto de aparición.");
        }

        // Obtener o agregar el script de seguimiento de la cámara
        if (Camera.main != null)
        {
            cameraFollowScript = Camera.main.GetComponent<CameraFollowProjectile>();
            if (cameraFollowScript == null)
            {
                Debug.LogWarning("Main Camera no tiene CameraFollowProjectile script. Agregando uno.");
                cameraFollowScript = Camera.main.gameObject.AddComponent<CameraFollowProjectile>();
            }
        }
        else
        {
            Debug.LogError("¡No se encontró Main Camera en la escena!");
            enabled = false; return;
        }

        projectilesRemaining_TotalLaunches = totalProjectiles;
        PrepareNextProjectile();
    }

    void Update()
    {
        ProcessInputs();
        UpdateBandsVisuals();

        if (currentProjectile == null || currentProjectileRb == null || !currentProjectileRb.isKinematic)
        {
            if (isDragging) isDragging = false;
            return;
        }

        if (primaryInputStartedThisFrame && !isDragging)
        {
            bool canStartDrag = false;
            if (objectCollider != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(currentInputScreenPosition);
                RaycastHit hit;
                if (objectCollider.Raycast(ray, out hit, 200f))
                {
                    canStartDrag = true;
                }
            }
            else
            {
                canStartDrag = true; // Permitir arrastrar si no hay collider específico en la resortera (arrastre global)
            }

            if (canStartDrag)
            {
                isDragging = true;
            }
        }

        if (isDragging && primaryInputIsHeld)
        {
            DragCurrentProjectile(currentInputScreenPosition);
        }

        if (isDragging && !primaryInputIsHeld)
        {
            isDragging = false;
            ReleaseCurrentProjectile();
        }
    }

    void ProcessInputs()
    {
        primaryInputStartedThisFrame = false;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var primaryTouch = Touchscreen.current.primaryTouch;
            currentInputScreenPosition = primaryTouch.position.ReadValue();
            var touchPhase = primaryTouch.phase.ReadValue();

            if (touchPhase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                primaryInputStartedThisFrame = true;
                primaryInputIsHeld = true;
            }
            else if (touchPhase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                primaryInputIsHeld = true;
            }
            else if (touchPhase == UnityEngine.InputSystem.TouchPhase.Ended || touchPhase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                primaryInputIsHeld = false;
            }
        }
        else if (Mouse.current != null)
        {
            currentInputScreenPosition = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                primaryInputStartedThisFrame = true;
                primaryInputIsHeld = true;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                primaryInputIsHeld = false;
            }
            // Mantener primaryInputIsHeld si el botón sigue presionado pero no fue el frame en que se presionó
            else if (Mouse.current.leftButton.isPressed)
            {
                primaryInputIsHeld = true;
            }
        }
        else
        {
            primaryInputIsHeld = false;
        }
    }

    void PrepareNextProjectile()
    {
        if (currentSpringJoint != null) Destroy(currentSpringJoint);

        if (projectilesRemaining_TotalLaunches > 0)
        {
            GameObject prefabToSpawn = projectilePrefabs_TypeSequence[currentProjectileTypeIndex];
            Vector3 spawnPos = (spawnPoint != null) ? spawnPoint.position : anchorPoint.position;
            currentProjectile = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            currentProjectile.name = prefabToSpawn.name + "_Launch_" + (totalProjectiles - projectilesRemaining_TotalLaunches + 1);

            currentProjectileRb = currentProjectile.GetComponent<Rigidbody>();
            if (currentProjectileRb == null)
            {
                Debug.LogError("¡El prefab del proyectil '" + prefabToSpawn.name + "' no tiene Rigidbody! No se puede preparar.", this);
                Destroy(currentProjectile);
                enabled = false;
                return;
            }
            currentProjectileRb.isKinematic = true;

            currentSpringJoint = currentProjectile.AddComponent<SpringJoint>();
            currentSpringJoint.connectedBody = anchorPoint.GetComponent<Rigidbody>(); //
            currentSpringJoint.spring = 50f; currentSpringJoint.damper = 5f;
            currentSpringJoint.autoConfigureConnectedAnchor = false;
            currentSpringJoint.anchor = Vector3.zero;
            currentSpringJoint.connectedAnchor = Vector3.zero;

            projectilesRemaining_TotalLaunches--;
        }
        else
        {
            currentProjectile = null; currentProjectileRb = null;
            Debug.Log("No quedan más lanzamientos.");
        }
        UpdateBandsVisuals();
    }

    void DragCurrentProjectile(Vector2 screenPosition)
    {
        if (currentProjectile == null) return;
        Vector3 worldInputPos = GetWorldPositionFromScreen(screenPosition);
        Vector3 directionFromAnchor = worldInputPos - anchorPoint.position;

        if (directionFromAnchor.magnitude > maxStretch)
        {
            directionFromAnchor = directionFromAnchor.normalized * maxStretch;
        }
        currentProjectile.transform.position = anchorPoint.position + directionFromAnchor;
    }

    void ReleaseCurrentProjectile()
    {
        if (currentProjectileRb == null || currentSpringJoint == null) return;

        GameObject projectileToLaunch = currentProjectile;
        Rigidbody projectileRbToLaunch = currentProjectileRb;

        currentProjectileRb.isKinematic = false;
        Vector3 launchDirection = anchorPoint.position - projectileToLaunch.transform.position;
        float stretchAmount = launchDirection.magnitude;
        Vector3 launchForce = launchDirection.normalized * stretchAmount * launchForceMultiplier;
        currentProjectileRb.AddForce(launchForce);

        Projectile projectileScript = projectileToLaunch.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.NotifyLaunched(); //
        }
        else
        {
            Debug.LogWarning("El proyectil lanzado '" + projectileToLaunch.name + "' no tiene un script 'Projectile.cs'. Sus poderes no se activarán.", projectileToLaunch);
        }

        ProjectileGlideControl glideControl = projectileToLaunch.GetComponent<ProjectileGlideControl>(); //
        if (glideControl != null && cameraFollowScript != null)
        {
            StartCoroutine(ActivateGlideAndFollowDelayed(glideControl, projectileToLaunch.transform, projectileRbToLaunch)); //
        }
        else if (cameraFollowScript != null) // Si no es planeador, pero quieres seguirlo
        {
            cameraFollowScript.StartFollowing(projectileToLaunch.transform);
        }

        currentProjectile = null;
        currentProjectileRb = null;
        Destroy(currentSpringJoint);
        currentSpringJoint = null;

        currentProjectileTypeIndex++;
        if (currentProjectileTypeIndex >= projectilePrefabs_TypeSequence.Length)
        {
            currentProjectileTypeIndex = 0;
        }

        // La comprobación original era projectilesRemaining_TotalLaunches >= 0.
        // Si se decrementó antes de la comprobación, projectilesRemaining_TotalLaunches > 0 es para cuando aún quedan.
        // Y projectilesRemaining_TotalLaunches == 0 es para el último que se preparó.
        if (projectilesRemaining_TotalLaunches >= 0) // Correcto para verificar si aún se pueden preparar más (después de decrementar)
        {
            Invoke("PrepareNextProjectile", timeToPrepareNext); //
        }
        else
        {
            UpdateBandsVisuals();
        }
    }

    private IEnumerator ActivateGlideAndFollowDelayed(ProjectileGlideControl glideControl, Transform projectileTransform, Rigidbody projectileRb)
    {
        // Espera a que la física inicial del lanzamiento se aplique
        yield return new WaitForFixedUpdate(); //

        // Asegurarse de que los objetos aún existen (podrían ser destruidos por colisión temprana)
        if (projectileTransform == null || projectileRb == null || cameraFollowScript == null)
        {
            yield break; // Salir de la corrutina si algo falta
        }

        // Iniciar el seguimiento de la cámara inmediatamente
        cameraFollowScript.StartFollowing(projectileTransform); //

        // Esperar el tiempo definido antes de activar el planeo
        if (glideActivationDelay > 0)
        {
            yield return new WaitForSeconds(glideActivationDelay);
        }

        // Volver a asegurarse de que los objetos aún existen después del retraso
        if (projectileTransform != null && glideControl != null && projectileRb != null)
        {
            // Activar el control de planeo en el proyectil
            // Es importante usar la velocidad actual del Rigidbody en este punto,
            // ya que habrá seguido una trayectoria balística.
            glideControl.ActivateGlide(projectileRb.linearVelocity); //
        }
    }

    void UpdateBandsVisuals()
    {
        if (bandLeft == null || bandRight == null) return;
        bool showBands = currentProjectile != null && currentSpringJoint != null && currentProjectileRb != null && currentProjectileRb.isKinematic;
        bandLeft.enabled = showBands;
        bandRight.enabled = showBands;
        if (showBands)
        {
            bandLeft.SetPosition(0, anchorPoint.position); //
            bandLeft.SetPosition(1, currentProjectile.transform.position); //
            bandRight.SetPosition(0, anchorPoint.position); //
            bandRight.SetPosition(1, currentProjectile.transform.position); //
        }
    }

    Vector3 GetWorldPositionFromScreen(Vector2 screenPos)
    {
        Ray cameraRay = Camera.main.ScreenPointToRay(screenPos);
        // Asumimos que la resortera y el arrastre ocurren en un plano Z constante relativo al anchorPoint.
        // Esto es común para juegos 2.5D o 3D con mecánica de resortera estilo 2D.
        Plane gamePlane = new Plane(Vector3.forward, new Vector3(0, 0, anchorPoint.position.z)); //
        float enterDistance;

        if (gamePlane.Raycast(cameraRay, out enterDistance)) //
        {
            return cameraRay.GetPoint(enterDistance); //
        }
        else
        {
            // Fallback si el rayo es paralelo al plano (raro si la cámara no está mirando exactamente de lado)
            // o si la cámara es ortográfica y el cálculo del plano no funciona como se espera.
            if (Camera.main.orthographic)
            {
                // Para cámara ortográfica, convertir directamente de pantalla a mundo en el Z del anchor.
                Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Camera.main.WorldToScreenPoint(anchorPoint.position).z));
                worldPoint.z = anchorPoint.position.z; // Asegurar el Z correcto
                return worldPoint;
            }
            // Para cámara en perspectiva, si el raycast al plano falla, usar un fallback basado en la distancia.
            Debug.LogWarning("El rayo del input no intersectó el plano de juego definido por el anchor. Usando profundidad de fallback.");
            return cameraRay.GetPoint(Vector3.Distance(Camera.main.transform.position, anchorPoint.position)); //
        }
    }
}