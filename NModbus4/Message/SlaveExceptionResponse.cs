namespace Modbus.Message
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;


 /**
 * ===================================================================
 * Modbus 报文物理映像 (从站统一错误异常应答包 - Slave Exception Response)
 * ===================================================================
 *
 * 【1】核心 异常 PDU 结构 (共 2 字节)
 * -------------------------------------------------------------------
 * PDU[0]     : 异常功能码 (1字节)      -> 计算公式: 原功能码 + 0x80
 * (例如读 FC03 报错时，返回 0x83)
 * PDU[1]     : 具体异常码 (1字节)      -> 官方标准规范定义的错误类型:
 * 0x01 = 非法功能码 (不支持该指令)
 * 0x02 = 非法数据地址 (寄存器越界报错)
 * 0x03 = 非法数据值 (请求点数/参数错误)
 * 0x04 = 从站设备故障 (PLC内部执行发生死报错)
 *
 * 【2】Modbus TCP 网口数据流 (共 9 字节)
 * -------------------------------------------------------------------
 * 流向: [7字节 MBAP 报头 (含1B站号)] + [2字节 异常 PDU]
 * * Byte 0, 1  : 事务标识符 (2字节, 大端序)
 * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
 * Byte 4, 5  : 长度字段   (2字节, 大端序, 固定填入 0x0003) -> 含义: 1B站号 + 2B异常PDU
 * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
 * Byte 7     : 异常功能码 (1字节, 映射 PDU[0])
 * Byte 8     : 具体异常码 (1字节, 映射 PDU[1])
 *
 * 【3】Modbus RTU 串口数据流 (共 5 字节)
 * -------------------------------------------------------------------
 * 流向: [1字节 从站号] + [2字节 异常 PDU] + [2字节 CRC 校验码]
 *
 * Byte 0     : 从站物理站号 (Address)
 * Byte 1     : 异常功能码   (映射 PDU[0])
 * Byte 2     : 具体异常码   (映射 PDU[1])
 * Byte 3, 4  : CRC 循环校验 (2字节, 低字节在前, 高字节在后)
 * ===================================================================
 */

    /// <summary>
    /// 
    /// </summary>
    public class SlaveExceptionResponse : AbstractModbusMessage, IModbusMessage
    {
        private static readonly Dictionary<byte, string> _exceptionMessages = CreateExceptionMessages();

        /// <summary>
        /// 
        /// </summary>
        public SlaveExceptionResponse()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slaveAddress"></param>
        /// <param name="functionCode"></param>
        /// <param name="exceptionCode"></param>
        public SlaveExceptionResponse(byte slaveAddress, byte functionCode, byte exceptionCode)
            : base(slaveAddress, functionCode)
        {
            SlaveExceptionCode = exceptionCode;
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
        public byte SlaveExceptionCode
        {
            get { return MessageImpl.ExceptionCode.Value; }
            set { MessageImpl.ExceptionCode = value; }
        }

        /// <summary>
        ///     Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
        /// </returns>
        public override string ToString()
        {
            string message = _exceptionMessages.ContainsKey(SlaveExceptionCode)
                ? _exceptionMessages[SlaveExceptionCode]
                : Resources.Unknown;
            return String.Format(CultureInfo.InvariantCulture, Resources.SlaveExceptionResponseFormat,
                Environment.NewLine, FunctionCode, SlaveExceptionCode, message);
        }

        internal static Dictionary<byte, string> CreateExceptionMessages()
        {
            Dictionary<byte, string> messages = new Dictionary<byte, string>(9);

            messages.Add(1, Resources.IllegalFunction);
            messages.Add(2, Resources.IllegalDataAddress);
            messages.Add(3, Resources.IllegalDataValue);
            messages.Add(4, Resources.SlaveDeviceFailure);
            messages.Add(5, Resources.Acknowlege);
            messages.Add(6, Resources.SlaveDeviceBusy);
            messages.Add(8, Resources.MemoryParityError);
            messages.Add(10, Resources.GatewayPathUnavailable);
            messages.Add(11, Resources.GatewayTargetDeviceFailedToRespond);

            return messages;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            if (FunctionCode <= Modbus.ExceptionOffset)
                throw new FormatException(Resources.SlaveExceptionResponseInvalidFunctionCode);

            SlaveExceptionCode = frame[2];
        }
    }
}
