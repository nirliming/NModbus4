namespace Modbus.IO
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    
    using Message;
    
    using Unme.Common;



    /// <summary>
    ///     Transport for Internet protocols.
    ///     Refined Abstraction - http://en.wikipedia.org/wiki/Bridge_Pattern
    ///     基于以太网（Internet protocols）的 Modbus 传输层实现（主要用于 Modbus TCP）。
    ///     在软件设计模式中充当桥接模式（Bridge Pattern）的“修正抽象化（Refined Abstraction）”角色。
    /// </summary>
    internal class ModbusIpTransport : ModbusTransport
    {
        // 事务标识符（Transaction ID）的同步锁，确保多线程下并发获取自增 ID 时的线程安全
        private static readonly object _transactionIdLock = new object();
        // 当前实例的事务标识符计数器
        private ushort _transactionId;

        /// <summary>
        /// 构造函数，初始化 Modbus IP 传输实例。
        /// </summary>
        /// <param name="streamResource">底层物理通信流资源（例如封装了 TcpClient 或 UdpClient 的网络适配器适配类）</param>
        internal ModbusIpTransport(IStreamResource streamResource)
            : base(streamResource)
        {
            Debug.Assert(streamResource != null, "Argument streamResource cannot be null.");
        }

        /// <summary>
        ///  核心静态工具方法：从底层流资源中完整、无拆包隐患地读取一个 Modbus TCP 报文帧（含MBAP报头和PDU）。
        /// </summary>
        /// <param name="streamResource">底层的物理流句柄</param>
        /// <returns>拼装完整的原始二进制字节数组</returns>
        internal static byte[] ReadRequestResponse(IStreamResource streamResource)
        {
            // read header
            // 1. 【核心节拍一】：首先精准读取 MBAP 报头的前 6 个字节（含事务ID、协议ID和长度字段）
            var mbapHeader = new byte[6];
            int numBytesRead = 0;

            // 工业网关核心防断包设计：由于 TCP 是面向字节流的无保护管道，数据可能会被网络层拆包分片，
            // 必须使用 While 循环强制等满 6 个字节，否则绝不向下流转。
            while (numBytesRead != 6)
            {
                int bRead = streamResource.Read(mbapHeader, numBytesRead, 6 - numBytesRead);

                // 若 Read 返回 0，代表物理 Socket 管道在对面已被 PLC 固件强行关闭或链路异常断开
                if (bRead == 0)
                    throw new IOException("Read resulted in 0 bytes returned.");

                numBytesRead += bRead;
            }

            Debug.WriteLine("MBAP header: {0}", string.Join(", ", mbapHeader));

            // 提取 MBAP 报头中第 4~5 字节的 Length 字段。
            // 因为 BitConverter 默认按上位机 X86/X64 架构的小端序解析，而网络流是大端序，
            // 必须调用 HostToNetworkOrder 将网络大端序反转为本地计算机小端整型数值。
            var frameLength = (ushort) IPAddress.HostToNetworkOrder(BitConverter.ToInt16(mbapHeader, 4));
            Debug.WriteLine("{0} bytes in PDU.", frameLength);

            // 2. 【核心节拍二】：根据报头中指示的后续长度，精准收割 PDU 业务指令内容
            var messageFrame = new byte[frameLength];
            numBytesRead = 0;
            // 同样采用死等While循环，确保完整将 PLC 响应的数据区或者是网线中积压的数据接收完全
            while (numBytesRead != frameLength)
            {
                int bRead = streamResource.Read(messageFrame, numBytesRead, frameLength - numBytesRead);

                if (bRead == 0)
                    throw new IOException("Read resulted in 0 bytes returned.");

                numBytesRead += bRead;
            }

            Debug.WriteLine("PDU: {0}", frameLength);
            //3. 【核心节拍三】：将 6 字节基本头部与变长的 PDU 数据合二为一，还原完整的 Modbus TCP 控制帧
            var frame = mbapHeader.Concat(messageFrame).ToArray();
            Debug.WriteLine("RX: {0}", string.Join(", ", frame));

            return frame;
        }

        /// <summary>
        /// 静态工具方法：将 C# 上层打包的消息对象翻译映射为符合 Modbus 官方标准的 7 字节 MBAP 报文头。
        /// </summary>
        /// <param name="message">Modbus 消息实体对象</param>
        /// <returns>7 字节的大端序 MBAP 头部数组</returns>
        internal static byte[] GetMbapHeader(IModbusMessage message)
        {

            /* ==============================================================================================
             * MODBUS TCP/IP 应用报文头结构模型 (MBAP Header)
             * ==============================================================================================
             * 字节偏移   |     字段名称      | 长度(Byte) |          描述           |         传输端序
             * -----------+-------------------+------------+-------------------------+----------------------------
             * Byte 0,1 |  Transaction ID   |     2      | 事务标识符（请求应答配对）| 大端序 (Big-Endian网络序)
             * Byte 2,3 |  Protocol ID      |     2      | 协议标识符（固定为 0）    | 大端序 (Big-Endian网络序)
             * Byte 4,5 |  Length           |     2      | 后续字节数（UnitID+PDU）  | 大端序 (Big-Endian网络序)
             * Byte 6   |  Unit ID / Slave  |     1      | 单元标识符（从站站号）    | 单字节（不涉及端序）
             * -----------+-------------------+------------+-------------------------+----------------------------
             * [Byte 7+] |  Modbus PDU       |   1~253    | 功能码(1B) + 业务数据区 | 标准 Modbus 规范约束
             * -----------+-------------------+------------+-------------------------+----------------------------
             * ==============================================================================================
             */

            /*
             * 主机字节序(Host Order):取决于电脑的cpu架构。绝大多数个人电脑采用的都是小端序(Little-Endian),
             * 即把数据的低位字节存储在内存的低地址端。
             * 网络字节序(Network Order)：在互联网协议(TCP/IP)协议中，为了保证不同架构的计算机可以正常通信，
             * 统一规定必须使用大端序（Big-Endian）传输数据，即把数据的高位字节存储在低地址端。
             * 如果在发送网络数据前不进行转换，一台小端序主机的多字节整数（如 int 或 short）直接传给网络，
             * 另一台大端序主机（或遵循网络协议的解析器）解析出来的数字就会发生错乱。
             */


            // 将本地计算机小端序的 16位 事务 ID 反转转换为符合 TCP 规范的网络大端序
            byte[] transactionId = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) message.TransactionId));

            //计算 Length 字段：Modbus TCP 标准硬性规定，此处长度 = 1字节单元标识符(从站号) + PDU 数据区的整体长度
            byte[] length = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) (message.ProtocolDataUnit.Length + 1)));
            // 显式开辟 7 字节固定容量的内存流，避免内存自动动态扩容（Array Resize）造成的工控高频轮询堆碎料和性能损耗
            var stream = new MemoryStream(7);
            // [第 0~1 字节]：写入网络大端序的事务标识符（Transaction ID，类似于报文的快递单号）
            stream.Write(transactionId, 0, transactionId.Length);
            // [第 2~3 字节]：写入协议标识符（Protocol ID），工业标准固定输入 0x00, 0x00 代表 Modbus 协议
            stream.WriteByte(0);
            stream.WriteByte(0);
            // [第 4~5 字节]：写入计算好的后续报文剩余字节总长度 (Length)
            stream.Write(length, 0, length.Length);
            // [第 6 字节]：写入单元标识符（Unit ID），即挂载在 TCP 网关下游或者本地网口的具体从站设备号（Slave Address）
            stream.WriteByte(message.SlaveAddress);

            return stream.ToArray();
        }

        /// <summary>
        /// Create a new transaction ID.
        /// 生成并维护一个全新的、线程安全的、在 1 ~ ushort.MaxValue 之间循环自增的事务标识符。
        /// </summary>
        internal virtual ushort GetNewTransactionId()
        {
            // 如果计数器冲到了无符号短整型的极限最大值，强行归位重置为 1，否则正常阶梯递增
            lock (_transactionIdLock)
                _transactionId = _transactionId == UInt16.MaxValue ? (ushort) 1 : ++_transactionId;

            return _transactionId;
        }

        /// <summary>
        /// 反串行化核心：根据底层网线收到的完整 TCP 报文，将其切片、逆转端序并反向组装为强类型的 C# Message 对象。
        /// </summary>
        internal IModbusMessage CreateMessageAndInitializeTransactionId<T>(byte[] fullFrame)
            where T : IModbusMessage, new()
        {
            // 切片剥离出前 6 字节的控制报头
            byte[] mbapHeader = fullFrame.Slice(0, 6).ToArray();
            // 剥离出从第 6 字节开始直到末尾的实际 PDU 业务载荷（功能码与物理寄存器数据）
            byte[] messageFrame = fullFrame.Slice(6, fullFrame.Length - 6).ToArray();
            // 利用框架的消息处理工厂，根据底层数据自动实例化创建出对应的强类型响应模型（如 ReadHoldingInputRegistersResponse）
            IModbusMessage response = CreateResponse<T>(messageFrame);
            // 提取报头最前面的 2 个字节，将其从网络网络大端序逆转恢复为本地计算机通用的小端序，并回填进对象的 TransactionId 属性中
            response.TransactionId = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(mbapHeader, 0));

            return response;
        }

        /// <summary>
        ///     重写传输层基类方法：将业务消息对象组装拼接为最终飞向网线的完整 Modbus TCP 报文数据帧。
        /// </summary>
        internal override byte[] BuildMessageFrame(IModbusMessage message)
        {
            // 生成 7 字节的控制头
            byte[] header = GetMbapHeader(message);
            // 提取具体的协议数据单元
            byte[] pdu = message.ProtocolDataUnit;
            // 动态开辟大小刚好的内存流将头尾咬合拼接
            var messageBody = new MemoryStream(header.Length + pdu.Length);
            messageBody.Write(header, 0, header.Length);
            messageBody.Write(pdu, 0, pdu.Length);

            return messageBody.ToArray();
        }
        /// <summary>
        ///     重写传输层基类方法：主站端（Master）或从站端真正实施报文发送动作的入口。
        /// </summary>
        internal override void Write(IModbusMessage message)
        {
            // 1. 发送前通过独占锁生成一个全局唯一的事务序列号并赋予消息
            message.TransactionId = GetNewTransactionId();
            // 2. 构建二进制控制数据帧
            byte[] frame = BuildMessageFrame(message);
            Debug.WriteLine("TX: {0}", string.Join(", ", frame));

            // 3. 击穿抽象层，调用物理 IStreamResource（如 TcpClientAdapter）将电平信号彻底打向以太网物理硬件
            StreamResource.Write(frame, 0, frame.Length);
        }

        /// <summary>
        /// 重写基类方法：读取请求帧（主要供上位机模拟 PLC 从站/服务器端接收主站指令时使用）。
        /// </summary>
        internal override byte[] ReadRequest()
        {
            return ReadRequestResponse(StreamResource);
        }

        /// <summary>
        /// 重写基类方法：读取响应帧（主站端发送读写指令后，调用此方法阻塞式接收远端 PLC 返回的字节流并反序列化实体）。
        /// </summary>
        internal override IModbusMessage ReadResponse<T>()
        {
            return CreateMessageAndInitializeTransactionId<T>(ReadRequestResponse(StreamResource));
        }

        /// <summary>
        ///  重写基类方法：提供 TCP 协议极其关键的专属安全防御验证。
        /// </summary>
        internal override void OnValidateResponse(IModbusMessage request, IModbusMessage response)
        {
            // 防御机制：由于网络波动、网关串流可能导致收发节奏错位。
            // 必须严格校验 PLC 应答回来的“快递单号（TransactionId）”是否和我们刚刚发出去的请求完全匹配。
            if (request.TransactionId != response.TransactionId)
                throw new IOException(String.Format(CultureInfo.InvariantCulture,
                    "Response was not of expected transaction ID. Expected {0}, received {1}.", request.TransactionId,
                    response.TransactionId));
        }
        /// <summary>
        ///     重写基类方法：提供在网络高延迟、PLC 历史积压报文延迟抵达时的“重试过滤与垃圾包清理”钩子。
        /// </summary>
        internal override bool OnShouldRetryResponse(IModbusMessage request, IModbusMessage response)
        {
            // 判定条件：如果我们当前请求的事务 ID 大于 PLC 返回的事务 ID，且两者的阶梯差值在用户配置的旧响应阈值（RetryOnOldResponseThreshold）之内
            if (request.TransactionId > response.TransactionId && request.TransactionId - response.TransactionId < RetryOnOldResponseThreshold)
            {
                // This response was from a previous request
                // 说明这条响应是上一次请求因为网卡、交换机阻塞而延迟抵达的“迟到的垃圾包”。
                // 返回 true 触发底层通信引擎：直接将该过期垃圾包无视并扔掉，不返回给业务层，同时在锁内重新尝试读取，直到抓取到对准当前事务 ID 的正确回应。
                return true;
            }

            return base.OnShouldRetryResponse(request, response);
        }
    }
}
