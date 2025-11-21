using UnityEngine;
using Starter.Shooter;

/// <summary>
/// Proyectil del garfio. Se mueve hacia adelante, dibuja una cadena
/// con LineRenderer y, al impactar o llegar a la distancia máxima,
/// notifica al Player.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class GrappleProjectile : MonoBehaviour
{
    public Player Owner;
    public Transform Muzzle;      // punta de la pistola (inicio de la cadena)
    public float Speed = 40f;
    public float MaxDistance = 40f;
    public LayerMask HitMask;

    private Vector3 _direction;
    private Vector3 _startPos;
    private LineRenderer _line;
    private bool _initialized;
    private bool _finishedNotified;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        if (_line != null)
        {
            _line.positionCount = 2;
            _line.useWorldSpace = true;

            _line.startWidth = 0.05f;
            _line.endWidth = 0.05f;
        }

        _startPos = transform.position;
    }

    /// <summary>
    /// Inicializa el proyectil con datos del Player.
    /// </summary>
    public void Init(Player owner, Transform muzzle, Vector3 direction, float speed, float maxDistance, LayerMask hitMask)
    {
        Owner = owner;
        Muzzle = muzzle;
        Speed = speed;
        MaxDistance = maxDistance;
        HitMask = hitMask;

        // Dirección forzada por la cámara del jugador
        _direction = direction.normalized;
        _startPos = transform.position;

        // Si no se configuró mascara, usamos la default
        if (HitMask == 0)
        {
            HitMask = Physics.DefaultRaycastLayers;
        }

        _initialized = true;
    }

    private void Update()
    {
        if (_initialized == false)
            return;

        float step = Speed * Time.deltaTime;

        // Raycast corto para detectar impacto en este frame
        if (Physics.Raycast(transform.position, _direction, out var hit, step, HitMask))
        {
            transform.position = hit.point;

            if (Owner != null)
            {
                // Notificamos que sí hubo impacto
                Owner.StartGrapple(hit.point);
                Owner.OnGrappleProjectileFinished(true);
            }

            UpdateLine();
            _finishedNotified = true;
            Destroy(gameObject);
            return;
        }

        // Avanzar
        transform.position += _direction * step;

        // Si nos pasamos de distancia, destruir sin enganchar
        float traveled = Vector3.Distance(_startPos, transform.position);
        if (traveled >= MaxDistance)
        {
            if (Owner != null)
            {
                Owner.OnGrappleProjectileFinished(false);
            }

            _finishedNotified = true;
            Destroy(gameObject);
            return;
        }

        UpdateLine();
    }

    private void UpdateLine()
    {
        if (_line == null)
            return;

        if (Muzzle != null)
        {
            _line.SetPosition(0, Muzzle.position);
        }
        else
        {
            _line.SetPosition(0, transform.position);
        }

        _line.SetPosition(1, transform.position);
    }

    private void OnDestroy()
    {
        // Fallback por si se destruye por otra razón
        if (!_finishedNotified && Owner != null)
        {
            Owner.OnGrappleProjectileFinished(false);
        }
    }
}
