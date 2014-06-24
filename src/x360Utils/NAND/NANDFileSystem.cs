﻿namespace x360Utils.NAND {
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
            for(var i = 0; i < bitmap.Length / 0x200; i++) {
                var buf = reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, bitmap, i * 0x200, buf.Length);
                buf = reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, fsinfo, i * 0x200, buf.Length);
            }

            if(reader.MetaType == NANDSpare.MetaType.MetaType0 || reader.MetaType == NANDSpare.MetaType.MetaType1 || reader.MetaType == NANDSpare.MetaType.MetaType2 ||
               reader.MetaType == NANDSpare.MetaType.MetaTypeNone) {
                for(var i = 0; i < fsinfo.Length / 0x20; i++) {
                    var blocks = new List<long>();
                    var name = Encoding.ASCII.GetString(fsinfo, i * 0x20, 0x16);
                    var start = BitOperations.Swap(BitConverter.ToUInt16(fsinfo, i * 0x20 + 0x16));
                    var size = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, i * 0x20 + 0x18));
                    var timestamp = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, i * 0x20 + 0x1C));
                    blocks.Add(start); // Always add the first block!
                    while(true) {
                        start = BitOperations.Swap(BitConverter.ToUInt16(bitmap, start * 2));
                        //if(start == 0x1FFF || start == 0x1FFE)
                        if (start *2 > bitmap.Length + 2)
                            break;
                        blocks.Add(start);
                    }
                    if(name.StartsWith("\0") || name.StartsWith("\x5"))
                        continue; // Ignore empty and/or deleted entries
                    if(blocks.Count > 0) {
                        if(blocks[0] == 0 || size == 0) // ignore entries with no offset / size
                            continue;
                    }
                    ret.Add(new FileSystemEntry(name.Substring(0, name.IndexOf('\0')), size, timestamp, blocks.ToArray()));
                }
            }
            else
                throw new NotSupportedException();
            return ret.ToArray();
        }

        public class FileSystemEntry {
            public readonly long[] Blocks;
            public readonly string Filename;
            public readonly uint Size;
            public readonly long TimeStamp;

            public FileSystemEntry(string filename, uint size, long timeStamp, long[] blocks) {
                Blocks = blocks;
                Filename = filename;
                Size = size;
                TimeStamp = timeStamp;
            }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.AppendFormat("FSEntry: {0} Size: 0x{1:X} Timestamp: 0x{2:X} Blocks: 0x{3:X}", Filename, Size, TimeStamp, Blocks[0]);
                if(Blocks.Length > 1) {
                    for(var i = 1; i < Blocks.Length; i++)
                        sb.AppendFormat(" -> 0x{0:X}", Blocks[i]);
                }
                return sb.ToString();
            }

            public byte[] GetData(ref NANDReader reader) {
                var ret = new List<byte>();
                var left = (int)Size;
                var clusterSize = reader.MetaType != NANDSpare.MetaType.MetaType2 ? 0x4000 : 0x20000;
                foreach(var offset in Blocks) {
                    reader.Seek(offset * clusterSize, SeekOrigin.Begin);
                    ret.AddRange(reader.ReadBytes(BitOperations.GetSmallest(left, clusterSize)));
                    left -= BitOperations.GetSmallest(left, clusterSize);
                }
                return ret.ToArray();
            }
        }
    }
}