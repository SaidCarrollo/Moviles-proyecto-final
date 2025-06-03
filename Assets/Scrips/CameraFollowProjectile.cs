using UnityEngine;

public class CameraFollowProjectile : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public float smoothSpeed = 5.0f;
    // AJUSTA ESTE OFFSET PARA UNA VISTA 2D (ej. más atrás en Z, y quizás más alto en Y)
    // Ejemplo para un juego donde la acción principal ocurre en el plano XY mundial:
    public Vector3 offset = new Vector3(0f, 5f, -15f); // Aumenta el valor negativo de Z

    [Header("Rotation Settings")]
    // Para un estilo Angry Birds, a menudo es mejor desactivar la rotación dinámica
    // o usar una rotación más simple.
    public bool lookAtTarget = false; // DESACTÍVALO para una cámara fija estilo 2D
    public float rotationSmoothSpeed = 5f;

    // Si lookAtTarget es true, pero quieres una rotación más simple (solo mirar al centro del target):
    public bool simpleLookAt = true;

    private bool isFollowing = false;
    private Quaternion initialRotation; // Para mantener la rotación inicial si lookAtTarget es false

    void Awake()
    {
        initialRotation = transform.rotation; // Guarda la rotación inicial
    }


    private void FixedUpdate()
    {
        if (target != null && isFollowing)
        {
            // --- Posicionamiento ---
            // El offset se aplica en el espacio del mundo directamente.
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // --- Rotación ---
            if (lookAtTarget)
            {
                Quaternion desiredRotation;
                if (simpleLookAt)
                {
                    // Simplemente mira hacia la posición del objetivo
                    desiredRotation = Quaternion.LookRotation(target.position - transform.position);
                }
                else
                {
                    // Lógica de rotación original (mirar en dirección de la velocidad)
                    Rigidbody targetRb = target.GetComponent<Rigidbody>();
                    if (targetRb != null && targetRb.linearVelocity.sqrMagnitude > 0.01f)
                    {
                        desiredRotation = Quaternion.LookRotation(targetRb.linearVelocity.normalized);
                    }
                    else
                    {
                        // Si no hay velocidad, mira hacia el 'forward' del objetivo o su posición
                        // Para 2D, mirar hacia la posición podría ser mejor incluso aquí
                        desiredRotation = Quaternion.LookRotation(target.position - transform.position);
                        // O Quaternion.LookRotation(target.forward);
                    }
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
            }
            else
            {
                // Si no estamos mirando al objetivo, mantenemos la rotación inicial o la que tenga la cámara.
                // Para un estilo Angry Birds estricto donde la cámara no rota, podrías usar:
                // transform.rotation = initialRotation;
                // O simplemente no hacer nada aquí si quieres que la cámara mantenga cualquier rotación
                // que se le haya dado en el editor y no la cambie dinámicamente.
                // Si la cámara fue orientada correctamente en el editor, no se necesita más.
            }
        }
    }
    public void StartFollowing(Transform newTarget)
    {
        target = newTarget;
        isFollowing = true;
        Debug.Log("Camera started following: " + newTarget.name);
    }

    public void StopFollowing()
    {
        isFollowing = false;
        Debug.Log("Camera stopped following.");
        // target = null; // Opcional
    }
}