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
 * Modbus 报文物理映像 (FC 16: 写多个寄存器 - 请求包)
 * ===================================================================
 *
 * 【1】核心 PDU 载荷结构 (变长: 共 6 + M 字节)
 * -------------------------------------------------------------------
 * PDU[0]     : 功能码 (1字节)          -> 固定为 0x10
 * PDU[1, 2]  : 起始地址 (2字节)        -> 大端序 (0x0000 ~ 0xFFFF)
 * PDU[3, 4]  : 寄存器数量 (2字节)      -> 大端序 (1 ~ 123 个寄存器)
 * PDU[5]     : 字节数 (1字节, ByteCount)-> 计算公式: M = 寄存器数量 * 2
 * PDU[6 ~ 5+M]: 寄存器写入具体值 (M字节)-> 各个寄存器连续拼接, 内部大端序
 *
 * 【2】Modbus TCP 网口数据流 (变长: 共 13 + M 字节)
 * -------------------------------------------------------------------
 * 流向: [7字节 MBAP 报头 (含1B站号)] + [变长 PDU]
 * * Byte 0, 1  : 事务标识符 (2字节, 大端序)
 * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
 * Byte 4, 5  : 长度字段   (2字节, 大端序, 动态计算公式: 7 + M 字节)
 * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
 * Byte 7     : 功能码     (1字节, 映射 PDU[0])
 * Byte 8, 9  : 起始地址   (2字节, 映射 PDU[1,2], 大端序)
 * Byte 10,11 : 寄存器数量 (2字节, 映射 PDU[3,4], 大端序)
 * Byte 12    : 字节数     (1字节, 映射 PDU[5])
 * Byte 13 ~ 12+M: 寄存器值写入区域 (M字节, 映射 PDU[6~5+M])
 *
 * 【3】Modbus RTU 串口数据流 (变长: 共 9 + M 字节)
 * -------------------------------------------------------------------
 * 流向: [1字节 从站号] + [变长 PDU] + [2字节 CRC 校验码]
 *
 * Byte 0     : 从站物理站号 (Address)
 * Byte 1     : 功能码       (映射 PDU[0])
 * Byte 2, 3  : 起始地址     (映射 PDU[1,2], 大端序)
 * Byte 4, 5  : 寄存器数量   (映射 PDU[3,4], 大端序)
 * Byte 6     : 字节数       (映射 PDU[5])
 * Byte 7 ~ 6+M: 寄存器值写入区域 (M字节, 映射 PDU[6~5+M])
 * Byte 7+M, 8+M: CRC 循环校验 (2字节, 低字节在前, 高字节在后)
 * ===================================================================
 */

    /// <summary>
    /// 
    /// </summary>
    public class WriteMultipleRegistersRequest : AbstractModbusMessageWithData<RegisterCollection>, IModbusRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public WriteMultipleRegistersRequest()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slaveAddress"></param>
        /// <param name="startAddress"></param>
        /// <param name="data"></param>
        public WriteMultipleRegistersRequest(byte slaveAddress, ushort startAddress, RegisterCollection data)
            : base(slaveAddress, Modbus.WriteMultipleRegisters)
        {
            StartAddress = startAddress;
            NumberOfPoints = (ushort) data.Count;
            ByteCount = (byte) (data.Count*2);
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
                if (value > Modbus.MaximumRegisterRequestResponseSize)
                    throw new ArgumentOutOfRangeException("NumberOfPoints",
                        String.Format(CultureInfo.InvariantCulture, "Maximum amount of data {0} registers.",
                            Modbus.MaximumRegisterRequestResponseSize));

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
            return String.Format(CultureInfo.InvariantCulture, "Write {0} holding registers starting at address {1}.",
                NumberOfPoints, StartAddress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        public void ValidateResponse(IModbusMessage response)
        {
            var typedResponse = (WriteMultipleRegistersResponse) response;

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
            Data = new RegisterCollection(frame.Slice(7, ByteCount).ToArray());
        }
    }
}
