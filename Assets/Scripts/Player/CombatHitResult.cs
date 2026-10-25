/// <summary>
/// 공격이 거부, 방어, 회피 또는 방어되지 않은 명중 중 어떤 결과였는지 구분한다.
/// </summary>
public enum CombatHitResult
{
    Rejected = 0,
    Blocked = 1,
    Evaded = 2,
    UnblockedHit = 3
}
