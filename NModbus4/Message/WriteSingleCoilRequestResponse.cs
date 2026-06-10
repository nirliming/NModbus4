namespace Modbus.Message
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;

    using Data;

    using Unme.Common;


 /**
 * ===================================================================
 * Modbus 报文物理映像 (FC 05: 写单个线圈 - 请求与响应 100% 同构包)
 * ===================================================================
 *
 * 【1】核心 PDU 载荷结构 (共 5 字节)
 * -------------------------------------------------------------------
 * PDU[0]     : 功能码 (1字节)          -> 固定为 0x05
 * PDU[1, 2]  : 线圈地址 (2字节)        -> 大端序 (0x0000 ~ 0xFFFF)
 * PDU[3, 4]  : 断通控制常数 (2字节)    -> 大端序。只允许两个硬编码值:
 * 0xFF00 = 强置线圈为 ON (通)
 * 0x0000 = 强置线圈为 OFF (断)
 * 注: 物理写入成功后，从站 PLC 将原封不动回传镜像此 5 字节 PDU。
 *
 * 【2】Modbus TCP 网口数据流 (共 12 字节)
 * -------------------------------------------------------------------
 * 流向: [7字节 MBAP 报头 (含1B站号)] + [5字节 核心 PDU]
 * * Byte 0, 1  : 事务标识符 (2字节, 大端序)
 * Byte 2, 3  : 协议标识符 (2字节, 固定为 0x0000)
 * Byte 4, 5  : 长度字段   (2字节, 大端序, 固定填入 0x0006)
 * Byte 6     : 单元标识符 (1字节, 物理从站站号 / Unit ID)
 * Byte 7     : 功能码     (1字节, 映射 PDU[0])
 * Byte 8, 9  : 线圈地址   (2字节, 映射 PDU[1,2], 大端序)
 * Byte 10,11 : 控制常数   (2字节, 映射 PDU[3,4], 大端序)
 *
 * 【3】Modbus RTU 串口数据流 (共 8 字节)
 * -------------------------------------------------------------------
 * 流向: [1字节 从站号] + [5字节 核心 PDU] + [2字节 CRC 校验码]
 *
 * Byte 0     : 从站物理站号 (Address)
 * Byte 1     : 功能码       (映射 PDU[0])
 * Byte 2, 3  : 线圈地址     (映射 PDU[1,2], 大端序)
 * Byte 4, 5  : 控制常数     (映射 PDU[3,4], 大端序)
 * Byte 6, 7  : CRC 循环校验 (2字节, 低字节在前, 高字节在后)  小端序，串口通信硬性规定
 * ===================================================================
 */

    /// <summary>
    /// 
    /// </summary>
    public class WriteSingleCoilRequestResponse : AbstractModbusMessageWithData<RegisterCollection>, IModbusRequest
    {
        /// <summary>
        /// 
        /// </summary>
        public WriteSingleCoilRequestResponse()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slaveAddress"></param>
        /// <param name="startAddress"></param>
        /// <param name="coilState"></param>
        public WriteSingleCoilRequestResponse(byte slaveAddress, ushort startAddress, bool coilState)
            : base(slaveAddress, Modbus.WriteSingleCoil)
        {
            StartAddress = startAddress;
            Data = new RegisterCollection(coilState ? Modbus.CoilOn : Modbus.CoilOff);
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
        public ushort StartAddress
        {
            get { return MessageImpl.StartAddress.Value; }
            set { MessageImpl.StartAddress = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            Debug.Assert(Data != null, "Argument Data cannot be null.");
            Debug.Assert(Data.Count() == 1, "Data should have a count of 1.");

            return String.Format(CultureInfo.InvariantCulture,
                "Write single coil {0} at address {1}.",
                Data.First() == Modbus.CoilOn ? 1 : 0,
                StartAddress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        public void ValidateResponse(IModbusMessage response)
        {
            var typedResponse = (WriteSingleCoilRequestResponse) response;

            if (StartAddress != typedResponse.StartAddress)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture,
                    "Unexpected start address in response. Expected {0}, received {1}.",
                    StartAddress,
                    typedResponse.StartAddress));
            }

            if (Data.First() != typedResponse.Data.First())
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture,
                    "Unexpected data in response. Expected {0}, received {1}.",
                    Data.First(),
                    typedResponse.Data.First()));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frame"></param>
        protected override void InitializeUnique(byte[] frame)
        {
            StartAddress = (ushort) IPAddress.NetworkToHostOrder(BitConverter.ToInt16(frame, 2));
            Data = new RegisterCollection(frame.Slice(4, 2).ToArray());
        }
    }
}
