using UnityEngine;
using UnityEngine.InputSystem; // Para el nuevo Input System

public class Slingshot : MonoBehaviour
{
    [Header("Configuración General")]
    public float maxStretch = 3.0f;
    public float launchForceMultiplier = 100f;
    public int totalProjectiles = 5; // Número total de lanzamientos permitidos
    public float timeToPrepareNext = 0.5f;

    [Header("Referencias Esenciales")]
    public Transform anchorPoint;        // Punto de anclaje de la resortera
    public Transform spawnPoint;         // Punto donde aparece el nuevo proyectil

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
            if (objectCollider != null) // Si la resortera tiene collider, verificar toque/clic sobre ella
            {
                Ray ray = Camera.main.ScreenPointToRay(currentInputScreenPosition);
                RaycastHit hit;
                if (objectCollider.Raycast(ray, out hit, 200f))
                {
                    canStartDrag = true;
                }
            }
            else // Si no hay collider en la resortera, permitir arrastre si hay proyectil listo (comportamiento más permisivo)
            {
                canStartDrag = true;
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
        // primaryInputIsHeld se mantiene desde el frame anterior a menos que se suelte

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

            if (Mouse.current.leftButton.isPressed && !primaryInputStartedThisFrame)
            {
                primaryInputIsHeld = true;
            }

        }
        else
        {
            primaryInputIsHeld = false; // No hay input activo
        }
    }

    void PrepareNextProjectile()
    {
        if (currentSpringJoint != null) Destroy(currentSpringJoint);

        if (projectilesRemaining_TotalLaunches > 0)
        {
            // Seleccionar el prefab del proyectil actual de la secuencia
            GameObject prefabToSpawn = projectilePrefabs_TypeSequence[currentProjectileTypeIndex];

            Vector3 spawnPos = (spawnPoint != null) ? spawnPoint.position : anchorPoint.position;
            currentProjectile = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            currentProjectile.name = prefabToSpawn.name + "_Launch_" + (totalProjectiles - projectilesRemaining_TotalLaunches + 1);

            currentProjectileRb = currentProjectile.GetComponent<Rigidbody>();
            if (currentProjectileRb == null)
            {
                Debug.LogError("¡El prefab del proyectil '" + prefabToSpawn.name + "' no tiene Rigidbody! No se puede preparar.", this);
                Destroy(currentProjectile); // Limpiar instancia fallida
                enabled = false; // Detener el script para evitar más errores
                return;
            }
            currentProjectileRb.isKinematic = true;

            currentSpringJoint = currentProjectile.AddComponent<SpringJoint>();
            currentSpringJoint.connectedBody = anchorPoint.GetComponent<Rigidbody>();
            currentSpringJoint.spring = 50f; currentSpringJoint.damper = 5f; // Ajustar según necesidad
            currentSpringJoint.autoConfigureConnectedAnchor = false;
            currentSpringJoint.anchor = Vector3.zero;
            currentSpringJoint.connectedAnchor = Vector3.zero;

            projectilesRemaining_TotalLaunches--;
            // Debug.Log("Proyectil preparado. Lanzamientos restantes: " + projectilesRemaining_TotalLaunches);
        }
        else
        {
            currentProjectile = null; currentProjectileRb = null;
            Debug.Log("No quedan más lanzamientos.");
            // Aquí puedes añadir lógica para "Juego Terminado", etc.
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

        GameObject projectileToLaunch = currentProjectile; // Guardar referencia antes de limpiar

        currentProjectileRb.isKinematic = false;
        Vector3 launchDirection = anchorPoint.position - projectileToLaunch.transform.position;
        float stretchAmount = launchDirection.magnitude;
        currentProjectileRb.AddForce(launchDirection.normalized * stretchAmount * launchForceMultiplier);

        // Notificar al script del proyectil que ha sido lanzado (para activar sus poderes/lógica)
        Projectile projectileScript = projectileToLaunch.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.NotifyLaunched();
        }
        else
        {
            Debug.LogWarning("El proyectil lanzado '" + projectileToLaunch.name + "' no tiene un script 'Projectile.cs'. Sus poderes no se activarán.", projectileToLaunch);
        }

        // Limpiar referencias del Slingshot para el proyectil recién lanzado
        currentProjectile = null;
        currentProjectileRb = null;
        Destroy(currentSpringJoint);
        currentSpringJoint = null;

        // Avanzar al siguiente tipo de proyectil en la secuencia para el próximo lanzamiento
        currentProjectileTypeIndex++;
        if (currentProjectileTypeIndex >= projectilePrefabs_TypeSequence.Length)
        {
            currentProjectileTypeIndex = 0; // Volver al inicio de la secuencia
        }

        // Preparar el siguiente proyectil si aún quedan lanzamientos
        if (projectilesRemaining_TotalLaunches >= 0) // >= 0 porque el decremento ya se hizo en PrepareNext
        {
            Invoke("PrepareNextProjectile", timeToPrepareNext);
        }
        else
        {
            UpdateBandsVisuals(); // Asegurar que las bandas se oculten si no hay más
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
            bandLeft.SetPosition(0, anchorPoint.position);
            bandLeft.SetPosition(1, currentProjectile.transform.position);
            bandRight.SetPosition(0, anchorPoint.position);
            bandRight.SetPosition(1, currentProjectile.transform.position);
        }
    }

    Vector3 GetWorldPositionFromScreen(Vector2 screenPos)
    {
        Ray cameraRay = Camera.main.ScreenPointToRay(screenPos);
        Plane gamePlane; // El plano sobre el que se proyectará el input

        gamePlane = new Plane(Vector3.forward, new Vector3(0, 0, anchorPoint.position.z));

        float enterDistance;
        if (gamePlane.Raycast(cameraRay, out enterDistance))
        {
            return cameraRay.GetPoint(enterDistance);
        }
        else
        {
            if (Camera.main.orthographic)
            {
                Vector3 worldPoint = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Camera.main.WorldToScreenPoint(anchorPoint.position).z));
                worldPoint.z = anchorPoint.position.z; // Asegurar la Z correcta para ortográfica
                return worldPoint;
            }
            Debug.LogWarning("El rayo del input no intersectó el plano de juego. Usando profundidad de fallback basada en distancia al anchor.");
            return cameraRay.GetPoint(Vector3.Distance(Camera.main.transform.position, anchorPoint.position));
        }
    }
}