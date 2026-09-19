public interface IDamageable
{
    /// <summary>
    /// 해당 개체에게 대미지를 가합니다.<br/>
    /// 개체의 상태에 따라 실제로 가해지는 대미지의 수치는 달라질 수 있습니다.
    /// </summary>
    /// <param name="damage">가할 대미지</param>
    /// <returns>실제로 가한 대미지</returns>
    int Damage(int damage);
    
    /// <summary>
    /// 해당 개체가 대미지를 받지 않는 무적 상태인지 확인합니다.
    /// </summary>
    /// <returns>개체의 무적 상태</returns>
    bool IsInvincible();
}
