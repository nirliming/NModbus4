namespace Modbus.Message
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;

    using Data;

    using Unme.Common;


    /**
 * ===================================================================
 * Modbus 报文物理映像 (FC 15: 写多个线圈 - 请求包)
 * ===================================================================
 *
 * 【1】核心 PDU 载荷结构 (变长: 共 6 + V 字节)
 * -------------------------------------------------------------------
 * PDU[0]     : 功能码 (1字节)          -> 固定为 0x0F
 * PDU[1, 2]  : 起始地址 (2字节)        -> 大端序 (0x0000 ~ 0xFFFF)
 * PDU[3, 4]  : 输出数量 (2字节)        -> 大端序 (1 ~ 1968 点)
 * PDU[5]     : 字节数 (1字节, ByteCount)-> 计算公式: V = 向上取整(输出数量 / 8)
 * PDU[6 ~ 5+V]: 强制线圈数值域 (V字节)  -> 状态位打包(LSB低位), 不足补0   ？？？问一下gemini为什么要按照低位打包成字节
 *
 * 【2】Modbus TCP 网口数据流 (变长: 共 13 + V 字节)
 * -------------------------------------------------------------------
 * 流向: [7字节 MBAP 报头 (含1B站号)] + [变长 PDU]
 * * Byte 0, 1  : 事务标识符 (2字节, 大端序)
 * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
 * Byte 4, 5  : 长度字段   (2字节, 大端序, 动态计算公式: 7 + V 字节) 7个字节包括Byte 6 7 8 9 10 11 12
 * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
 * Byte 7     : 功能码     (1字节, 映射 PDU[0])
 * Byte 8, 9  : 起始地址   (2字节, 映射 PDU[1,2], 大端序)
 * Byte 10,11 : 输出数量   (2字节, 映射 PDU[3,4], 大端序)
 * Byte 12    : 字节数     (1字节, 映射 PDU[5])
 * Byte 13 ~ 12+V: 线圈数据区域 (V字节, 映射 PDU[6~5+V])
 *
 * 【3】Modbus RTU 串口数据流 (变长: 共 9 + V 字节)
 * -------------------------------------------------------------------
 * 流向: [1字节 从站号] + [变长 PDU] + [2字节 CRC 校验码]
 *
 * Byte 0     : 从站物理站号 (Address)
 * Byte 1     : 功能码       (映射 PDU[0])
 * Byte 2, 3  : 起始地址     (映射 PDU[1,2], 大端序)
 * Byte 4, 5  : 输出数量     (映射 PDU[3,4], 大端序)
 * Byte 6     : 字节数       (映射 PDU[5])
 * Byte 7 ~ 6+V: 线圈数据区域 (V字节, 映射 PDU[6~5+V])
 * Byte 7+V, 8+V: CRC 循环校验 (2字节, 低字节在前, 高字节在后)
 * ===================================================================
 */

    /// <summary>
    /// 
    /// </summary>
    public class WriteMultipleCoilsRequest : AbstractModbusMessageWithData<DiscreteCollection>, IModbusRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public WriteMultipleCoilsRequest()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slaveAddress"></param>
        /// <param name="startAddress"></param>
        /// <param name="data"></param>
        public WriteMultipleCoilsRequest(byte slaveAddress, ushort startAddress, DiscreteCollection data)
            : base(slaveAddress, Modbus.WriteMultipleCoils)
        {
            StartAddress = startAddress;
            NumberOfPoints = (ushort) data.Count;
            ByteCount = (byte) ((data.Count + 7)/8);
            Data = data;
        }

        /// <summary>
        /// 
        /// </summary>
        public byte ByteCount
        {
            get { return MessageImpl.ByteCount.Value; }
            set { MessageImpl.ByteCount = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public ushort NumberOfPoints
        {
            get { return MessageImpl.NumberOfPoints.Value; }
            set
            {
                if (value > Modbus.MaximumDiscreteRequestResponseSize)
                    throw new ArgumentOutOfRangeException("NumberOfPoints",
                        String.Format(CultureInfo.InvariantCulture, "Maximum amount of data {0} coils.",
                            Modbus.MaximumDiscreteRequestResponseSize));

                MessageImpl.NumberOfPoints = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ushort StartAddress
        {
            get { return MessageImpl.StartAddress.Value; }
            set { MessageImpl.StartAddress = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public override int MinimumFrameSize
        {
            get { return 7; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Write {0} coils starting at address {1}.",
                NumberOfPoints, StartAddress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        public void ValidateResponse(IModbusMessage response)
        {
            var typedResponse = (WriteMultipleCoilsResponse) response;

            if (StartAddress != typedResponse.StartAddress)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture,
                    "Unexpected start address in response. Expected {0}, received {1}.",
                    StartAddress,
                    typedResponse.StartAddress));
            }

            if (NumberOfPoints != typedResponse.NumberOfPoints)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture,
                    "Unexpected number of points in response. Expected {0}, received {1}.",
                    NumberOfPoints,
                    typedResponse.NumberOfPoints));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            if (frame.Length < MinimumFrameSize + frame[6])
                throw new FormatException("Message frame does not contain enough bytes.");

            StartAddress = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 2));
            NumberOfPoints = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 4));
            ByteCount = frame[6];
            Data = new DiscreteCollection(frame.Slice(7, ByteCount).ToArray());
        }
    }
}
