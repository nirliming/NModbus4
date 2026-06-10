namespace Modbus.Message
{
    using System;
    using System.Globalization;
    using System.Net;


    /**
    * ===================================================================
    * Modbus 报文物理映像 (FC 16: 写多个寄存器 - 响应包)
    * ===================================================================
    *
    * 【1】核心 PDU 载荷结构 (共 5 字节)
    * -------------------------------------------------------------------
    * PDU[0]     : 功能码 (1字节)          -> 固定为 0x10
    * PDU[1, 2]  : 起始地址 (2字节)        -> 大端序 (与原请求包对齐一致)
    * PDU[3, 4]  : 寄存器数量 (2字节)      -> 大端序 (与原请求包对齐一致)
    *
    * 【2】Modbus TCP 网口数据流 (共 12 字节)
    * -------------------------------------------------------------------
    * 流向: [7字节 MBAP 报头 (含1B站号)] + [5字节 核心 PDU]
    * * Byte 0, 1  : 事务标识符 (2字节, 大端序)
    * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
    * Byte 4, 5  : 长度字段   (2字节, 大端序, 固定填入 0x0006)
    * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
    * Byte 7     : 功能码     (1字节, 映射 PDU[0])
    * Byte 8, 9  : 起始地址   (2字节, 映射 PDU[1,2], 大端序)
    * Byte 10,11 : 寄存器数量 (2字节, 映射 PDU[3,4], 大端序)
    *
    * 【3】Modbus RTU 串口数据流 (共 8 字节)
    * -------------------------------------------------------------------
    * 流向: [1字节 从站号] + [5字节 核心 PDU] + [2字节 CRC 校验码]
    *
    * Byte 0     : 从站物理站号 (Address)
    * Byte 1     : 功能码       (映射 PDU[0])
    * Byte 2, 3  : 起始地址     (映射 PDU[1,2], 大端序)
    * Byte 4, 5  : 寄存器数量   (映射 PDU[3,4], 大端序)
    * Byte 6, 7  : CRC 循环校验 (2字节, 低字节在前, 高字节在后)   小端序，串口通信硬性规定
    * ===================================================================
    */

    /// <summary>
    /// 
    /// </summary>
    public class WriteMultipleRegistersResponse : AbstractModbusMessage, IModbusMessage
    {
        /// <summary>
        /// 
        /// </summary>
        public WriteMultipleRegistersResponse()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slaveAddress"></param>
        /// <param name="startAddress"></param>
        /// <param name="numberOfPoints"></param>
        public WriteMultipleRegistersResponse(byte slaveAddress, ushort startAddress, ushort numberOfPoints)
            : base(slaveAddress, Modbus.WriteMultipleRegisters)
        {
            StartAddress = startAddress;
            NumberOfPoints = numberOfPoints;
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
                {
                    throw new ArgumentOutOfRangeException("NumberOfPoints",
                        String.Format(CultureInfo.InvariantCulture, "Maximum amount of data {0} registers.",
                            Modbus.MaximumRegisterRequestResponseSize));
                }

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
            get { return 6; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Wrote {0} holding registers starting at address {1}.",
                NumberOfPoints, StartAddress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            StartAddress = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 2));
            NumberOfPoints = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 4));
        }
    }
}
