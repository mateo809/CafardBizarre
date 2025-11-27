using UnityEngine;

public class RotationExo : MonoBehaviour
{
    [SerializeField] private Transform center;
    [SerializeField] private float rotationSpeed = 5f;
    private void Update()
    {
        float x = transform.position.x * Mathf.Cos(rotationSpeed) + transform.position.y * -Mathf.Sin(rotationSpeed);
        float y = transform.position.x * Mathf.Sin(rotationSpeed) + transform.position.y * Mathf.Cos(rotationSpeed);
        transform.position = center.position + new Vector3(x, y);
    }
}
