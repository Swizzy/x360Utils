namespace x360Utils.NAND {
    using System;
    using System.IO;

    public static class Meta {
        public enum MetaTypes {
            MetaTypeUnInitialized = int.MinValue, // Really old JTAG XeLL images
            MetaType0 = 0, // Pre Jasper (0x01198010)
            MetaType1 = 1, // Jasper, Trinity & Corona (0x00023010 [Jasper & Trinity] and 0x00043000 [Corona])
            MetaType2 = 2, // BigBlock Jasper (0x008A3020 and 0x00AA3020)
            MetaTypeNone = int.MaxValue // No spare type or unknown
        }

        internal static MetaTypes DetectSpareType(NANDReader reader, bool firsttry = true) {
            if(!reader.HasSpare)
                return MetaTypes.MetaTypeNone;
            if(firsttry)
                reader.BaseStream.Seek(0x4400, SeekOrigin.Begin);
            else
                reader.BaseStream.Seek(reader.BaseStream.Length - 0x4000, SeekOrigin.Begin);
            var tmp = new Byte[0x10];
            reader.BaseStream.Read(tmp, 0, 0x10);
            reader.Seek(0, SeekOrigin.Begin);
            var mdata = new MetaData(tmp, MetaTypes.MetaType0);
            if(!CheckIsBadBlock(mdata)) {
                if(GetLba(mdata) == 1)
                    return MetaTypes.MetaType0;
                mdata.MetaType = MetaTypes.MetaType1;
                if(GetLba(mdata) == 1)
                    return MetaTypes.MetaType1;
            }
            mdata.MetaType = MetaTypes.MetaType2;
            if(!CheckIsBadBlock(mdata)) {
                if(firsttry)
                    reader.BaseStream.Seek(0x21200, SeekOrigin.Begin);
                else if(reader.BaseStream.Length <= 0x4200000)
                    reader.BaseStream.Seek(reader.BaseStream.Length - 0x4000, SeekOrigin.Begin);
                else
                    reader.BaseStream.Seek(0x4200000 - 0x4000, SeekOrigin.Begin);
                reader.BaseStream.Read(tmp, 0, 0x10);
                reader.Seek(0, SeekOrigin.Begin);
                mdata = new MetaData(tmp, MetaTypes.MetaType2);
                if(!CheckIsBadBlock(mdata)) {
                    if(GetLba(mdata) == 1)
                        return MetaTypes.MetaType2;
                }
            }
            else
                Main.SendInfo(Main.VerbosityLevels.Low, firsttry ? "\r\nBlock 1 is bad!" : "\r\nThe last system block is bad!");
            if(firsttry)
                return DetectSpareType(reader, false);
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownMetaType);
        }

        public static byte[] CalculateEcd(ref byte[] data, int offset) {
            UInt32 i, val = 0, v = 0;
            var count = 0;
            for(i = 0; i < 0x1066; i++) {
                if((i & 31) == 0) {
                    v = ~BitConverter.ToUInt32(data, (count + offset));
                    count += 4;
                }
                val ^= v & 1;
                v >>= 1;
                if((val & 1) != 0)
                    val ^= 0x6954559;
                val >>= 1;
            }
            val = ~val;
            return new[] {
                             (byte)(val << 6), (byte)((val >> 2) & 0xFF), (byte)((val >> 10) & 0xFF), (byte)((val >> 18) & 0xFF)
                         };
        }

        internal static bool CheckPageEcd(ref byte[] blockData, int offset) {
            var calculated = CalculateEcd(ref blockData, offset);
            var actual = new byte[4];
            Buffer.BlockCopy(blockData, offset + 524, actual, 0, 4);
            return (calculated[0] == actual[0] && calculated[1] == actual[1] && calculated[2] == actual[2] && calculated[3] == actual[3]);
        }

        public static UInt16 GetLba(MetaData meta) {
            switch(meta.MetaType) {
                case MetaTypes.MetaType0:
                    return (ushort)(((meta.BlockId0 & 0xF) << 8) | meta.BlockId1);
                case MetaTypes.MetaType1:
                case MetaTypes.MetaType2:
                    return (ushort)(((meta.BlockId0 & 0xF) << 8) | (meta.BlockId1 & 0xFF));
                default:
                    throw new NotSupportedException();
            }
        }

        public static UInt16 GetFsSize(MetaData meta) { return (ushort)(meta.FsSize0 << 8 | meta.FsSize1); }

        public static UInt16 GetFreePages(MetaData meta) {
            switch(meta.MetaType) {
                case MetaTypes.MetaType0:
                case MetaTypes.MetaType1:
                    return meta.FsPageCount;
                case MetaTypes.MetaType2:
                    return (ushort)(meta.FsPageCount * 4);
                default:
                    throw new NotSupportedException();
            }
        }

        public static UInt32 GetFsSequence(MetaData meta) { return (uint)(meta.FsSequence0 | meta.FsSequence1 << 8 | meta.FsSequence2 << 16); }

        public static bool IsFsRootPage(MetaData meta) {
            switch(meta.MetaType) {
                case MetaTypes.MetaType0:
                case MetaTypes.MetaType1:
                    return meta.FsBlockType == 0x30; // SB FS Root
                case MetaTypes.MetaType2:
                    return meta.FsBlockType == 0x2C; // BB FS Root
                default:
                    throw new NotSupportedException();
            }
        }

        public static bool IsMobilePage(MetaData meta) { return meta.FsBlockType >= 0x30 && meta.FsBlockType < 0x3F; }

        public static bool CheckIsBadBlock(MetaData meta) { return meta.BadBlock != 0xFF; }

        public class MetaData {
            public readonly byte[] RawData;
            public MetaTypes MetaType;

            public MetaData(byte[] rawData) { RawData = rawData; }

            public MetaData(byte[] rawData, MetaTypes metaType) {
                MetaType = metaType;
                RawData = rawData;
            }

            public MetaData(NANDReader reader) {
                RawData = new byte[0x10];
                reader.BaseStream.Read(RawData, 0, RawData.Length);
                MetaType = reader.MetaType;
            }

            public byte BadBlock {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                            return RawData[5];
                        case MetaTypes.MetaType2:
                            return RawData[0];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte BlockId0 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                            return (byte)(RawData[1] & 0xF);
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return (byte)(RawData[2] & 0xF);
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte BlockId1 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                            return RawData[0];
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return RawData[1];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsBlockType {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return (byte)(RawData[12] & 0x3F);
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsPageCount {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return RawData[9];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSequence0 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                            return RawData[2];
                        case MetaTypes.MetaType1:
                            return RawData[0];
                        case MetaTypes.MetaType2:
                            return RawData[5];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSequence1 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                            return RawData[3];
                        case MetaTypes.MetaType2:
                            return RawData[4];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSequence2 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                            return RawData[4];
                        case MetaTypes.MetaType2:
                            return RawData[3];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSequence3 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                            return RawData[6];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSize0 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return RawData[8];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }

            public byte FsSize1 {
                get {
                    switch(MetaType) {
                        case MetaTypes.MetaType0:
                        case MetaTypes.MetaType1:
                        case MetaTypes.MetaType2:
                            return RawData[7];
                        default:
                            throw new NotSupportedException(string.Format("MetaType: {0} is not supported", MetaType));
                    }
                }
            }
        }
    }
}