using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    //PlayerAction Manager
    [SerializeField] private InputActionAsset PlayerControls;
    public InputAction moveAction;
    public InputAction lookAction;
    public InputAction jumpAction;
    public InputAction sprintAction;
    public InputAction crouchAction;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private Camera mainCamera;
    private CharacterController characterController;
    private float upDownRange = 80.0f;
    private Vector3 currentMovement = Vector3.zero;
    private bool isMoving;

    //Movement speed
   [SerializeField] private float sprintMultiplier = 2.0f;
    [SerializeField] private float walkSpeed = 3.0f;
    //Jump
    [SerializeField] private float jumpForce = 5.0f;
    [SerializeField] private float gravity = 9.81f;


    private Vector2 movementVector;
    private float movementX;
    private float movementY;

    public float mouseSensitivity = 2f;
    private float verticalRotation = 0f;

    [SerializeField] float crouchHeight = 1f;
    public float courchTransitionSpeed = 10f;
    float standingHeight = 1.8f;
    private float cameraOffset = 0.4f;
    public float Height
    {
        get => characterController.height;
        set => characterController.height = value;

    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        // Gameplay -> Player
        moveAction = PlayerControls.FindActionMap("Player").FindAction("Move");
        lookAction = PlayerControls.FindActionMap("Player").FindAction("Look");
        sprintAction = PlayerControls.FindActionMap("Player").FindAction("Sprint");
        jumpAction = PlayerControls.FindActionMap("Player").FindAction("Jump");
        crouchAction = PlayerControls.FindActionMap("Player").FindAction("Crouch");

        moveAction.performed += context => moveInput = context.ReadValue<Vector2>();
        moveAction.canceled += context => moveInput = Vector2.zero;

        lookAction.performed += context => lookInput = context.ReadValue<Vector2>();
        lookAction.canceled += context => lookInput = Vector2.zero;
    }

    private void Start()
    {
        standingHeight = characterController.height;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        sprintAction.Enable();
        jumpAction.Enable();
      
    }
    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        sprintAction.Disable();
        jumpAction.Disable();
    }

    void MovementHandle()
    {
        float speedMultiplier = sprintAction.ReadValue<float>() > 0 ? sprintMultiplier : 1f;
        float verticalSpeed = moveInput.y * walkSpeed * speedMultiplier;
        float horizontalSpeed = moveInput.x * walkSpeed * speedMultiplier;

        Vector3 horizontalMovement = new Vector3(horizontalSpeed, 0, verticalSpeed);   
        horizontalMovement = transform.rotation * horizontalMovement;

        HandleGravityAndJumping();

        currentMovement.x = horizontalMovement.x;
        currentMovement.z = horizontalMovement.z;
        
        characterController.Move(currentMovement * Time.deltaTime);
        isMoving = moveInput.y != 0 || moveInput.x != 0;
    }

    void HandleGravityAndJumping()
    {
        if (characterController.isGrounded)
        {
            currentMovement.y = -0.5f;
            if (jumpAction.triggered)
            {
                currentMovement.y = jumpForce;

            }
        }
        else
        {
            currentMovement.y -= gravity * Time.deltaTime;
        }

    }
    void HandleRotation()
    {
        float mouseXRotation = lookInput.x * mouseSensitivity;
        transform.Rotate(0, mouseXRotation, 0);

        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation =Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);


        
    }

    void Crouch()
    {
        var isTryingToCrouch = crouchAction.ReadValue<float>() > 0;
        var heightTarget = isTryingToCrouch ? crouchHeight : standingHeight;
        characterController.height = heightTarget;
    }

   void HandleCrouch()
    {
        Height = characterController.height;
        if(Mathf.Abs(Height - crouchHeight)< 0.01f)
        {
            characterController.height = Height;

        }

        var newHeight = Mathf.Lerp(Height, crouchHeight, courchTransitionSpeed* Time.deltaTime);
        characterController.height = newHeight;

        characterController.center = Vector3.up * (newHeight*0.5f);
        var cameraTargetPosition = mainCamera.transform.localPosition;
        cameraTargetPosition.y = crouchHeight - cameraOffset;

        mainCamera.transform.localPosition = Vector3.Lerp(mainCamera.transform.localPosition, cameraTargetPosition, courchTransitionSpeed* Time.deltaTime);



    }

    // Update is called once per frame
    void Update()
    {
        MovementHandle();
        HandleRotation();
        HandleCrouch();


    }
    private void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();
        movementX = movementVector.x;
        movementY = movementVector.y;
    }

    private void FixedUpdate()
    {
      
    }
}
