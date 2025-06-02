
public enum ProjectilePowerType
{
    Normal,            // Sin poder especial, solo impacto
    ExplodeOnImpact,   // Explota al chocar con algo
    SplitOnTap,        // Se divide en varios proyectiles si el jugador toca la pantalla después de lanzarlo
    SpeedBoostOnTap,   // Obtiene un impulso de velocidad si el jugador toca la pantalla
    PierceThrough      // Atraviesa un número limitado de objetos débiles
}