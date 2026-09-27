/// <summary>
/// Locator Pattern으로 Singleton Pattern의 장점인 전역 접근이 가능하다.
/// 모든 Manager를 가지고 있는 객체다.
/// </summary>
/// <typeparam name="T">Manager Type만 등록해야 한다.</typeparam>
public static class Locator<T> 
{
    private static T manager;
    public static void Provide(T _manager) => manager = _manager;
    public static T Get() => manager;
}
