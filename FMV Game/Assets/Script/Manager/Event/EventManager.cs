using System.Collections.Generic;
using System;

/// <summary>
/// Event를 관리하고 있는 Manager
/// Observer Pattern을 이용해서 만든 Manager다.
/// </summary>
public class EventManager 
{
    // Key : Enum, Value : Action List
    private Dictionary<ChannelInfo, List<Action<ChannelInfo, object>>> channels;

    // EventManager 생성자
    public EventManager()
    {
        channels = new Dictionary<ChannelInfo, List<Action<ChannelInfo, object>>>();
    }

    /// <summary>
    /// Event 구독(등록)
    /// </summary>
    /// <param name="channelType">Enum Type</param>
    /// <param name="channel">채널, object 값을 가진 함수를 등록</param>
    public void Subscription(ChannelInfo channelType, Action<ChannelInfo, object> channel) 
    {
        // enum type 찾기 있으면 Value List에 추가, 없으면 List 생성
        if(channels.ContainsKey(channelType))
        {
            channels[channelType].Add(channel);
        }
        else
        {
            var channelList = new List<Action<ChannelInfo, object>>();
            channelList.Add(channel);
            channels.Add(channelType, channelList);
        }
    }

    /// <summary>
    /// Event 구독해지
    /// Enum Type과 관련된 모든 구독을 해지한다.
    /// </summary>
    /// <param name="channelType">Enum Type</param>
    public void Unsubscription(ChannelInfo channelType)
    {
        // type을 전체 제거
        if(channels.ContainsKey(channelType))
        {
            channels.Remove(channelType);
        }
    }

    /// <summary>
    /// Event 구독 해지 Override 
    /// Enum Type에 있는 함수만 구독 해지
    /// </summary>
    /// <param name="channelType">Enum Type</param>
    /// <param name="channel">해당 함수만</param>
    public void Unsubscription(ChannelInfo channelType, Action<ChannelInfo, object> channel)
    {
        // type 안에서 순회하면서 같은 함수가 있으면 제거
        if(channels.ContainsKey(channelType))
        {
            var channelList = channels[channelType];
            for(int i = 0; i < channelList.Count; i++)
            {
                if (channelList[i] == channel)
                {
                    channelList.RemoveAt(i);
                    return;
                }
            }
        }
    }
    
    /// <summary>
    /// 구독을 한 사람들에게 알리는 함수
    /// Enum Type에 속해 있는 사람들에게 Object 값이 바뀌었다고 알려준다.
    /// </summary>
    /// <param name="channelType">Enum Type</param>
    /// <param name="eventInfo">바뀔 Object 값</param>
    public void Notify(ChannelInfo channelType, object eventInfo = null)
    {
        // Type에 관련된 함수를 순회하면서 값을 바꾼다.
        if(channels.ContainsKey(channelType))
        {
            // Event 중간에 Unsubscription을 하면 터지는 문제가 있어서 복사본으로 가져와서 실행한다.
            var channelList = new List<Action<ChannelInfo, object>>(channels[channelType]);
            foreach (var channel in channelList)
            {
                channel?.Invoke(channelType, eventInfo);
            }
        }
    }
}

