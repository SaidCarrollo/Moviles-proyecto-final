using UnityEngine;

public class ProjectileGlideControl : MonoBehaviour
{
    [Header("Glide Physics Configuration")]
    public float liftCoefficient = 15f;         // Cuánta sustentación se genera. Ajusta según masa del Rigidbody.
    public float dragCoefficient = 0.5f;        // Resistencia del aire.
    public float angularDragForce = 2f;         // Resistencia a la rotación, ayuda a estabilizar.
    public float tiltToPitchSensitivity = 30f;  // Grados de pitch por unidad de inclinación del acelerómetro.
    public float pitchCorrectionTorque = 5f;    // Fuerza para corregir el pitch hacia el control del jugador.
    public float yawAlignmentTorque = 3f;       // Fuerza para alinear el yaw con la dirección de la velocidad.

    [Header("Glide Speed & Angle Limits")]
    public float minSpeedForLift = 3f;        // Velocidad mínima para una sustentación efectiva.
    public float maxPitchAngle = 60f;         // Máximo ángulo de cabeceo hacia arriba.
    public float minPitchAngle = -45f;        // Máximo ángulo de cabeceo hacia abajo (picada).

    [Header("Glide Constraints")]
    public float maxYPosition = 30f;
    public float minYPosition = 0.5f;         // Asumiendo que 0 es el suelo.
    public float rotationSmoothness = 5f;     // Para Slerp de rotación general (menos usado ahora).

    private Rigidbody rb;
    private bool isGliding = false;

    public bool IsGliding => isGliding;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("ProjectileGlideControl requires a Rigidbody on the projectile.", this);
            enabled = false;
            return;
        }
        // Asegúrate de que el Rigidbody tenga una masa y drag razonables por defecto.
        // El drag lineal del Rigidbody se puede poner a 0 si usamos nuestro propio dragForce.
        // rb.drag = 0; // Opcional, si nuestro dragCoefficient es el principal.
        enabled = false;
    }

    public void ActivateGlide(Vector3 launchVelocity)
    {
        if (rb == null) return;

        this.enabled = true;
        isGliding = true;
        rb.useGravity = true; // La gravedad DEBE estar activa.
        rb.linearVelocity = launchVelocity; // Velocidad inicial.

        // Orientación inicial (puede ser refinada)
        if (launchVelocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(launchVelocity.normalized); //
        }
        Debug.Log(gameObject.name + " Realistic Glide Control Activated (using forces). Velocity: " + launchVelocity);
    }

    void FixedUpdate()
    {
        if (!isGliding || rb == null) return;

        Vector3 currentVelocity = rb.linearVelocity;
        float speed = currentVelocity.magnitude;
        Vector3 velocityDirection = speed > 0.01f ? currentVelocity.normalized : transform.forward;

        // --- 1. Input del Jugador (Acelerómetro para Pitch) ---
        float tiltInput = Input.acceleration.y; // Inclinación arriba/abajo.

        // --- 2. Calcular Fuerzas Aerodinámicas ---
        // Sustentación (Lift)
        float speedFactor = Mathf.Clamp01(speed / minSpeedForLift); // Menos sustentación a baja velocidad.
        // La sustentación es proporcional al cuadrado de la velocidad y al ángulo de ataque (pitch).
        // Aquí, el 'pitch' del transform (su orientación local X) influye en transform.up.
        // Un modelo simplificado: la sustentación es mayormente hacia el 'arriba' local del ave.
        float liftPower = liftCoefficient * speed * speedFactor; // o speed * speed
        Vector3 liftForce = transform.up * liftPower;
        rb.AddForce(liftForce);

        // Resistencia (Drag)
        Vector3 dragForce = -velocityDirection * dragCoefficient * speed * speed; //
        rb.AddForce(dragForce);

        // --- 3. Control de Rotación (Torque) ---
        // Pitch Control (Cabeceo basado en input)
        float targetPitch = Mathf.Clamp(tiltInput * tiltToPitchSensitivity, minPitchAngle, maxPitchAngle);
        float currentPitch = GetSignedEulerAngle(transform.eulerAngles.x);

        float pitchError = Mathf.DeltaAngle(currentPitch, targetPitch);
        Vector3 pitchTorque = transform.right * pitchError * pitchCorrectionTorque;
        rb.AddTorque(pitchTorque);

        // Yaw Control (Alinearse con la dirección de la velocidad horizontal)
        Vector3 velocityHorizontalDir = new Vector3(velocityDirection.x, 0, velocityDirection.z).normalized;
        if (velocityHorizontalDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetYawRotation = Quaternion.LookRotation(velocityHorizontalDir);
            Quaternion currentYawRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            float yawError = Quaternion.Angle(currentYawRotation, targetYawRotation); // Simplificado

            // Determinar si girar a la izquierda o derecha
            Vector3 localVelDir = transform.InverseTransformDirection(velocityHorizontalDir);
            float yawSign = Mathf.Sign(localVelDir.x);

            Vector3 yawTorque = transform.up * yawError * yawSign * yawAlignmentTorque * -1f; // -1f puede necesitar ajuste
            rb.AddTorque(yawTorque);
        }

        // Estabilización de Roll (Evitar que dé vueltas sobre sí mismo)
        float currentRoll = GetSignedEulerAngle(transform.eulerAngles.z);
        Vector3 rollTorque = -transform.forward * currentRoll * angularDragForce; // Corregir hacia roll = 0
        rb.AddTorque(rollTorque);


        // Limitar velocidad angular general para que no gire demasiado rápido
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, angularDragForce * 2f); // Ajustar multiplicador


        // --- 4. Límites de Altitud ---
        Vector3 currentPosition = rb.position;
        if (currentPosition.y > maxYPosition && rb.linearVelocity.y > 0)
        {
            rb.position = new Vector3(currentPosition.x, maxYPosition, currentPosition.z);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }
        else if (currentPosition.y < minYPosition && rb.linearVelocity.y < 0)
        {
            rb.position = new Vector3(currentPosition.x, minYPosition, currentPosition.z);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * -0.3f, rb.linearVelocity.z); // Pequeño rebote o parada
            // Opcionalmente, desactivar planeo si toca el suelo:
            // DeactivateGlide();
            // Projectile script podría manejar esto en OnCollisionEnter también.
        }
    }

    // Helper para obtener ángulos entre -180 y 180
    float GetSignedEulerAngle(float angle)
    {
        angle %= 360;
        if (angle > 180)
            return angle - 360;
        return angle;
    }

    public void DeactivateGlide()
    {
        if (!isGliding) return;

        isGliding = false;
        this.enabled = false;
        // La gravedad ya debería estar activa.
        // Puedes resetear el drag angular si lo modificaste mucho en el Rigidbody.
        // rb.angularDrag = originalAngularDrag;
        Debug.Log(gameObject.name + " Realistic Glide Control Deactivated (using forces).");
    }
}