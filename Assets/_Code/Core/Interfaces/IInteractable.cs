public interface IInteractable
{
    /// <summary>
    /// 해당 개체와 상호작용을 시도합니다.
    /// </summary>
    /// <returns><c>true</c>: 상호작용 성공<br/>
    /// <c>false</c>: 상호작용 실패</returns>
    bool Interact();
}
