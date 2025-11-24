using UnityEngine;

/// <summary>
/// Jaula esférica hueca que mantiene al jugador dentro de un radio interno.
/// - Se basa en un SphereCollider marcado como trigger.
/// - Empuja SIEMPRE al root del objeto objetivo hacia adentro del radio interno.
/// - Funciona con Rigidbody o con controlador tipo CharacterController/KCC (moviendo el transform).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class EsferaHuecaJaula : MonoBehaviour
{
    [Header("Configuración de la Jaula")]
    [Tooltip("Grosor del borde de la esfera en unidades de mundo (cascarón sólido).")]
    [Min(0.01f)]
    public float grosorBorde = 0.5f;

    [Tooltip("Capas de objetos que serán encarcelados por la jaula (pon aquí la capa del player).")]
    public LayerMask capasObjetivo;

    [Tooltip("Usar tag en vez de solo capas. Si está activo, solo atrapará objetos con este tag.")]
    public bool usarTagObjetivo = true;

    [Tooltip("Tag del jugador u objeto a encarcelar (ej: 'Player').")]
    public string tagObjetivo = "Player";

    [Header("Debug")]
    public bool debugLogs = true;

    // Referencias
    private SphereCollider _sphereCol;
    private Rigidbody _selfRb;

    // Geometría jaula en mundo
    private Vector3 _centroMundo;
    private float _radioExternoMundo;
    private float _radioInternoMundo;

    private const float EPSILON = 0.001f;

    private void Reset()
    {
        // Config por defecto al añadir el script
        capasObjetivo = LayerMask.GetMask("Player"); // Si no existe, ajustas en inspector
    }

    private void Awake()
    {
        _sphereCol = GetComponent<SphereCollider>();
        _selfRb = GetComponent<Rigidbody>();

        if (_sphereCol == null)
        {
            Debug.LogError("[EsferaHuecaJaula] No hay SphereCollider en este objeto.", this);
            enabled = false;
            return;
        }

        // Forzar trigger
        if (!_sphereCol.isTrigger)
        {
            _sphereCol.isTrigger = true;
            if (debugLogs) Debug.Log("[EsferaHuecaJaula] Forzando SphereCollider.isTrigger = true", this);
        }

        // Rigidbody de la jaula (solo para el trigger, no física real)
        _selfRb.isKinematic = true;
        _selfRb.useGravity = false;

        RecalcularGeometria();
    }

    private void Start()
    {
        RecalcularGeometria();
    }

    private void OnValidate()
    {
        if (grosorBorde < 0.01f) grosorBorde = 0.01f;

        var col = GetComponent<SphereCollider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (Application.isEditor && !Application.isPlaying)
        {
            _sphereCol = col;
            RecalcularGeometria();
        }
    }

    private void LateUpdate()
    {
        // Por si la jaula se mueve/escala
        RecalcularGeometria();
    }

    private void RecalcularGeometria()
    {
        if (_sphereCol == null) return;

        _centroMundo = transform.TransformPoint(_sphereCol.center);

        float maxScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z)
        );

        _radioExternoMundo = _sphereCol.radius * maxScale;

        float grosorMaximo = _radioExternoMundo * 0.9f;
        float grosorClampeado = Mathf.Clamp(grosorBorde, 0.01f, grosorMaximo);

        _radioInternoMundo = Mathf.Max(_radioExternoMundo - grosorClampeado, EPSILON);
    }

    private void OnTriggerStay(Collider other)
    {
        // 1) Filtrar por capa
        if (!EsCapaObjetivo(other.gameObject.layer))
            return;

        // 2) Filtrar por tag si está activado
        if (usarTagObjetivo && !other.CompareTag(tagObjetivo))
            return;

        if (debugLogs)
            Debug.Log($"[EsferaHuecaJaula] OnTriggerStay con {other.name}", this);

        // 3) Obtener el root real del objeto (muy importante para KCC / characters)
        Transform targetRoot = other.transform.root;

        // 4) Intentar usar un Rigidbody si el root lo tiene
        Rigidbody rbTarget = targetRoot.GetComponent<Rigidbody>();

        // Pos actual del objetivo (root, no el hijo collider)
        Vector3 posActual = rbTarget != null ? rbTarget.position : targetRoot.position;

        Vector3 dirDesdeCentro = posActual - _centroMundo;
        float distancia = dirDesdeCentro.magnitude;

        if (distancia < EPSILON)
            return;

        // Si ya está dentro del radio interno, no hacemos nada
        if (distancia <= _radioInternoMundo - EPSILON)
            return;

        // Clamp al radio interno
        Vector3 dirNormalizada = dirDesdeCentro / distancia;
        Vector3 posicionLimite = _centroMundo + dirNormalizada * _radioInternoMundo;

        if (rbTarget != null && !rbTarget.isKinematic)
        {
            // Si el jugador usa Rigidbody dinámico
            rbTarget.MovePosition(posicionLimite);
        }
        else
        {
            // Para CharacterController, KCC, etc. forzamos el transform root
            targetRoot.position = posicionLimite;
        }
    }

    private bool EsCapaObjetivo(int layer)
    {
        return (capasObjetivo.value & (1 << layer)) != 0;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_sphereCol == null)
            _sphereCol = GetComponent<SphereCollider>();

        if (_sphereCol == null) return;

        RecalcularGeometria();

        // Radio externo
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(_centroMundo, _radioExternoMundo);

        // Radio interno
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(_centroMundo, _radioInternoMundo);
    }
#endif
}
