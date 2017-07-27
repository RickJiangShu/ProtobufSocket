/*
 * Author:  Rick
 * Create:  2017/7/26 17:23:35
 * Email:   rickjiangshu@gmail.com
 * Follow:  https://github.com/RickJiangShu
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Socket基类（负责接发消息，并处理粘包）
/// </summary>
public class ProtocolSocket : MonoBehaviour
{
    public string host;//IP地址
    public int port;//端口
    public byte protocolHead;//协议头
    public bool connectOnStart;

    #region 事件
    /// <summary>
    /// 连接开始
    /// </summary>
    public event Action connectStarted;
    /// <summary>
    /// 连接成功
    /// </summary>
    public event Action connectSucceed;
    /// <summary>
    /// 连接失败
    /// </summary>
    public event Action connectFailed; 
    /// <summary>
    /// 连接断开（成功之后断开）
    /// </summary>
    public event Action disconnected;
    /// <summary>
    /// 发送成功
    /// </summary>
    public event Action sended;
    /// <summary>
    /// 接收到数据包
    /// </summary>
    public event Action<uint, byte[]> received;
    #endregion

    #region 状态
    /// <summary>
    /// 是否连接中
    /// </summary>
    public bool isConnected { get { return tcp.Connected; } }

    #endregion

    private byte[] receiveBuffer = new byte[0xffff];//设置一个缓冲区，用来保存数据
    private TcpClient tcp;
    private NetworkStream stream;

    protected virtual void Start()
    {
        if(connectOnStart)
            Connect();
    }

    /// <summary>
    /// 开始连接
    /// </summary>
    public void Connect()
    {
        tcp = new TcpClient();
        tcp.BeginConnect(host, port, ConnectCallback, null);
    }

    /// <summary>
    /// 连接回调
    /// </summary>
    public void ConnectCallback(IAsyncResult result)
    {
        try
        {
            tcp.EndConnect(result);

            stream = tcp.GetStream();
            stream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Recevie, null);

            Debug.Log("服务器连接成功！ Host:" + host + " Port:" + port);
            
            if (connectSucceed != null)
                connectSucceed();
        }
        catch
        {
            Debug.LogError("连接服务器失败！ Host:" + host + " Port:" + port);
        }
    }


    /// <summary>
    /// 发送协议包
    /// </summary>
    /// <param name="buffer"></param>
    public void Send(uint protocolId, byte[] body)
    {
        bodyLength = body.Length;
        byte[] headBuffer = new byte[8] { 0x7D, 0, 0, 0, 0, 1, 0, 0x7F };//125 127
        int totalLength = 8 + 4 + bodyLength;//协议头 + 4字节协议id + 包体
        Array.Copy(BitConverter.GetBytes(totalLength), 0, headBuffer, 1, 4); //包头写入长度

        byte[] buffer = new byte[totalLength];
        Array.Copy(headBuffer, 0, buffer, 0, 8);
        Array.Copy(BitConverter.GetBytes(protocolId), 0, buffer, 8, 4);
        if (bodyLength > 0) Array.Copy(body, 0, buffer, 12, bodyLength);
        stream.Write(buffer, 0, totalLength);
    }

    #region 读取数据包
    //缓存上一个包的数据（断包情况）
    private uint protocol;//协议号
    private int bodyWriteIndex;//包写入索引
    private int bodyLength;//包长
    private byte[] bodyBuffer;//包体

    /*
     * 解包流程：
     */

    /// <summary>
    /// 
    /// 粘包的典型四种情况
    /// A.先接收到data1,然后接收到data2.（正常情况）
    /// B.先接收到data1的部分数据,然后接收到data1余下的部分以及data2的全部. （属于半包+粘包）
    /// C.先接收到了data1的全部数据和data2的部分数据,然后接收到了data2的余下的数据.（属于粘包+半包）
    /// D.一次性接收到了data1和data2的全部数据.（属于粘包）
    /// </summary>
    /// <param name="ar"></param>
    private void Recevie(IAsyncResult ar)
    {
        int numberOfBytesRead = 0;
        try
        {
            numberOfBytesRead = stream.EndRead(ar);//读取流长度
        }
        catch
        {
            //因为要显示断线面板，所以需要在主线程中派发
            if (disconnected != null)
                disconnected();
            return;
        }

        stream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Recevie, null);

        int readIndex = 0;//当前数据包读取索引
        while (readIndex < numberOfBytesRead)
        {
            //断包处理
            if (bodyWriteIndex > 0)
            {
                int leftLength = bodyLength - bodyWriteIndex;
                int writeLength = Mathf.Min(leftLength, numberOfBytesRead);
                Array.Copy(receiveBuffer, 0, bodyBuffer, bodyWriteIndex, writeLength);

                readIndex += writeLength;
                bodyWriteIndex += writeLength;
            }
            //新包/粘包
            else
            {
                byte startFlag = receiveBuffer[readIndex]; readIndex += 1;
                if (startFlag != protocolHead)
                {
                    Debug.LogError("协议头错误！startFlag:" + startFlag + " readIndex:" + readIndex + " numberOfBytesRead:" + numberOfBytesRead);
                    return;
                }
                int totalLength = BitConverter.ToInt32(receiveBuffer, readIndex); readIndex += 4;  //eader.ReadInt32();
                if (totalLength == 0)
                {
                    Debug.LogError("协议长度为0");
                    return;
                }
                byte type = receiveBuffer[readIndex]; readIndex += 1;
                byte key = receiveBuffer[readIndex]; readIndex += 1;
                byte endFlag = receiveBuffer[readIndex]; readIndex += 1;
                protocol = BitConverter.ToUInt32(receiveBuffer, readIndex); readIndex += 4;

                bodyLength = totalLength - 12;//包体长度
                int writeLength = Mathf.Min(bodyLength, numberOfBytesRead - readIndex);//写入包的长度
                bodyBuffer = new byte[bodyLength];
                Array.Copy(receiveBuffer, readIndex, bodyBuffer, 0, writeLength);

                readIndex += writeLength;
                bodyWriteIndex += writeLength;
            }

            //完成一个包
            if (bodyWriteIndex == bodyLength)
            {
                if (received != null)
                    received(protocol, bodyBuffer);

                //清空缓存
                protocol = 0;
                bodyWriteIndex = 0;
                bodyLength = 0;
                bodyBuffer = null;
            }
        }

    }
    #endregion
}
