using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts human input into player controller.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class HumanInput : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Angles per unit of mouse movement")]
    public Vector2 mouseSensitivity = new Vector2(1, 1);

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        playerController.RotateCamera(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity);
        playerController.Move(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")), Input.GetButton("Jump"));
        if (Input.GetButtonDown("Fire1")) playerController.Shoot();
    }
}
