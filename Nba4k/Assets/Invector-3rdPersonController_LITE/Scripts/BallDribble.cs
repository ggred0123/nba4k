using UnityEngine;
using UnityEngine.UI;

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

    [Header("Shoot Settings")]
    public Transform hoopTransform;      // 골대 Transform
    public float minShootForce = 5f;
    public float maxShootForce = 15f;
    public float maxChargeTime = 1.5f;
    public float shootArcHeight = 2f;
    private float shootChargeStartTime;  // 슛 차지 시작 시간
    private bool isChargingShot = false; // 슛 차지 중인지

    // 내부 동작 변수
    private CharacterController playerController;
    private Vector3 lastPlayerPosition;
    private float playerSpeed;
    private float upwardForce;    // 현재 드리블 힘(걷기/달리기 구분)
    private float bounceInterval; // 현재 드리블 간격
    private float nextBounceTime; // 다음 바운스 가능한 시간

    private bool isDribbling = true;
    private bool isMovingToHand = false;

    private ScoreManager scoreManager;
    private bool canScore = true; 

    private Animator animator;

    [Header("UI")]
    public Slider powerSlider;  

    [Header("Shoot Trajectory")]
    private float maxHeightOffset = 0.6f;  // 근거리 슛 시 최고점 보정값
    private float range;  

    // 손 안에 머무는 시간 체크용
    private float holdTimer = 0f; 

     void Update()
    {
        // 슛 입력 처리
        HandleShootInput();
        UpdatePowerUI();
    }

     void UpdatePowerUI()
    {
        if(powerSlider != null)
        {
            if(isChargingShot)
            {
                powerSlider.gameObject.SetActive(true);
                float chargeTime = Mathf.Min(Time.time - shootChargeStartTime, maxChargeTime);
                powerSlider.value = chargeTime / maxChargeTime;
            }
            else
            {
                powerSlider.gameObject.SetActive(false);
                powerSlider.value = 0;
            }
        }
    }


    private float CalcMaxHeight(Vector3 startPos, Vector3 targetPos)
    {
        // 지면상의 두 점 사이 거리 계산
        Vector3 direction = new Vector3(targetPos.x, 0f, targetPos.z) - 
                          new Vector3(startPos.x, 0f, startPos.z);
        range = direction.magnitude;
        
        // 공통 높이 (골대 높이 + 보정값)
        float maxYPos = targetPos.y + maxHeightOffset;
        
        // 45도 각도 유지를 위한 높이 조정
        if (range / 2f > maxYPos)
            maxYPos = range / 2f;
            
        return maxYPos;
    }

    private Vector3 CalculateShootVelocity(Vector3 startPos, Vector3 targetPos, float maxYPos)
{
    Vector3 newVel = new Vector3();

    // 최고점까지의 시간
    float timeToMax = Mathf.Sqrt(-2 * (maxYPos - startPos.y) / Physics.gravity.y);
    // 최고점에서 골인 지점까지의 시간
    float timeToTargetY = Mathf.Sqrt(-2 * (maxYPos - targetPos.y) / Physics.gravity.y);
    float totalFlightTime = timeToMax + timeToTargetY;

    // 지면상의 방향과 거리 계산
    Vector3 direction = new Vector3(targetPos.x, 0f, targetPos.z) - 
                      new Vector3(startPos.x, 0f, startPos.z);
    float range = direction.magnitude;  // 여기서 지역 변수로 range 계산
    Vector3 unitDirection = direction.normalized;
    
    // 수평 방향 속도
    float horizontalVelocityMagnitude = range / totalFlightTime;
    
    // 각 축의 속도 계산
    newVel.x = horizontalVelocityMagnitude * unitDirection.x;
    newVel.z = horizontalVelocityMagnitude * unitDirection.z;
    // 수직 방향 속도
    newVel.y = Mathf.Sqrt(-2.0f * Physics.gravity.y * (maxYPos - startPos.y));

    return newVel;
}

    void HandleShootInput()
    {
        // E키를 누르면 차지 시작
        if (Input.GetKeyDown(KeyCode.E))
        {
            animator.SetBool("IsDribbling", false);
            StartCharging();
        }

        // E키를 떼면 슛 발사
        if (Input.GetKeyUp(KeyCode.E) && isChargingShot)
        {
            ShootBall();
        }
    }
    Vector3 CalculateShootDirection(Vector3 targetPos, float force)
    {
        Vector3 toHoop = hoopTransform.position - transform.position;

        // 1) 지면 상에서의 회전만 고려 (y=0)
        Vector3 horizontalDir = new Vector3(toHoop.x, 0, toHoop.z).normalized;

        // 2) 어느 정도 위로 올려칠 각도(또는 높이)
        float arcOffset = 0.5f; // 혹은 shootArcHeight, distance 등에 따라 동적으로
        Vector3 upDir = Vector3.up * arcOffset;  // 위로 조금 들어줄 벡터

        // 3) 최종 슛 방향
        Vector3 finalDir = horizontalDir + upDir;
        finalDir.Normalize();

        return finalDir;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hoop") && canScore)
        {
            Debug.Log("Score!");
            if(scoreManager != null)
            {
                scoreManager.AddScore();
                canScore = false;
                Invoke("ResetScoring", 1f); // 1초 후 다시 득점 가능
            }
        }
    }
    void ResetScoring()
    {
        canScore = true;
    }

    void StartCharging()
    {
        isChargingShot = true;
        shootChargeStartTime = Time.time;
    
    // 드리블 상태 종료
    isDribbling = false;
    isMovingToHand = true;
    ballRigidbody.useGravity = false;
    
    // 드리블 애니메이션 종료
    animator.SetBool("IsDribbling", false);
    
    // UI 표시
    if(powerSlider != null)
    {
        powerSlider.gameObject.SetActive(true);
    }
    }
    void ShootBall()
    {
        float chargeTime = Mathf.Min(Time.time - shootChargeStartTime, maxChargeTime);
        float chargePercent = chargeTime / maxChargeTime;

        Vector3 startPos = transform.position;
        Vector3 targetPos = hoopTransform.position;
        
        // 최대 높이 계산
        float maxYPos = CalcMaxHeight(startPos, targetPos);
        // 초기 속도 계산
        Vector3 shootVelocity = CalculateShootVelocity(startPos, targetPos, maxYPos);
        
        // 차지에 따른 속도 조절
        shootVelocity *= Mathf.Lerp(minShootForce, maxShootForce, chargePercent);

        // 발사
        transform.SetParent(null);
        ballRigidbody.isKinematic = false;
        ballRigidbody.useGravity = true;
        ballRigidbody.linearVelocity = shootVelocity;
        
        // 회전 효과
        ballRigidbody.AddTorque(Random.insideUnitSphere * shootVelocity.magnitude * 0.1f, ForceMode.Impulse);

        // 상태 및 UI 초기화
        isChargingShot = false;
        isDribbling = false;
        isMovingToHand = false;
        animator.SetBool("IsDribbling", false);
        
        if(powerSlider != null)
        {
            powerSlider.gameObject.SetActive(false);
            powerSlider.value = 0;
        }
    }

    // (선택적) 차지 파워를 시각적으로 표시하는 함수
    public float GetChargePercent()
    {
        if (!isChargingShot) return 0f;
        float chargeTime = Mathf.Min(Time.time - shootChargeStartTime, maxChargeTime);
        return chargeTime / maxChargeTime;
    }


    void Start()
    {
        if (ballRigidbody == null)
            ballRigidbody = GetComponent<Rigidbody>();

        playerController = player.GetComponent<CharacterController>();
        lastPlayerPosition = player.position;
        animator = player.GetComponent<Animator>(); // 플레이어에 Animator 있다고 가정
        scoreManager = FindObjectOfType<ScoreManager>();

        // 초기화
        isDribbling = true;
        isMovingToHand = false;

        if(powerSlider != null)
        {
            powerSlider.gameObject.SetActive(false);
        }
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
    // 메인 카메라의 방향 가져오기 (y축 제외)
    Vector3 cameraForward = Camera.main.transform.forward;
    cameraForward.y = 0f;
    cameraForward.Normalize();

    // 플레이어의 앞쪽 위치 계산
    float forwardDistance = maxDistance * 0.7f + (playerSpeed * 0.1f);
    Vector3 targetPosition = player.position + (cameraForward * forwardDistance);
    targetPosition.y = transform.position.y;

    // xz 평면상의 거리만 계산
    float distance = Vector3.Distance(
        new Vector3(player.position.x, 0, player.position.z),
        new Vector3(transform.position.x, 0, transform.position.z)
    );

    // 거리가 너무 멀어졌을 때만 공의 위치 보정
    if (distance > maxDistance)
    {
        float lerpSpeed = (playerSpeed > 3f) ? 10f : 5f;
        Vector3 newPos = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * lerpSpeed);
        ballRigidbody.MovePosition(newPos);
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