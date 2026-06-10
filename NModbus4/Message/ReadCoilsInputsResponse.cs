namespace Modbus.Message
{
    using System;
    using System.Globalization;
    using System.Linq;

    using Data;

    using Unme.Common;

    /**
     * ===================================================================
     * Modbus 报文物理映像 (FC 01: 读线圈 / FC 02: 读离散输入 - 响应包)
     * ===================================================================
     *
     * 【1】核心 PDU 载荷结构 (变长: 共 2 + V 字节)
     * -------------------------------------------------------------------
     * PDU[0]     : 功能码 (1字节)          -> 固定为 0x01 或 0x02
     * PDU[1]     : 字节数 (1字节, ByteCount)-> 计算公式: V = 向上取整(请求点数 / 8) 例如 13bit / 8bit = 2B
     * PDU[2 ~ 1+V]: 线圈状态数据域 (V字节)  -> 位打包(Bit-Packing): LSB在低位, 不足补0
     *
     * 【2】Modbus TCP 网口数据流 (变长: 共 9 + V 字节)
     * -------------------------------------------------------------------
     * 流向: [7字节 MBAP 报头 (含1B站号)] + [变长 PDU]
     * * Byte 0, 1  : 事务标识符 (2字节, 大端序, 强对齐请求单号)
     * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
     * Byte 4, 5  : 长度字段   (2字节, 大端序, 动态计算公式: 3 + V 字节)
     * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
     * Byte 7     : 功能码     (1字节, 映射 PDU[0])
     * Byte 8     : 字节数     (1字节, 映射 PDU[1])
     * Byte 9 ~ 8+V: 线圈状态数据区域 (V字节, 映射 PDU[2~1+V])
     *
     * 【3】Modbus RTU 串口数据流 (变长: 共 5 + V 字节)
     * -------------------------------------------------------------------
     * 流向: [1字节 从站号] + [变长 PDU] + [2字节 CRC 校验码]
     *
     * Byte 0     : 从站物理站号 (Address)
     * Byte 1     : 功能码       (映射 PDU[0])
     * Byte 2     : 字节数       (映射 PDU[1])
     * Byte 3 ~ 2+V: 线圈状态数据区域 (V字节, 映射 PDU[2 ~ 1+V])
     * Byte 3+V, 4+V: CRC 循环校验 (2字节, 低字节在前, 高字节在后) 小端序，串口通信硬性规定
     * ===================================================================
     */

    /// <summary>
    /// 
    /// </summary>
    public class ReadCoilsInputsResponse : AbstractModbusMessageWithData<DiscreteCollection>, IModbusMessage
    {
        /// <summary>
        /// 
        /// </summary>
        public ReadCoilsInputsResponse()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="functionCode"></param>
        /// <param name="slaveAddress"></param>
        /// <param name="byteCount"></param>
        /// <param name="data"></param>
        public ReadCoilsInputsResponse(byte functionCode, byte slaveAddress, byte byteCount, DiscreteCollection data)
            : base(slaveAddress, functionCode)
        {
            ByteCount = byteCount;
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
            return String.Format(CultureInfo.InvariantCulture,
                "Read {0} {1} - {2}.",
                Data.Count(),
                FunctionCode == Modbus.ReadInputs ? "inputs" : "coils",
                Data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            if (frame.Length < 3 + frame[2])
                throw new FormatException("Message frame data segment does not contain enough bytes.");

            ByteCount = frame[2];
            Data = new DiscreteCollection(frame.Slice(3, ByteCount).ToArray());
        }
    }
}
