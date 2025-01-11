using UnityEngine;

public class ShootEventReceiver : MonoBehaviour
{
    public BallDribble ball;  // Inspector에서 공의 BallDribble 스크립트 연결

    // 애니메이션 이벤트가 호출할 함수
    public void ReleaseShot()
    {
        if(ball != null)
        {
            ball.ReleaseShot();
        }
    }
}