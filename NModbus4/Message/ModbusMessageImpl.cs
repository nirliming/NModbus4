namespace Modbus.Message
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    using Data;

    /// <summary>
    ///     Class holding all implementation shared between two or more message types.
    ///     Interfaces expose subsets of type specific implementations.
    /// </summary>
    internal class ModbusMessageImpl
    {
        public ModbusMessageImpl()
        {
        }

        public ModbusMessageImpl(byte slaveAddress, byte functionCode)
        {
            SlaveAddress = slaveAddress;
            FunctionCode = functionCode;
        }

        public byte? ByteCount { get; set; }

        public byte? ExceptionCode { get; set; }

        public ushort TransactionId { get; set; }

        public byte FunctionCode { get; set; }

        public ushort? NumberOfPoints { get; set; }

        public byte SlaveAddress { get; set; }

        public ushort? StartAddress { get; set; }

        public ushort? SubFunctionCode { get; set; }

        public IModbusMessageDataCollection Data { get; set; }

        public byte[] MessageFrame
        {
            get
            {
                var pdu = ProtocolDataUnit;
                var frame = new MemoryStream(1 + pdu.Length);

                frame.WriteByte(SlaveAddress);
                frame.Write(pdu, 0, pdu.Length);

                return frame.ToArray();
            }
        }

        /// <summary>
        /// 构建PDU字节流
        /// </summary>
        public byte[] ProtocolDataUnit
        {
            get
            {
                // 创建一个动态字节列表，作为 PDU 字节流的临时收集容器
                List<byte> pdu = new List<byte>();

                // 1. 注入功能码（强制必选项，占 1 字节）
                // 无论是读(0x03)、写(0x10)还是异常(功能码+0x80)，PDU 的第一个字节永远是功能码
                pdu.Add(FunctionCode);

                // 2. 注入异常码（可选项，占 1 字节）
                // 只有当 PLC 返回错误（如 0x01 非法功能码，0x02 非法数据地址）时，此字段才有值
                if (ExceptionCode.HasValue)
                    pdu.Add(ExceptionCode.Value);

                // 3. 注入子功能码（可选项，占 2 字节）
                // 常见于 08 功能码（诊断功能）。同样由于上位机是 Intel 小端序，
                // 在通过 BitConverter 转为字节前，必须调用 HostToNetworkOrder 翻转为网络大端序。
                if (SubFunctionCode.HasValue)
                    pdu.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) SubFunctionCode.Value)));

                // 4. 注入起始地址（可选项，占 2 字节）
                // 读写操作时的寄存器/线圈物理起始逻辑坐标（如读取 0x0000 处的寄存器）
                if (StartAddress.HasValue)
                    pdu.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) StartAddress.Value)));

                // 5. 连续注入寄存器/线圈数量（可选项，占 2 字节）
                // 告诉下位机PLC本次操作要连续读取或者写入多少个点位（多少个寄存器或者多少个bit）
                if (NumberOfPoints.HasValue)
                    pdu.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) NumberOfPoints.Value)));

                // 6. 注入字节计数（可选项，占 1 字节）
                // 常见于写多个寄存器请求(FC16)或读寄存器响应(FC03)，用来明确后续紧跟的纯数据有多少字节
                if (ByteCount.HasValue)
                    pdu.Add(ByteCount.Value);

                // 7. 注入纯数据载荷（可选项，长度可变）
                // 如果是写入操作，这里装载的是准备写进 PLC 的具体数值；
                // Data.NetworkBytes 本身在框架底层已经完成了网络大端序的转换，所以直接追加。
                if (Data != null)
                    pdu.AddRange(Data.NetworkBytes);

                // 将动态列表转换为紧凑的固定长度 byte[] 数组，交付传输层去套上外壳（如 MBAP 头部）
                return pdu.ToArray();
            }
        }

        public void Initialize(byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException("frame", "Argument frame cannot be null.");

            if (frame.Length < Modbus.MinimumFrameSize)
                throw new FormatException(String.Format(CultureInfo.InvariantCulture,
                    "Message frame must contain at least {0} bytes of data.", Modbus.MinimumFrameSize));

            SlaveAddress = frame[0];
            FunctionCode = frame[1];
        }
    }
}
