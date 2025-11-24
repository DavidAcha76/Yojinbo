using UnityEngine;

/// <summary>
/// Plataforma que puede desactivarse temporalmente (colisión + render).
/// Ojo: esto es local/host. Si quieres sync 100% en Fusion, hay que hacerlo Networked.
/// </summary>
public class DestructiblePlatform : MonoBehaviour
{
    [Tooltip("Tiempo por defecto que la plataforma permanece desactivada.")]
    public float defaultDisableTime = 10f;

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private bool _isDisabled;

    private void Awake()
    {
        // Cachear colliders y renderers para activarlos/desactivarlos rápido
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// Desactiva la plataforma durante 'time' segundos.
    /// </summary>
    public void DisableTemporarily(float time)
    {
        if (_isDisabled)
            return;

        if (time <= 0f)
            time = defaultDisableTime;

        StartCoroutine(DisableRoutine(time));
    }

    private System.Collections.IEnumerator DisableRoutine(float time)
    {
        _isDisabled = true;

        SetActiveState(false);

        yield return new WaitForSeconds(time);

        SetActiveState(true);

        _isDisabled = false;
    }

    private void SetActiveState(bool value)
    {
        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = value;
            }
        }

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = value;
            }
        }
    }
}
