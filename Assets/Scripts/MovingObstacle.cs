using UnityEngine;

namespace BiopunkRV
{
    /// <summary>
    /// Moves an obstacle left at a given speed. Destroys itself when off-screen.
    /// </summary>
    public class MovingObstacle : MonoBehaviour
    {
        public float speed = 15f;
        public float destroyX = -2000f;

        private void Update()
        {
            transform.position -= new Vector3(speed * Time.deltaTime, 0f, 0f);

            if (transform.position.x < destroyX)
            {
                Destroy(gameObject);
            }
        }
    }
}