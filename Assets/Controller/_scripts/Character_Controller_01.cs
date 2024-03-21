using Assets.Joystick_Pack.Scripts.Joysticks;
using UnityEngine;

namespace Assets.Controller._scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class Player_Controller_01 : MonoBehaviour
    {

        [SerializeField]
        VariableJoystick joystick;

        [SerializeField]
        Canvas canvas;

        Rigidbody rB;

        [SerializeField]
        GameObject artAsset;

        [SerializeField]
        Transform targetSphere;

        [SerializeField, Range(0f, 100f)]
        float speed = 5f, speedBackward;

        [SerializeField, Range(0f, 100f)]
        float jumpHeight = 2f;

        [SerializeField, Range(0f, 100f)]
        float rotationSpeed = 5f;
        Vector3 m;

        [SerializeField]
        Animator a;


        private Vector3 smoothVelocity = Vector3.zero;
        public float smoothTime = 0.1f; // Adjust this value to control the smoothness of the animation



        [SerializeField]
        Transform cam;

        bool isGrounded;
        bool isMoving;
        bool isRotating;

        bool desiredJump;
        readonly float turnSmoothTime = 1f;
        float turnSmoothVelocity;

        void Awake()
        {
            canvas.gameObject.SetActive(true);
            rB = GetComponent<Rigidbody>();
            m = transform.position;
        }

        void Update()
        {
            HandlePlayerInput();

            if(a == null) return;
        
            if (isMoving)
            {
                a.SetBool("isMoving", true);
                SetSmoothProceduralAnimation(m);
            }
            else
            {
                a.SetBool("isMoving", false);
                // ResetProceduralAnimation();
            }
            // Debug.Log(isGrounded);
            // cC.Move(mDir.normalized * Time.deltaTime * speed);


        }

        void FixedUpdate()
        {
            if (desiredJump)
            {
                desiredJump = false;
                Jump();
            }
            Vector3 mDir = HandleRotation();

            // rB.MovePosition(transform.position + (mDir * Time.deltaTime * speed));
            Vector3 forwardForce = mDir * Time.deltaTime * speed;
            // rB.AddForce(forwardForce);
            mDir += HandleGravity();

            if (m.z < 0)
            {
                rB.AddForce(mDir * Time.deltaTime * speedBackward * 100);
            }
            else
            {
                rB.AddForce(mDir * Time.deltaTime * speed * 100);
            }

        }

        void HandlePlayerInput()
        {
            m.x = Input.GetAxis("Horizontal") + joystick.Direction.x;
            m.z = Input.GetAxis("Vertical") + joystick.Direction.y;
            desiredJump |= Input.GetButtonDown("Jump");
            // m.y = 2f;
            // m = Vector3.ClampMagnitude(m, 1f);
            targetSphere.transform.localPosition = m + new Vector3(0, 1, 0);

            float isMovingStop = .3f;
            float isRotatingStop = .1f;

            // if (m.z < -.3f) { isRotatingStop = .8f; }

            isMoving = m.z > isMovingStop || m.z < -isMovingStop;
            isRotating = m.x > isRotatingStop || m.x < -isRotatingStop;
        }

        public void OnJumpButtonPress()
        {
            Debug.Log("Jump");
            desiredJump = true;
        }

        public void OnCloseButtonPress()
        {
            Application.Quit();
        }


        Vector3 HandleRotation()
        {
            if (isRotating)
            {
                // Quaternion targetRotation = Quaternion.LookRotation(m, Vector3.up);
                // transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                transform.Rotate(Vector3.up, m.x * rotationSpeed * Time.deltaTime);

            }

            if (isMoving)
            {
                float tragetAngle = Mathf.Atan2(m.x, m.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, tragetAngle, ref turnSmoothVelocity, turnSmoothTime);

                // transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 mDir = Quaternion.Euler(0f, tragetAngle, 0f) * Vector3.forward;
                // Debug.Log("Moooov");
                Debug.Log(mDir);
                return mDir;

            }
            return Vector3.zero;
        }

        void Jump()
        {
            if (isGrounded)
            {
                Vector3 v = rB.velocity;
                v.y += Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
                rB.velocity = v;
            }


        }
        Vector3 HandleGravity()
        {
            if (isGrounded)
            {
                return new Vector3(0, -0.1f, 0);
            }
            else
            {
                Debug.Log("Big Force");
                return new Vector3(0, -1f, 0);
            }
        }

        void OnCollisionExit(Collision c)
        {
            isGrounded = false;
        }


        void OnCollisionEnter(Collision c)
        {
            EvaluateCollision(c);
            Debug.Log("enter");
        }

        void OnCollisionStay(Collision c)
        {
            EvaluateCollision(c);
            // Debug.Log("Stay");
        }
        void EvaluateCollision(Collision c)
        {
            for (int i = 0; i < c.contactCount; i++)
            {
                Vector3 normal = c.GetContact(i).normal;
                isGrounded |= normal.y >= 0.9f;

            }
        }

        void SetProceduralAnimation(Vector3 movementInput)
        {
            if (artAsset == null) return;
            Vector3 r = artAsset.transform.localEulerAngles;
            r.x = movementInput.z * 5;
            r.y = movementInput.x * 5f;
            r.z = -movementInput.x * 5;
            artAsset.transform.localEulerAngles = r;
        }

        void SetSmoothProceduralAnimation(Vector3 movementInput)
        {
            if (artAsset == null) return;

            float targetRotationX = movementInput.z * 5f;
            float targetRotationY = movementInput.x * 5f;
            float targetRotationZ = -movementInput.x * 5f;

            Vector3 currentRotation = artAsset.transform.localEulerAngles;
            float smoothVelocityX = 0f;
            float smoothVelocityY = 0f;
            float smoothVelocityZ = 0f;

            // Smoothly interpolate each angle component
            float smoothedRotationX = Mathf.SmoothDampAngle(currentRotation.x, targetRotationX, ref smoothVelocityX, smoothTime);
            float smoothedRotationY = Mathf.SmoothDampAngle(currentRotation.y, targetRotationY, ref smoothVelocityY, smoothTime);
            float smoothedRotationZ = Mathf.SmoothDampAngle(currentRotation.z, targetRotationZ, ref smoothVelocityZ, smoothTime);

            // Apply the smoothed rotation to the art asset
            artAsset.transform.localEulerAngles = new Vector3(smoothedRotationX, smoothedRotationY, smoothedRotationZ);
        }


        void ResetProceduralAnimation()
        {
            artAsset.transform.localEulerAngles = new Vector3();
        }
    }
}
