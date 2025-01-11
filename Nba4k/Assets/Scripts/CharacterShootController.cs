using UnityEngine;

public class CharacterShootController : MonoBehaviour
{
    public BallDribble ballDribble;  // Ball 오브젝트의 BallDribble 스크립트 참조

    public void ReleaseShot()
    {
        if(ballDribble != null)
        {
            ballDribble.ReleaseShot();  // Ball의 ReleaseShot 함수 호출
        }
    }
}