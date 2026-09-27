/// <summary>
/// EventManager를 쉽게 등록하고 해지할 수 있게 만든 Interface
/// object가 value면 is keyword 사용 ref면 as keyword 사용
/// </summary>
public interface IChannel
{
    public void HandleEvent(ChannelInfo channel, object information = null);
}
