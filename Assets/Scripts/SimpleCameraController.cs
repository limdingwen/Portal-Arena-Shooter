using UnityEngine;
using System.Collections;

/// <summary>
/// For testing purposes.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SimpleCameraController : MonoBehaviour
{
    [Tooltip("In m/s")]
    public float speed;
    [Tooltip("In angles/s")]
    public float rotateSpeed;

    private new Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void FixedUpdate()
    {
        // Rotate
        rigidbody.MoveRotation(
            Quaternion.Euler(0, rotateSpeed * Input.GetAxis("Horizontal") * Time.fixedDeltaTime, 0) * 
            rigidbody.rotation);

        // Move
        rigidbody.MovePosition(rigidbody.position +
            transform.forward * speed * Input.GetAxis("Vertical") * Time.fixedDeltaTime);
    }
}
