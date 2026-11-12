using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Volumen de un lago. Si el jugador cae dentro, se le devuelve a su último punto
    /// seguro. Se usa SendMessage para no acoplar esta asamblea (Game.Terrain) al jugador.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LakeVolume : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            other.SendMessage("RespawnToSafePoint", SendMessageOptions.DontRequireReceiver);
        }
    }
}
