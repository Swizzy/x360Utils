namespace x360Utils.NAND {
    using System;
    using System.IO;

    public class NANDSpare {
        #region MetaType enum

        public enum MetaType {
            MetaTypeUnInitialized = int.MinValue, // Really old JTAG XeLL images
            MetaType0 = 0, // Pre Jasper (0x01198010)
            MetaType1 = 1, // Jasper, Trinity & Corona (0x00023010 [Jasper & Trinity] and 0x00043000 [Corona])
            MetaType2 = 2, // BigBlock Jasper (0x008A3020 and 0x00AA3020)
            MetaTypeNone = int.MaxValue // No spare type or unknown
        }

        #endregion

        //internal static readonly byte[] UnInitializedSpareBuffer = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        public void TestMetaUtils(string file) {
            var reader = new NANDReader(file);
            var metaType = reader.MetaType;
            for(long i = 0; i < reader.RawLength; i += 0x4200) {
                Debug.SendDebug("Seeking to page 0 of block 0x{0:X}", i / 0x4200);
                reader.RawSeek(i + 0x200, SeekOrigin.Begin);
                var mData = GetMetaData(reader.RawReadBytes(0x10));
                Main.SendInfo("Block 0x{0:X} Page 0 Information:\r\n", i / 0x4200);
                Main.SendInfo("LBA: 0x{0:X}\r\n", GetLBA(ref mData, metaType));
                Main.SendInfo("Block Type: 0x{0:X}\r\n", GetBlockType(ref mData, metaType));
                Main.SendInfo("FSSize: 0x{0:X}\r\n", GetFSSize(ref mData, metaType));
                Main.SendInfo("FsFreePages: 0x{0:X}\r\n", GetFsFreePages(ref mData, metaType));
                Main.SendInfo("FsSequence: 0x{0:X}\r\n", GetFsSequence(ref mData, metaType));
                Main.SendInfo("BadBlock Marker: 0x{0:X}\r\n", GetBadBlockMarker(ref mData, metaType));
            }
        }

        internal MetaType DetectSpareType(NANDReader reader, bool firsttry = true) {
            if(!reader.HasSpare)
                return MetaType.MetaTypeNone;
            if(firsttry)
                reader.RawSeek(0x4400, SeekOrigin.Begin);
            else
                reader.RawSeek(reader.RawLength - 0x4000, SeekOrigin.Begin);
            var tmp = reader.RawReadBytes(0x10);
            var mdata = GetMetaData(tmp);
            if(!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType0)) {
                if(GetLBARaw0(ref mdata) == 1)
                    return MetaType.MetaType0;
                if(GetLBARaw1(ref mdata) == 1)
                    return MetaType.MetaType1;
            }
            if(!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType2)) {
                if(firsttry)
                    reader.RawSeek(0x21200, SeekOrigin.Begin);
                else if(reader.RawLength <= 0x4200000)
                    reader.RawSeek(reader.RawLength - 0x4000, SeekOrigin.Begin);
                else
                    reader.RawSeek(0x4200000 - 0x4000, SeekOrigin.Begin);
                tmp = reader.RawReadBytes(0x10);
                if(!CheckIsBadBlockSpare(ref tmp, MetaType.MetaType2)) {
                    if(BlockIDFromSpare(ref tmp, MetaType.MetaType2) == 1)
                        return MetaType.MetaType2;
                }
            }
            else if(Main.VerifyVerbosityLevel(1))
                Main.SendInfo(firsttry ? "Block 1 is bad!" : "The last system block is bad!");
            if(firsttry)
                return DetectSpareType(reader, false);
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.UnkownMetaType);
        }

        public bool CheckIsBadBlockSpare(ref byte[] spareData, MetaType metaType) {
            var tmp = GetMetaData(spareData);
            return (GetBadBlockMarker(ref tmp, metaType) != 0xFF);
        }

        public bool CheckIsBadBlock(ref byte[] blockData, MetaType metaType) {
            var tmp = GetMetaData(ref blockData);
            return (GetBadBlockMarker(ref tmp, metaType) != 0xFF);
        }

        public int BlockIDFromSpare(ref byte[] spareData, MetaType metaType) {
            if(CheckIsBadBlockSpare(ref spareData, metaType))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadBlockDetected);
            var tmp = GetMetaData(spareData);
            return GetLBA(ref tmp, metaType);
        }

        public int BlockIDFromBlock(ref byte[] blockData, MetaType metaType) {
            if(CheckIsBadBlockSpare(ref blockData, metaType))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadBlockDetected);
            var tmp = GetMetaData(ref blockData);
            return GetLBA(ref tmp, metaType);
        }

        public static byte[] CalculateECD(ref byte[] data, int offset) {
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

        internal static bool CheckPageECD(ref byte[] data, int offset) {
            var actual = new byte[4];
            var calculated = CalculateECD(ref data, offset);
            Buffer.BlockCopy(data, offset + 524, actual, 0, 4);
            return (calculated[0] == actual[0] && calculated[1] == actual[1] && calculated[2] == actual[2] && calculated[3] == actual[3]);
        }

        internal static bool PageIsFS(ref byte[] data) { return false; }

        public MetaData GetMetaData(ref byte[] data, uint page) {
            if(data.Length % 0x210 != 0)
                throw new ArgumentException("data must be a multipile of 0x210 bytes!");
            var offset = (int)(page * 0x210);
            if(offset + 0x210 > data.Length)
                throw new ArgumentOutOfRangeException("page", @"Page * 0x210 + 0x210 must be within data!");
            var tmp = new byte[0x10];
            Buffer.BlockCopy(data, offset + 0x200, tmp, 0, tmp.Length);
            return new MetaData(tmp);
        }

        public MetaData GetMetaData(ref byte[] pageData) {
            if(pageData.Length != 0x210)
                throw new ArgumentException("pageData must be 0x210 bytes!");
            var tmp = new Byte[0x10];
            Buffer.BlockCopy(pageData, 0x200, tmp, 0, tmp.Length);
            return new MetaData(tmp);
        }

        public MetaData GetMetaData(byte[] pageSpare) {
            if(pageSpare.Length != 0x10)
                throw new ArgumentException("pageSpare must be 0x10 bytes!");
            return new MetaData(pageSpare);
        }

        public UInt16 GetLBA(ref MetaData data, MetaType metaType) {
            byte id0, id1;
            switch(metaType) {
                case MetaType.MetaType0:
                    id0 = data.Meta0.BlockID0;
                    id1 = data.Meta0.BlockID1;
                    break;
                case MetaType.MetaType1:
                    id0 = data.Meta1.BlockID0;
                    id1 = data.Meta1.BlockID1;
                    break;
                case MetaType.MetaType2:
                    id0 = data.Meta2.BlockID0;
                    id1 = data.Meta2.BlockID1;
                    break;
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
            return (ushort)(id0 << 8 | id1);
        }

        private static UInt16 GetLBARaw0(ref MetaData data) { return (ushort)((data.RawData[1] & 0xF) << 8 | data.RawData[0]); }

        private static UInt16 GetLBARaw1(ref MetaData data) { return (ushort)((data.RawData[2] & 0xF) << 8 | data.RawData[1]); }

        //public void SetLBA(ref MetaData data, MetaType metaType, UInt16 lba) {
        //    var id0 = (byte) ((lba >> 8) & 0xFF);
        //    var id1 = (byte) (lba & 0xFF);
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.BlockID0 = id0;
        //            data.Meta0.BlockID1 = id1;
        //            break;
        //        case 1:
        //            data.Meta1.BlockID0 = id0;
        //            data.Meta1.BlockID1 = id1;
        //            break;
        //        case 2:
        //            data.Meta2.BlockID0 = id0;
        //            data.Meta2.BlockID1 = id1;
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}

        public byte GetBlockType(ref MetaData data, MetaType metaType) {
            switch(metaType) {
                case MetaType.MetaType0:
                    return data.Meta0.FsBlockType;
                case MetaType.MetaType1:
                    return data.Meta1.FsBlockType;
                case MetaType.MetaType2:
                    return data.Meta2.FsBlockType;
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
        }

        //public void SetBlockType(ref MetaData data, MetaType metaType, byte blockType) {
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.FsBlockType = blockType;
        //            break;
        //        case 1:
        //            data.Meta1.FsBlockType = blockType;
        //            break;
        //        case 2:
        //            data.Meta2.FsBlockType = blockType;
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}

        public byte GetBadBlockMarker(ref MetaData data, MetaType metaType) {
            switch(metaType) {
                case MetaType.MetaType0:
                    return data.Meta0.BadBlock;
                case MetaType.MetaType1:
                    return data.Meta1.BadBlock;
                case MetaType.MetaType2:
                    return data.Meta2.BadBlock;
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
        }

        //public void SetBadBlockMarker(ref MetaData data, MetaType metaType, byte marker) {
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.BadBlock = marker;
        //            break;
        //        case 1:
        //            data.Meta1.BadBlock = marker;
        //            break;
        //        case 2:
        //            data.Meta2.BadBlock = marker;
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}

        public UInt16 GetFSSize(ref MetaData data, MetaType metaType) {
            byte fs0, fs1;
            switch(metaType) {
                case MetaType.MetaType0:
                    fs0 = data.Meta0.FsSize0;
                    fs1 = data.Meta0.FsSize1;
                    break;
                case MetaType.MetaType1:
                    fs0 = data.Meta1.FsSize0;
                    fs1 = data.Meta1.FsSize1;
                    break;
                case MetaType.MetaType2:
                    fs0 = data.Meta2.FsSize0;
                    fs1 = data.Meta2.FsSize1;
                    break;
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
            return (ushort)(fs0 << 8 | fs1);
        }

        //public void SetFSSize(ref MetaData data, MetaType metaType, UInt16 fsSize) {
        //    var fs0 = (byte) ((fsSize >> 8) & 0xFF);
        //    var fs1 = (byte) (fsSize & 0xFF);
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.FsSize0 = fs0;
        //            data.Meta0.FsSize1 = fs1;
        //            break;
        //        case 1:
        //            data.Meta1.FsSize0 = fs0;
        //            data.Meta1.FsSize1 = fs1;
        //            break;
        //        case 2:
        //            data.Meta2.FsSize0 = fs0;
        //            data.Meta2.FsSize1 = fs1;
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}

        public UInt16 GetFsFreePages(ref MetaData data, MetaType metaType) {
            switch(metaType) {
                case MetaType.MetaType0:
                    return data.Meta0.FsPageCount;
                case MetaType.MetaType1:
                    return data.Meta1.FsPageCount;
                case MetaType.MetaType2:
                    return (ushort)(data.Meta2.FsPageCount * 4);
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
        }

        //public void SetFsFreePages(ref MetaData data, MetaType metaType, UInt16 pageCount, bool divideIt = true) {
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.FsPageCount = (byte) (pageCount & 0xFF);
        //            break;
        //        case 1:
        //            data.Meta1.FsPageCount = (byte) (pageCount & 0xFF);
        //            break;
        //        case 2:
        //            if(divideIt)
        //                data.Meta2.FsPageCount = (byte) ((pageCount * 4) & 0xFF);
        //            else
        //                data.Meta2.FsPageCount = (byte) (pageCount & 0xFF);
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}

        public UInt32 GetFsSequence(ref MetaData data, MetaType metaType) {
            byte seq0, seq1, seq2, seq3;
            switch(metaType) {
                case MetaType.MetaType0:
                    seq0 = data.Meta0.FsSequence0;
                    seq1 = data.Meta0.FsSequence1;
                    seq2 = data.Meta0.FsSequence2;
                    seq3 = data.Meta0.FsSequence3;
                    break;
                case MetaType.MetaType1:
                    seq0 = data.Meta1.FsSequence0;
                    seq1 = data.Meta1.FsSequence1;
                    seq2 = data.Meta1.FsSequence2;
                    seq3 = data.Meta1.FsSequence3;
                    break;
                case MetaType.MetaType2:
                    seq0 = data.Meta2.FsSequence0;
                    seq1 = data.Meta2.FsSequence1;
                    seq2 = data.Meta2.FsSequence2;
                    seq3 = 0;
                    break;
                default:
                    throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
            }
            return (uint)(seq3 << 24 | seq2 << 16 | seq1 << 8 | seq0);
        }

        #region Nested type: MetaData

        public sealed class MetaData {
            internal readonly MetaType0 Meta0;
            internal readonly MetaType1 Meta1;
            internal readonly MetaType2 Meta2;

            internal readonly byte[] RawData;

            internal MetaData(byte[] rawData, MetaType metaType = MetaType.MetaTypeNone) {
                Meta0 = new MetaType0(ref rawData);
                Meta1 = new MetaType1(ref rawData);
                Meta2 = new MetaType2(ref rawData);
                //MetaType = metaType;
                RawData = rawData;
            }

            //internal MetaType MetaType { get; private set; }
        }

        #endregion

        #region Nested type: MetaType0

        internal sealed class MetaType0 {
            private byte[] _data;

            public MetaType0(ref byte[] rawData) { _data = rawData; }

            public byte FsBlockType { get { return (byte)(_data[12] & 0x3F); } }

            public byte FsPageCount { get { return _data[9]; } } // free pages left in block (ie: if 3 pages are used by cert then this would be 29:0x1d)

            public byte FsSequence0 { get { return _data[2]; } }

            public byte FsSequence1 { get { return _data[3]; } }

            public byte FsSequence2 { get { return _data[4]; } }

            public byte FsSequence3 { get { return _data[6]; } }

            public byte FsSize0 { get { return _data[8]; } }

            public byte FsSize1 { get { return _data[7]; } } // ((FsSize0<<8)+FsSize1) = cert size

            public byte BlockID0 { get { return (byte)(_data[1] & 0xF); } }

            public byte BlockID1 { get { return _data[0]; } }

            public byte BadBlock { get { return _data[5]; } }
        }

        #endregion

        #region Nested type: MetaType1

        internal sealed class MetaType1 {
            private byte[] _data;

            public MetaType1(ref byte[] rawData) { _data = rawData; }

            public byte FsBlockType { get { return (byte)(_data[12] & 0x3F); } }

            public byte FsPageCount { get { return _data[9]; } } // free pages left in block (ie: if 3 pages are used by cert then this would be 29:0x1d)

            public byte FsSequence0 { get { return _data[0]; } }

            public byte FsSequence1 { get { return _data[3]; } }

            public byte FsSequence2 { get { return _data[4]; } }

            public byte FsSequence3 { get { return _data[6]; } }

            public byte FsSize0 { get { return _data[8]; } }

            public byte FsSize1 { get { return _data[7]; } } // ((FsSize0<<8)+FsSize1) = cert size

            public byte BlockID0 { get { return (byte)(_data[2] & 0xF); } }

            public byte BlockID1 { get { return _data[1]; } }

            public byte BadBlock { get { return _data[5]; } }
        }

        #endregion

        #region Nested type: MetaType2

        internal sealed class MetaType2 {
            private byte[] _data;

            public MetaType2(ref byte[] rawData) { _data = rawData; }

            public byte BadBlock { get { return _data[0]; } }

            public byte BlockID0 { get { return (byte)(_data[2] & 0xF); } }

            public byte BlockID1 { get { return _data[1]; } }

            public byte FsBlockType { get { return (byte)(_data[12] & 0x3F); } }

            public byte FsPageCount { get { return _data[9]; } } // FS: 04 (system config reserve) free pages left in block (multiples of 4 pages, ie if 3f then 3f*4 pages are free after)

            public byte FsSequence0 { get { return _data[5]; } }

            public byte FsSequence1 { get { return _data[4]; } }

            public byte FsSequence2 { get { return _data[3]; } }

            public byte FsSize0 { get { return _data[8]; } } // FS: 20 (size of flash filesys in smallblocks >>5)

            public byte FsSize1 { get { return _data[7]; } } //FS: 06 (system reserve block number) else ((FsSize0<<16)+(FsSize1<<8)) = cert size
        }

        #endregion

        //public void SetFsSequence(ref MetaData data, MetaType metaType, UInt32 fsSequence) {
        //    var seq0 = (byte) (fsSequence & 0xFF);
        //    var seq1 = (byte) ((fsSequence >> 8) & 0xFF);
        //    var seq2 = (byte) ((fsSequence >> 16) & 0xFF);
        //    var seq3 = (byte) ((fsSequence >> 24) & 0xFF);
        //    switch(metaType) {
        //        case 0:
        //            data.Meta0.FsSequence0 = seq0;
        //            data.Meta0.FsSequence1 = seq1;
        //            data.Meta0.FsSequence2 = seq2;
        //            data.Meta0.FsSequence3 = seq3;
        //            break;
        //        case 1:
        //            data.Meta1.FsSequence0 = seq0;
        //            data.Meta1.FsSequence1 = seq1;
        //            data.Meta1.FsSequence2 = seq2;
        //            data.Meta1.FsSequence3 = seq3;
        //            break;
        //        case 2:
        //            data.Meta2.FsSequence0 = seq0;
        //            data.Meta2.FsSequence1 = seq1;
        //            data.Meta2.FsSequence2 = seq2;
        //            break;
        //        default:
        //            throw new NotSupportedException(string.Format("metaType: {0} is currently not supported!", metaType));
        //    }
        //}
    }
}