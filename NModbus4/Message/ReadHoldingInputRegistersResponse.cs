namespace Modbus.Message
{
    using System;
    using System.Globalization;
    using System.Linq;

    using Data;

    using Unme.Common;


    /**
    * ===================================================================
    * Modbus 报文物理映像 (FC 03: 读保持寄存器 / FC 04: 读输入寄存器 - 响应包)
    * ===================================================================
    *
    * 【1】核心 PDU 载荷结构 (变长: 共 2 + M 字节)
    * -------------------------------------------------------------------
    * PDU[0]     : 功能码 (1字节)          -> 固定为 0x03 或 0x04
    * PDU[1]     : 字节数 (1字节, ByteCount)-> 计算公式: M = 寄存器数量 * 2
    * PDU[2 ~ 1+M]: 寄存器数值区域 (M字节)  -> 每2字节代表一个寄存器值, 寄存器内部大端序
    *
    * 【2】Modbus TCP 网口数据流 (变长: 共 9 + M 字节)
    * -------------------------------------------------------------------
    * 流向: [7字节 MBAP 报头 (含1B站号)] + [变长 PDU]
    * * Byte 0, 1  : 事务标识符 (2字节, 大端序, 强对齐请求单号)
    * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
    * Byte 4, 5  : 长度字段   (2字节, 大端序, 动态计算公式: 3 + M 字节)
    * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
    * Byte 7     : 功能码     (1字节, 映射 PDU[0])
    * Byte 8     : 字节数     (1字节, 映射 PDU[1])
    * Byte 9 ~ 8+M: 寄存器数值区域 (M字节, 映射 PDU[2~1+M], 每2字节大端)
    *
    * 【3】Modbus RTU 串口数据流 (变长: 共 5 + M 字节)
    * -------------------------------------------------------------------
    * 流向: [1字节 从站号] + [变长 PDU] + [2字节 CRC 校验码]
    *
    * Byte 0     : 从站物理站号 (Address)
    * Byte 1     : 功能码       (映射 PDU[0])
    * Byte 2     : 字节数       (映射 PDU[1])
    * Byte 3 ~ 2+M: 寄存器数值区域 (M字节, 映射 PDU[2~1+M], 各寄存器内部大端)
    * Byte 3+M, 4+M: CRC 循环校验 (2字节, 低字节在前, 高字节在后) 小端序，串口通信硬性规定
    * ===================================================================
    */

    /// <summary>
    /// 
    /// </summary>
    public class ReadHoldingInputRegistersResponse : AbstractModbusMessageWithData<RegisterCollection>, IModbusMessage
    {
        /// <summary>
        /// 
        /// </summary>
        public ReadHoldingInputRegistersResponse()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="functionCode"></param>
        /// <param name="slaveAddress"></param>
        /// <param name="data"></param>
        public ReadHoldingInputRegistersResponse(byte functionCode, byte slaveAddress, RegisterCollection data)
            : base(slaveAddress, functionCode)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            ByteCount = data.ByteCount;
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
        public override int MinimumFrameSize
        {
            get { return 3; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "Read {0} {1} registers.", Data.Count,
                FunctionCode == Modbus.ReadHoldingRegisters ? "holding" : "input");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            if (frame.Length < MinimumFrameSize + frame[2])
                throw new FormatException("Message frame does not contain enough bytes.");

            ByteCount = frame[2];
            Data = new RegisterCollection(frame.Slice(3, ByteCount).ToArray());
        }
    }
}
