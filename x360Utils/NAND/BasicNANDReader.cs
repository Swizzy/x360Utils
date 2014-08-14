namespace x360Utils.NAND
{
    using System.IO;

    class BasicNANDReader : NANDReader
    {
        public BasicNANDReader(string file): base(file) {}

        public BasicNANDReader(Stream input): base(input) {}
    }
}
