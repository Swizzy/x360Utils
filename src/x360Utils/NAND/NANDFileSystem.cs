namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using x360Utils.Common;

    public class NANDFileSystem {
        public FileSystemEntry[] ParseFileSystem(ref NANDReader reader) { return ParseFileSystem(ref reader, reader.FsRoot); }

        public FileSystemEntry[] ParseFileSystem(ref NANDReader reader, FsRootEntry fsRoot) {
            var ret = new List<FileSystemEntry>();
            reader.Seek(fsRoot.Offset, SeekOrigin.Begin);
            var bitmap = new byte[0x2000];
            var fsinfo = new byte[0x2000];
            for(var i = 0; i < 0x10; i++) {
                var buf = reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, bitmap, i * 0x200, buf.Length);
                buf = reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, fsinfo, i * 0x200, buf.Length);
            }

            if(reader.MetaType == NANDSpare.MetaType.MetaType0 || reader.MetaType == NANDSpare.MetaType.MetaType1) {
                #region 16MB NANDs

                var offsets = new List<long>();
                for(var i = 0; i < 0x10; i++) {
                    for(var j = 0; j < 0x10; j++) {
                        offsets.Clear();
                        var name = Encoding.ASCII.GetString(fsinfo, (i * 0x200) + j * 0x20, 0x16);
                        var start = BitOperations.Swap(BitConverter.ToUInt16(fsinfo, (i * 0x200) + (j * 0x20) + 0x16));
                        var size = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, (i * 0x200) + (j * 0x20) + 0x18));
                        var timestamp = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, (i * 0x200) + (j * 0x20) + 0x1C));
                        offsets.Add(start * 0x4000); // Always add the first block!
                        if(size > 0x4000) {
                            while(true) {
                                start = BitOperations.Swap(BitConverter.ToUInt16(bitmap, start * 2));
                                //if(start == 0x1FFE || start == 0x1FFF)
                                if(start >= bitmap.Length / 2)
                                    break;
                                offsets.Add(start * 0x4000);
                            }
                        }
                        if(!name.StartsWith("\0") && !name.StartsWith("\x5")) // Ignore empty and deleted entries
                            ret.Add(new FileSystemEntry(name.Substring(0, name.IndexOf('\0')), size, timestamp, offsets.ToArray()));
                    }
                }

                #endregion
            }

            else
                throw new NotImplementedException();
            return ret.ToArray();
        }

        public class FileSystemEntry {
            public readonly string Filename;
            public readonly long[] Offsets;
            public readonly uint Size;
            public readonly long TimeStamp;

            public FileSystemEntry(string filename, uint size, long timeStamp, long[] offsets) {
                Offsets = offsets;
                Filename = filename;
                Size = size;
                TimeStamp = timeStamp;
            }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.AppendFormat("FSEntry: {0} Size: 0x{1:X} Timestamp: 0x{2:X} Blocks: 0x{3:X}", Filename, Size, TimeStamp, Offsets[0] / 0x4000);
                if(Size > 0x4000) {
                    for(var i = 1; i < Offsets.Length; i++)
                        sb.AppendFormat(" -> 0x{0:X}", Offsets[i] / 0x4000);
                }
                return sb.ToString();
            }

            public byte[] GetData(ref NANDReader reader) {
                var ret = new List<byte>();
                var left = (int)Size;
                foreach(var offset in Offsets) {
                    reader.Seek(offset, SeekOrigin.Begin);
                    ret.AddRange(reader.ReadBytes(left > 0x4000 ? 0x4000 : left));
                    left -= left > 0x4000 ? 0x4000 : left;
                }
                return ret.ToArray();
            }
        }
    }
}