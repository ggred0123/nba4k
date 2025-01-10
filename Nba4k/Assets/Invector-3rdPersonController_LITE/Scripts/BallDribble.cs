using UnityEngine;

public class BallDribble : MonoBehaviour
{
    [Header("References")]
    public Transform player;           // 플레이어 Transform
    public Transform rightHand;        // 드리블 시 공을 잡을 손 위치
    public Rigidbody ballRigidbody;    // 농구공 Rigidbody

    [Header("Base Dribble Settings")]
    public float catchHeight = 1f;         // 공이 어느 높이 이상 올라가지 않도록 제한 (손 높이)
    public float maxDribbleHeight = 1.3f;  // 공이 올라갈 수 있는 최대 높이 (약간 여유)
    public float maxDistance = 1.5f;       // 플레이어와 공 사이 최대 거리(걷는 상황 기준)
    
    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.1f; // 공이 바닥에 닿았는지 확인할 체크 반경

    [Header("Movement Settings")]
    public float walkDribbleForce = 5f;      // 걷기 드리블 힘
    public float runDribbleForce = 8f;       // 달리기 드리블 힘
    public float walkBounceInterval = 0.25f; // 걷기 드리블 간격
    public float runBounceInterval = 0.15f;  // 달리기 드리블 간격
    public float runMaxDistance = 2f;        // 달릴 때 플레이어와 공 사이 거리
    
    [Header("Limits")]
    public float maxVelocity = 10f; // 공의 최대 속도 제한

    [Header("Hand Hold Settings")]
    public float holdBallInHandDuration = 0.3f;  // 손에 붙어 있는 시간(초)

    // 내부 동작 변수
    private CharacterController playerController;
    private Vector3 lastPlayerPosition;
    private float playerSpeed;
    private float upwardForce;    // 현재 드리블 힘(걷기/달리기 구분)
    private float bounceInterval; // 현재 드리블 간격
    private float nextBounceTime; // 다음 바운스 가능한 시간

    private bool isDribbling = true;
    private bool isMovingToHand = false;

    private Animator animator;

    // 손 안에 머무는 시간 체크용
    private float holdTimer = 0f; 

    void Start()
    {
        if (ballRigidbody == null)
            ballRigidbody = GetComponent<Rigidbody>();

        playerController = player.GetComponent<CharacterController>();
        lastPlayerPosition = player.position;
        animator = player.GetComponent<Animator>(); // 플레이어에 Animator 있다고 가정

        // 초기화
        isDribbling = true;
        isMovingToHand = false;
    }

    void FixedUpdate()
    {
        UpdatePlayerSpeed();
        AdjustDribbleParameters(); // 걷기/달리기 구분에 따라 Force, Interval, 거리 조절

        if (isDribbling)
        {
            LimitBallHeight();           
            if (CheckCatchCondition())    
            {
                // 손으로 가져오기 상태 시작
                isDribbling = false;
                isMovingToHand = true;
                ballRigidbody.useGravity = false;
                ballRigidbody.linearVelocity = Vector3.zero;
                holdTimer = 0f; // 손에 머무는 시간 타이머 리셋
            }
            else
            {
                HandleBallPosition();     
                CheckGroundAndBounce();   
            }
        }
        else if (isMovingToHand)
        {
            MoveToHand(); // 손 위치로 이동 및 일정 시간 유지
        }
    }

    #region Player Speed & Dribble Parameter
    void UpdatePlayerSpeed()
    {
        float distanceMoved = Vector3.Distance(player.position, lastPlayerPosition);
        playerSpeed = distanceMoved / Time.fixedDeltaTime;
        lastPlayerPosition = player.position;
    }

    void AdjustDribbleParameters()
    {
        float speedThreshold = 3f; // 걷기/달리기 구분

        if (playerSpeed > speedThreshold)
        {
            upwardForce = runDribbleForce;
            bounceInterval = runBounceInterval;
            maxDistance = runMaxDistance;
        }
        else
        {
            upwardForce = walkDribbleForce;
            bounceInterval = walkBounceInterval;
            maxDistance = 1.5f;
        }
    }
    #endregion

    #region Dribbling Core Logic
    void LimitBallHeight()
    {
        // 공이 너무 높게 올라가면 위치/속도 제한
        if (transform.position.y > maxDribbleHeight)
        {
            Vector3 pos = transform.position;
            pos.y = maxDribbleHeight;
            ballRigidbody.MovePosition(pos);

            Vector3 vel = ballRigidbody.linearVelocity;
            if (vel.y > 0)
                vel.y = 0f;
            ballRigidbody.linearVelocity = vel;
        }
    }

    bool CheckCatchCondition()
    {
        // 공이 catchHeight 부근까지 올라왔고, 내려오는 중이라면 손으로 가져오기
        return (transform.position.y >= catchHeight - 0.05f &&
                ballRigidbody.linearVelocity.y < 0);
    }

    void HandleBallPosition()
    {
        // 플레이어 앞(전방) + 약간 측면 흔들림
        float forwardOffset = maxDistance * 0.7f + (playerSpeed * 0.1f);
        Vector3 targetPosition = player.position + player.forward * forwardOffset;
        targetPosition.y = transform.position.y; 

        float distance = Vector3.Distance(
            new Vector3(player.position.x, 0, player.position.z),
            new Vector3(transform.position.x, 0, transform.position.z)
        );

        // 너무 멀어지면 Lerp로 보정
        if (distance > maxDistance)
        {
            float lerpSpeed = (playerSpeed > 3f) ? 10f : 5f;
            Vector3 newPos = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * lerpSpeed);
            ballRigidbody.MovePosition(newPos);
        }

        // 옆으로 살짝 흔들리는 움직임
        if (playerSpeed > 0.1f)
        {
            float sideOffset = Mathf.Sin(Time.time * playerSpeed) * 0.1f;
            Vector3 sideMovement = player.right * sideOffset;
            ballRigidbody.MovePosition(transform.position + sideMovement);
        }
    }

    void CheckGroundAndBounce()
    {
        bool isGrounded = Physics.CheckSphere(transform.position, groundCheckRadius, groundLayer);

        if (isGrounded && Time.time >= nextBounceTime)
        {
            // y속도를 0으로(바닥에 닿는 순간)
            Vector3 vel = ballRigidbody.linearVelocity;
            vel.y = 0f;
            ballRigidbody.linearVelocity = vel;

            float heightDiff = catchHeight - transform.position.y; 
            float speedMultiplier = 1f + (playerSpeed * 0.1f);
            float adjustedForce = upwardForce * (1 + heightDiff * 0.5f) * speedMultiplier;

            // 위로 튕기는 힘
            ballRigidbody.AddForce(Vector3.up * adjustedForce, ForceMode.Impulse);

            // 전방으로 살짝 힘
            if (playerSpeed > 0.1f)
            {
                float forwardPush = playerSpeed * 0.3f; 
                ballRigidbody.AddForce(player.forward * forwardPush, ForceMode.Impulse);
            }

            nextBounceTime = Time.time + bounceInterval;
        }

        // 공의 최대 속도 제한
        if (ballRigidbody.linearVelocity.magnitude > maxVelocity)
        {
            ballRigidbody.linearVelocity = Vector3.ClampMagnitude(ballRigidbody.linearVelocity, maxVelocity);
        }
    }
    #endregion

    #region Move Ball To Hand
    void MoveToHand()
    {
        // 손까지 서서히 이동
        float moveSpeed = 15f; // 손쪽으로 당기는 속도
        Vector3 newPos = Vector3.Lerp(transform.position, rightHand.position, Time.fixedDeltaTime * moveSpeed);
        ballRigidbody.MovePosition(newPos);

        // 손에 충분히 가까워졌다면, 일정 시간 버티다가(holdTimer) 다시 떨굼
        float distance = Vector3.Distance(transform.position, rightHand.position);

        if (distance < 0.1f)
        {
            // 손에 붙어있는 시간 측정
            holdTimer += Time.fixedDeltaTime;

            // 붙어있는 동안, 공을 정확히 손 위치로 고정하고 싶으면 아래처럼:
            // ballRigidbody.MovePosition(rightHand.position);

            if (holdTimer >= holdBallInHandDuration)
            {
                // 손에 일정 시간 머문 후 드리블 재시작
                StartDribbleDown();
            }
        }
        else
        {
            // 아직 손에 가까워지지 않았다면, 시간 카운트 리셋
            holdTimer = 0f;
        }
    }

    void StartDribbleDown()
    {
        StartDribbleAnimation(); // 애니메이터 Bool On
        isMovingToHand = false;
        isDribbling = true;
        ballRigidbody.useGravity = true;

        // 아래쪽으로 약간의 초기 속도
        ballRigidbody.linearVelocity = Vector3.down * 5f;

        // 다음 바운스 가능 시간 업데이트
        nextBounceTime = Time.time + bounceInterval;
    }
    #endregion

    #region Animation Helpers
    void StartDribbleAnimation()
    {
        animator.SetBool("IsDribbling", true);
    }

    void StopDribbleAnimation()
    {
        animator.SetBool("IsDribbling", false);
    }
    #endregion

    #region Public API
    // 외부에서 드리블 재시작을 위한 메서드
    public void RestartDribble()
    {
        ballRigidbody.isKinematic = false;
        ballRigidbody.useGravity = true;
        isDribbling = true;
        isMovingToHand = false;
        holdTimer = 0f;
    }
    #endregion
}