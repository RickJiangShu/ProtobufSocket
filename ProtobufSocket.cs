/*
 * Author:  Rick
 * Create:  2017/7/26 17:24:18
 * Email:   rickjiangshu@gmail.com
 * Follow:  https://github.com/RickJiangShu
 */
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProtoBuf;

public delegate void ProtobufCallback<T>(T data);
/// <summary>
/// 使用Protocol Buffer的Socket连接
/// </summary>
public class ProtobufSocket : ProtocolSocket
{
    public static ProtobufSocket main;

    private List<SocketEvent> dispatchEvents = new List<SocketEvent>();
    private Dictionary<uint, Delegate> listeners = new Dictionary<uint, Delegate>();

    protected override void Start()
    {
        base.Start();
        
        //Listen
        received += OnReceived;

        main = this;
    }

    void Update()
    {
        while (dispatchEvents.Count > 0)
        {
            SocketEvent evt = dispatchEvents[0];
            dispatchEvents.RemoveAt(0);

            listeners[evt.protocolId].DynamicInvoke(evt.data);
        }
    }

    private void OnReceived(uint protocolId, byte[] buffer)
    {
        Delegate listener;
        if (listeners.TryGetValue(protocolId, out listener))
        {
            Type type = listener.Method.GetParameters()[0].ParameterType;
            using (MemoryStream s = new MemoryStream(buffer))
            {
                object data = Serializer.NonGeneric.Deserialize(type, s);
                dispatchEvents.Add(new SocketEvent(protocolId, data));
                //listener.DynamicInvoke(data);
            }
        }
    }

    public void Send(uint protocolId, IExtensible body)
    {
        MemoryStream bodyStream;
        int bodyLength;
        if (body != null)
        {
            //序列化包体
            bodyStream = new MemoryStream();
            Serializer.Serialize(bodyStream, body);
            bodyLength = (int)bodyStream.Length;
        }
        else
        {
            bodyLength = 0;
            bodyStream = null;
        }
        byte[] buffer = bodyStream.ToArray();
        Send(protocolId, buffer);

    }

    public void Listen<T>(uint protocolId, ProtobufCallback<T> callback) where T : IExtensible
    {
        Delegate listener;
        if (listeners.TryGetValue(protocolId, out listener))
        {
            listeners[protocolId] = Delegate.Combine(listener, callback);
        }
        else
        {
            listeners[protocolId] = callback;
        }
    }

    public void Unlisten<T>(uint protocolId, ProtobufCallback<T> callback) where T : IExtensible
    {
        Delegate listener;
        if (listeners.TryGetValue(protocolId, out listener))
        {
            listener = Delegate.Remove(listener, callback);

            if (listener == null)
                listeners.Remove(protocolId);
            else
                listeners[protocolId] = listener;
        }
    }

    private struct SocketEvent
    {
        public uint protocolId;
        public object data;
        public SocketEvent(uint protocolId, object data)
        {
            this.protocolId = protocolId;
            this.data = data;
        }
    }
}
